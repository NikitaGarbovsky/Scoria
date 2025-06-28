using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Scoria.Controls;

/// <summary>
/// Visual “pill” used by the Markdown preview to display a single
/// <c>date / tag / alias</c> badge, TODO, more will be added.
///
/// <para>
/// The control is intentionally tiny: it exposes two
/// <see cref="StyledProperty{T}">styled properties</see>—
/// <see cref="Text"/> and <see cref="BadgeBackground"/>—
/// so callers can data-bind or set them in code, and the XAML
/// template takes care of the layout.
/// </para>
/// </summary>
public partial class TagBadge : UserControl
{
    /// <summary>Initialises the control and loads its XAML.</summary>
    public TagBadge() => AvaloniaXamlLoader.Load(this);

    // --------------------------------------------------------------------- //
    //  Public styled properties                                             //
    // --------------------------------------------------------------------- //
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<TagBadge, string>(nameof(Text));

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The pastel background brush for the pill.
    /// Set by <c>MarkdownRenderer</c> via a deterministic colour-picking
    /// helper so the same tag always gets the same tint.
    /// </summary>
    public static readonly StyledProperty<IBrush> BadgeBackgroundProperty =
        AvaloniaProperty.Register<TagBadge, IBrush>(nameof(BadgeBackground));

    /// <inheritdoc cref="BadgeBackgroundProperty"/>
    public IBrush BadgeBackground
    {
        get => GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }
}