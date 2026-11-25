using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Depot.Designer;
using Depot.Interface;
using Depot.Rendering;

namespace Depot.Tests;

/// <summary>
/// A stand-in for depot-render that needs no display: it reads the document as Depot does, places each control and
/// visual where its attributes say, below a 30-pixel title bar for a window, and writes a blank picture of the
/// size. Answers come back at once, so a test's designer edits are drawn before the next line.
/// </summary>
public sealed class FakeRenderer
{
    private const float TitleHeight = 30;

    /// <summary>Every request it was asked to draw, in order.</summary>
    public List<RenderRequest> Requests { get; } = [];

    public Task<RenderReply> RenderAsync(RenderRequest request)
    {
        Requests.Add(request);
        var document = UIDocument.Read(request.Document ?? "");
        if (document.Problem is { } problem)
            return Task.FromResult(new RenderReply(null, $"Hudini can't read the document: {problem.Message}"));

        var xml = document.Xml!;
        var root = xml.Children.First(child => child.Name is "Window" or "Panel");
        var isWindow = root.Name == "Window";
        var top = isWindow ? TitleHeight : 0;
        var width = Number(root["width"]) ?? 1;
        var height = Number(root["height"]) ?? 1;

        List<FrameElement> elements = [];
        foreach (var visuals in (bool[])[true, false])
        {
            foreach (var child in xml.Children)
            {
                if ((child.Name == "Visual") != visuals || !FrameEdit.IsFramed(child.Name) || child["name"] is not { } name)
                    continue;
                elements.Add(new FrameElement(name, child.Name,
                    new FrameRect(Number(child["x"]) ?? 0, (Number(child["y"]) ?? 0) + top, Number(child["width"]) ?? 24, Number(child["height"]) ?? 24),
                    Hidden: false));
            }
        }

        using (WriteableBitmap picture = new(new PixelSize((int)width, (int)(height + top)), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul))
            picture.Save(request.Output, new PngBitmapEncoderOptions());

        FrameMap frames = new(isWindow ? "window" : "panel", width, height + top, new FrameRect(0, top, width, height), elements, [], [], [], []);
        return Task.FromResult(new RenderReply(frames, null));
    }

    private static float? Number(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
