using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Depot.Rendering;

/// <summary>
/// depot-render, kept running with <c>--serve</c> so the canvas redraws as you type without starting raylib for
/// each picture. It's started on the first request, and again after it fails.
/// </summary>
public sealed class Renderer(string executable) : IDisposable
{
    /// <summary>How long a picture may take before depot-render is given up on.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const int RememberedErrorLines = 5;

    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private readonly Queue<string> errorLines = new();
    private Process? process;
    private RenderSession? session;
    private bool disposed;

    /// <summary>The program Depot runs.</summary>
    public string Executable { get; } = executable ?? throw new ArgumentNullException(nameof(executable));

    /// <summary>Asks depot-render for a picture. Requests made while one is drawn wait their turn.</summary>
    /// <exception cref="RendererException">depot-render couldn't start, or failed; the next request starts it again.</exception>
    public async Task<RenderReply> RenderAsync(RenderRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await oneAtATime.WaitAsync();
        try
        {
            var current = Start();
            try
            {
                return await current.RenderAsync(request);
            }
            catch (RendererException exception)
            {
                var said = Stop();
                throw said is null ? exception : new RendererException(Words.Get(TextKeys.RendererSaid, exception.Message, said), exception);
            }
        }
        finally
        {
            oneAtATime.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Stop();
        oneAtATime.Dispose();
    }

    private RenderSession Start()
    {
        if (session is { IsBroken: false } && process is { HasExited: false })
            return session;
        Stop();

        ProcessStartInfo start = new(Executable, "--serve")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // depot-render reads UTF-8, and a byte-order mark would spoil the first request's JSON.
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
        };
        Process started = new() { StartInfo = start };
        // What depot-render says on standard error explains a failure, so the last few lines are kept.
        started.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;
            lock (errorLines)
            {
                errorLines.Enqueue(e.Data);
                while (errorLines.Count > RememberedErrorLines)
                    errorLines.Dequeue();
            }
        };
        try
        {
            started.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            started.Dispose();
            throw new RendererException(Words.Get(TextKeys.RendererCantStart, Executable, exception.Message), exception);
        }

        started.BeginErrorReadLine();
        process = started;
        session = new RenderSession(started.StandardInput, started.StandardOutput, Timeout);
        return session;
    }

    // Ends depot-render, and returns what it last said on standard error, if anything.
    private string? Stop()
    {
        session = null;
        if (process is not { } running)
            return null;
        process = null;
        try
        {
            if (!running.HasExited)
            {
                running.StandardInput.Close();
                // A moment to finish on its own after its input ends; otherwise it's ended.
                if (!running.WaitForExit(TimeSpan.FromSeconds(1)))
                    running.Kill(entireProcessTree: true);
            }

            running.WaitForExit();
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or IOException)
        {
            // It's already gone; there's nothing left to stop.
        }
        finally
        {
            running.Dispose();
        }

        lock (errorLines)
        {
            var said = errorLines.Count == 0 ? null : string.Join(" ", errorLines);
            errorLines.Clear();
            return said;
        }
    }
}
