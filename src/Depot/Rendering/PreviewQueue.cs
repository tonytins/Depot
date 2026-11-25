namespace Depot.Rendering;

/// <summary>
/// Keeps the canvas up to date without drawing every keystroke: while one picture is drawn, only the newest request
/// waits, and the ones before it are dropped.
/// </summary>
/// <remarks>Use it from one thread, the window's, which its results come back on.</remarks>
public sealed class PreviewQueue(Func<RenderRequest, Task<RenderReply>> render)
{
    private readonly Func<RenderRequest, Task<RenderReply>> render = render ?? throw new ArgumentNullException(nameof(render));
    private RenderRequest? waiting;
    private bool drawing;

    /// <summary>A request was answered, with a picture or depot-render's reason it couldn't draw.</summary>
    public event Action<RenderRequest, RenderReply>? Answered;

    /// <summary>A request couldn't be answered: depot-render couldn't start, or failed.</summary>
    public event Action<RenderRequest, Exception>? Failed;

    /// <summary>Whether a picture is being drawn.</summary>
    public bool IsDrawing => drawing;

    /// <summary>Asks for a picture, replacing any request still waiting.</summary>
    public void Request(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        waiting = request;
        if (!drawing)
            _ = DrawAsync();
    }

    private async Task DrawAsync()
    {
        drawing = true;
        try
        {
            while (waiting is { } next)
            {
                waiting = null;
                RenderReply reply;
                try
                {
                    reply = await render(next);
                }
                catch (Exception exception)
                {
                    // Whatever went wrong is the canvas's to show; the queue keeps going for the next request.
                    Failed?.Invoke(next, exception);
                    continue;
                }

                Answered?.Invoke(next, reply);
            }
        }
        finally
        {
            drawing = false;
        }
    }
}
