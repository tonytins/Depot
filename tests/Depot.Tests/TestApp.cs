using System.Diagnostics;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Depot.Tests;

/// <summary>
/// Depot's app, off-screen: rendered with Skia, so tests can look at real frames. Avalonia starts once a process, so
/// every test shares one session and hands it its work.
/// </summary>
public static class TestApp
{
    // One application for the whole run. The default, a new one for each test, races when xUnit runs test classes
    // side by side: one test's setup can find the render loop another test's thread made.
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.StartNew(typeof(TestApp), AvaloniaTestIsolationLevel.PerAssembly);

    /// <summary>Avalonia finds the app here, as it does in Program.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Runs work on Avalonia's thread and waits for it.</summary>
    public static void Run(Action work) => Session.Dispatch(work, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Lets the window's thread run, timers and all, until <paramref name="done"/> holds, for work that waits on a
    /// pause in typing or on a process.
    /// </summary>
    public static void RunUntil(Func<bool> done, TimeSpan limit)
    {
        var clock = Stopwatch.StartNew();
        DispatcherFrame frame = new();
        DispatcherTimer poll = new(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background, (_, _) =>
        {
            if (done() || clock.Elapsed > limit)
                frame.Continue = false;
        });
        poll.Start();
        try
        {
            if (!done())
                Dispatcher.UIThread.PushFrame(frame);
        }
        finally
        {
            poll.Stop();
        }

        if (!done())
            throw new TimeoutException($"Nothing happened within {limit.TotalSeconds} seconds.");
    }
}

/// <summary>A folder of a test's own, removed after it.</summary>
public sealed class TemporaryFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"depot-tests-{Guid.NewGuid():N}");

    public TemporaryFolder() => Directory.CreateDirectory(Path);

    public string File(string name, string text)
    {
        var path = System.IO.Path.Combine(Path, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
