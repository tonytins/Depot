namespace Depot.Rendering;

/// <summary>Finds depot-render.</summary>
public static class RendererLocation
{
    /// <summary>The environment variable that names depot-render, ahead of anywhere Depot looks.</summary>
    public const string Variable = "DEPOT_RENDER";

    /// <summary>The program's file name on this system.</summary>
    public static string FileName => OperatingSystem.IsWindows() ? "depot-render.exe" : "depot-render";

    // How far up from Depot's own folder a development build is looked for: src/Depot/bin/Debug/net10.0 is five.
    private const int FoldersUp = 8;

    /// <summary>
    /// Where depot-render is: <see cref="Variable"/> when it's set; beside Depot, as a package puts it; a build in a
    /// Depot checkout Depot runs from, the newer of its release and debug builds; or on the <c>PATH</c>.
    /// </summary>
    /// <returns>The path, or <see langword="null"/> when it's nowhere.</returns>
    public static string? Find(string? named, string baseDirectory, string? searchPath)
    {
        if (!string.IsNullOrWhiteSpace(named))
            return named;

        var beside = Path.Combine(baseDirectory, FileName);
        if (File.Exists(beside))
            return beside;

        var folder = new DirectoryInfo(baseDirectory);
        for (var up = 0; folder is not null && up < FoldersUp; up++, folder = folder.Parent)
        {
            var built = new[] { "release", "debug" }
                .Select(configuration => new FileInfo(Path.Combine(folder.FullName, "depot-render", ".build", configuration, FileName)))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (built is not null)
                return built.FullName;
        }

        foreach (var entry in (searchPath ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var onPath = Path.Combine(entry, FileName);
            if (File.Exists(onPath))
                return onPath;
        }

        return null;
    }

    /// <summary>Where depot-render is for this process, from its environment.</summary>
    public static string? Find() =>
        Find(Environment.GetEnvironmentVariable(Variable), AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"));
}
