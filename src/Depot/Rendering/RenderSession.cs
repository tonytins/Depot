using System.Text.Json;

namespace Depot.Rendering;

/// <summary>
/// A conversation with <c>depot-render --serve</c>: a request line written, an answer line read. Requests go one at
/// a time; the caller keeps them from overlapping.
/// </summary>
/// <remarks>
/// Once an answer is late or doesn't come, the next line read might answer the request before, so the session
/// stops: <see cref="IsBroken"/> is set, and the owner starts depot-render afresh.
/// </remarks>
public sealed class RenderSession(TextWriter input, TextReader output, TimeSpan timeout)
{
    private readonly TextWriter input = input ?? throw new ArgumentNullException(nameof(input));
    private readonly TextReader output = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>Whether the session has failed, and can't be used again.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>Asks for a picture, and waits for the answer.</summary>
    /// <exception cref="RendererException">depot-render didn't answer in time, stopped, or answered nonsense.</exception>
    public async Task<RenderReply> RenderAsync(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsBroken)
            throw new RendererException(Words.Get(TextKeys.RendererFailedBefore));

        using CancellationTokenSource deadline = new(timeout);
        try
        {
            // JSON escapes line breaks inside strings, so a request is always one line.
            await input.WriteLineAsync(JsonSerializer.Serialize(request, RenderJson.Options).AsMemory(), deadline.Token);
            await input.FlushAsync(deadline.Token);
            var line = await output.ReadLineAsync(deadline.Token)
                ?? throw new RendererException("depot-render stopped without answering.");
            return RenderReply.Parse(line);
        }
        catch (OperationCanceledException exception)
        {
            IsBroken = true;
            throw new RendererException(Words.Get(TextKeys.RendererTimedOut, $"{timeout.TotalSeconds:0.#}"), exception);
        }
        catch (IOException exception)
        {
            IsBroken = true;
            throw new RendererException(Words.Get(TextKeys.RendererLost, exception.Message), exception);
        }
        catch (RendererException)
        {
            IsBroken = true;
            throw;
        }
    }
}
