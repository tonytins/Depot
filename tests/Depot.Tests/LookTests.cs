using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Depot.Tests;

/// <summary>
/// Depot's look, as App.axaml defines it: a value for every role of the Venture Design Language on both palettes,
/// text that reads on every surface it's drawn over, and Fluent's palette agreeing with the roles, so Fluent's
/// controls and Depot's own drawing don't drift apart.
/// </summary>
public sealed class LookTests
{
    public static TheoryData<string> Palettes => ["Dark", "Light"];

    // Foundations ▸ Color roles, and the ones Depot adds for its own surfaces and states.
    private static readonly string[] ColorRoles =
    [
        "text", "secondaryText", "disabledText", "highlightedText", "accent", "panel", "inputBackground", "border",
        "selection", "headerBar", "canvasBackground", "errorText", "hover", "pressed",
    ];

    private static readonly string[] TypeRoles = ["captionSize", "bodySize", "titleSize", "largeTitleSize"];

    // What Depot draws text over: the window, fields and lists, the bars, and behind the canvas.
    private static readonly string[] Surfaces = ["panel", "inputBackground", "headerBar", "canvasBackground"];

    private static ThemeVariant Variant(string palette) => palette == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

    private static Color Role(string key, string palette)
    {
        Assert.True(Application.Current!.TryGetResource(key, Variant(palette), out var value), $"{key} isn't defined on {palette}.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryRoleHasAValue(string palette) => TestApp.Run(() =>
    {
        foreach (var role in ColorRoles)
            Role(role, palette);
        foreach (var role in TypeRoles)
        {
            Assert.True(Application.Current!.TryGetResource(role, Variant(palette), out var size), $"{role} isn't defined.");
            Assert.IsType<double>(size);
        }
    });

    [Theory]
    [MemberData(nameof(Palettes))]
    public void TextReadsOnEverySurface(string palette) => TestApp.Run(() =>
    {
        foreach (var surface in Surfaces)
        {
            var behind = Role(surface, palette);
            // The Design Language asks 3 to 1; Depot's small text, its status, hints, and errors, holds to 4.5.
            foreach (var text in new[] { "text", "secondaryText", "errorText" })
                AssertContrast(text, surface, Over(Role(text, palette), behind), behind, 4.5);
            // The accent marks focus and selection, a part of the interface.
            AssertContrast("accent", surface, Role("accent", palette), behind, 3);
        }

        var selection = Role("selection", palette);
        AssertContrast("text", "selection", Role("text", palette), selection, 4.5);
        AssertContrast("secondaryText", "selection", Over(Role("secondaryText", palette), selection), selection, 3);
        var accent = Role("accent", palette);
        AssertContrast("highlightedText", "accent", Role("highlightedText", palette), accent, 4.5);
    });

    [Theory]
    [MemberData(nameof(Palettes))]
    public void FluentsPaletteAgreesWithTheRoles(string palette) => TestApp.Run(() =>
    {
        var fluent = Application.Current!.Styles.OfType<FluentTheme>().Single().Palettes[Variant(palette)];
        Assert.Equal(Role("accent", palette), fluent.Accent);
        Assert.Equal(Role("panel", palette), fluent.RegionColor);
        Assert.Equal(Role("inputBackground", palette), fluent.AltMedium);
        Assert.Equal(Role("text", palette), fluent.BaseHigh);
        Assert.Equal(Role("headerBar", palette), fluent.ChromeMediumLow);
        Assert.Equal(Role("errorText", palette), fluent.ErrorText);
    });

    private static void AssertContrast(string text, string surface, Color fore, Color back, double least)
    {
        var ratio = Contrast(fore, back);
        Assert.True(ratio >= least, $"{text} on {surface} is {ratio:0.00} to 1, under {least} to 1.");
    }

    /// <summary>A translucent color as it shows over an opaque one.</summary>
    private static Color Over(Color fore, Color back)
    {
        double alpha = fore.A / 255.0;
        byte Mix(byte f, byte b) => (byte)Math.Round(b + (f - b) * alpha);
        return Color.FromRgb(Mix(fore.R, back.R), Mix(fore.G, back.G), Mix(fore.B, back.B));
    }

    /// <summary>WCAG 2's contrast ratio, from 1 to 21.</summary>
    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
