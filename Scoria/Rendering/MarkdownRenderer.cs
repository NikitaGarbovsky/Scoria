using System;
using System.IO;
using System.Linq;
using Markdig;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Scoria.Controls;
using Scoria.Models;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Input;  
using Avalonia.Controls.Primitives;
using Scoria.Services;
 
namespace Scoria.Rendering
{
    /// <summary>
    /// Converts raw Markdown text into a hierarchy of Avalonia visual controls
    /// (<see cref="TextBlock"/>, <see cref="CheckBox"/>, etc.) so that it can
    /// be embedded inside the preview pane.
    /// <para/>
    /// <b>Key features</b>
    /// <list type="bullet">
    ///   <item>Uses <see cref="Markdig"/> for parsing and normalization.</item>
    ///   <item>Supports GitHub-style task-list checkboxes
    ///         (<c>- [ ]</c> &amp; <c>- [x]</c>).</item>
    ///   <item>Skips link-reference definition blocks (they are invisible).</item>
    ///   <item>Delegates a clicked checkbox back to the caller via
    ///         <c>Action&lt;int,bool&gt;</c> giving the zero-based line index.</item>
    /// </list>
    /// </summary>
    public class MarkdownRenderer
    {
        // Pastel palette for badges TODO move this to some centralized color styling class in the future.
        private static readonly Color[] _pastels =
        {
            Color.Parse("#FFD6D6"),
            Color.Parse("#D6FFE2"),
            Color.Parse("#D6E6FF"),
            Color.Parse("#FFF5D6"),
            Color.Parse("#F2D6FF"),
        };
        private static SolidColorBrush PickBrush(string _key)
        {
            unchecked
            {
                int hash = 17;
                foreach (var ch in _key) hash = hash * 31 + ch;
                return new SolidColorBrush(_pastels[Math.Abs(hash) % _pastels.Length]);
            }
        }
        private static readonly Regex wiki =
            new(@"\[\[(?<slug>[^\]\|]+)(\|(?<alias>[^\]]+))?\]\]",
                RegexOptions.Compiled);
        
        /*──────────────────────────── 1. Pipeline set-up ───────────────────────────*/
        private readonly MarkdownPipeline pipeline;

        /// <summary>Creates a pipeline with most “advanced” extensions and task lists.</summary>
        public MarkdownRenderer()
        {
            pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseTaskLists()
                .UseYamlFrontMatter()
                .Build();
        }
        
        /// <summary>
        /// Parse the raw markdown, optionally enrich it with <paramref name="_metadata"/>,
        /// and build an Avalonia visual tree representing the preview.
        /// </summary>
        /// <param name="_markdown">
        /// Raw Markdown text, which may include a YAML front-matter fence.
        /// </param>
        /// <param name="_metadata">
        /// Front-matter that has already been extracted from the same markdown.
        /// When supplied, the renderer adds coloured “badge” pills for
        /// <c>date</c>, <c>tags</c>, and <c>aliases</c> above the document body.
        /// Pass <see langword="null" /> to skip badge rendering.
        /// </param>
        /// <param name="_onTaskToggled">
        /// Optional callback fired whenever a task-list checkbox is toggled in the
        /// preview.  The arguments are the zero-based source line index and the new
        /// checked state.  Use <see langword="null" /> if no editing is needed.
        /// </param>
        /// <remarks>
        /// The returned root is a vertical <see cref="StackPanel"/>—no internal
        /// <see cref="ScrollViewer"/>—so the host XAML controls scrolling and layout.
        /// If <paramref name="_metadata"/> is provided, the original YAML fence is
        /// suppressed and replaced by the badge row.
        /// </remarks>
        public Control Render(string _markdown,
            NoteMetadata? _metadata,
            Action<int, bool>? _onTaskToggled = null, 
            Action<string>?      _onWikiLinkClick = null)
        {
            var rootPanel = new StackPanel();          // vertical

            /* 1️⃣  Render badges if metadata present */
            if (_metadata is not null &&
                (_metadata.Tags.Count  + _metadata.Aliases.Count) > 0)
            {
                var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                
                // DATE first — only if present
                if (_metadata.Date is { } d)
                    badgeRow.Children.Add(new TagBadge
                    {
                    Text            = d.ToString("dd-MMM-yy"),
                    BadgeBackground = PickBrush("date")
                    });
                
                // Tags first
                foreach (var tag in _metadata.Tags)
                    badgeRow.Children.Add(new TagBadge
                    {
                        Text            = tag,
                        BadgeBackground = PickBrush("tag:" + tag)
                    });

                // Aliases
                foreach (var ali in _metadata.Aliases)
                    badgeRow.Children.Add(new TagBadge
                    {
                        Text            = ali,
                        BadgeBackground = PickBrush("alias:" + ali)
                    });

                rootPanel.Children.Add(badgeRow);
            }

            /* 2️⃣  Normal markdown rendering (skip YAML block) */
            var doc = Markdig.Markdown.Parse(_markdown, pipeline);      // _pipeline: existing field
            foreach (var block in doc)
            {
                if (block is YamlFrontMatterBlock) continue;    // hide the raw YAML
                rootPanel.Children.Add(RenderBlock(block, _markdown, _onTaskToggled, _onWikiLinkClick)); 
            }

            return rootPanel;
        }
        /*──────────────────────────── 3. Block rendering ──────────────────────────*/
        
        private Control RenderBlock(
            Block _block, 
            string _markdown,
            Action<int,bool> _onTaskToggled,
            Action<string>?     _onWikiLinkClick = null)
        {
            switch (_block)
            {
                /*──────── Horizontal rule (---) ───────*/
                case ThematicBreakBlock _:
                    return new Rectangle
                    {
                        Height = 1,
                        Fill   = Brushes.Gray,
                        Margin = new Thickness(0, 12)
                    };

                // #, ##, ### Headings ──────────────────────────────
                case HeadingBlock hb:
                    var header = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight   = FontWeight.Bold,
                        Margin       = new Thickness(0, 4),
                        FontSize     = Math.Max(12, 32 - hb.Level * 4),
                    };
                    header.Text = string.Concat(hb.Inline.Select(i => i.ToString()));
                    return header;

                 // ─── a list (ordered or bullet) ──────
                case ListBlock lb:
                    var listPanel = new StackPanel
                    {
                        Spacing = 4,
                        Margin  = new Thickness(0, 2)
                    };
                    // preserve numbering for ordered lists
                    int counter = lb.IsOrdered ? (int.TryParse(lb.OrderedStart, out var s) ? s : 1) : 0;

                    // render each item
                    foreach (var item in lb.OfType<ListItemBlock>())
                        listPanel.Children.Add(
                            RenderListItem(item, lb.IsOrdered, counter++, _markdown, _onTaskToggled, _onWikiLinkClick));

                    return listPanel;
                
                // ¶ Paragraph ──────────────────────────────────────
                case ParagraphBlock pb:
                {
                    var paragraph = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Margin       = new Thickness(0, 2)
                    };

                    string raw = string.Concat(pb.Inline.Select(i => i.ToString()));
                    int pos = 0;

                    foreach (Match m in wiki.Matches(raw))
                    {
                        // plain text before the link
                        if (m.Index > pos)
                            paragraph.Inlines.Add(new Run { Text = raw.Substring(pos, m.Index - pos) });

                        // ----- wiki-link itself -----
                        var slug  = m.Groups["slug"].Value;
                        var alias = m.Groups["alias"].Success
                            ? m.Groups["alias"].Value
                            : System.IO.Path.GetFileName(slug);   // strip folders
                        
                        var slugKey = System.IO.Path.GetFileNameWithoutExtension(slug);
                        
                        // blue underlined text styled like a link
                        var linkBtn = new Button
                        {
                            Content         = alias,                     // just the text string
                            Background       = Brushes.Transparent,
                            BorderThickness  = new Thickness(0),
                            Padding          = new Thickness(0),
                            Cursor           = new Cursor(StandardCursorType.Hand),
                            Foreground       = Brushes.DodgerBlue,
                        };
                        
                        // 1). click → navigate
                        if (_onWikiLinkClick is not null)
                            linkBtn.Click += (_, __) => _onWikiLinkClick(slugKey);
                        
                        /* helper variables captured by the closures */
                        bool linkHovered  = false;
                        bool popupHovered = false;
                        
                        // 2). hover → preview popup
                        linkBtn.PointerEntered += (_, __) =>
                        {
                            EnsurePopup().IsOpen = true;    // open immediately
                            linkHovered = true;
                        };

                        linkBtn.PointerExited += (_, __) =>
                        {
                            linkHovered = false;
                            CloseIfBothLeft();
                        };

                        Popup EnsurePopup()
                        {
                            if (linkBtn.Tag is Popup p) return p;

                            var pop = CreatePreviewPopup(slugKey, _onTaskToggled);
                            pop.PlacementTarget       = linkBtn;
                            pop.PlacementMode         = PlacementMode.Bottom;
                            pop.IsLightDismissEnabled = false;   // we manage close ourselves

                            pop.PointerEntered += (_, __) =>
                            {
                                popupHovered = true;
                            };
                            pop.PointerExited += (_, __) =>
                            {
                                popupHovered = false;
                                CloseIfBothLeft();
                            };

                            linkBtn.Tag = pop;
                            return pop;
                        }

                        void CloseIfBothLeft()
                        {
                            if (!linkHovered && !popupHovered && linkBtn.Tag is Popup pp)
                                pp.IsOpen = false;
                        }

                        // wrap the Button in an inline container so the TextBlock can host it
                        var inlineHost = new InlineUIContainer { Child = linkBtn };
                        
                        paragraph.Inlines.Add(inlineHost);
                        pos = m.Index + m.Length;
                    }

                    // tail plain text
                    if (pos < raw.Length)
                        paragraph.Inlines.Add(new Run { Text = raw.Substring(pos) });

                    return paragraph;
                }


                /*──────── Anything else → invisible spacer ─────*/
                default:
                    // return an empty spacer so we don't see block.ToString()
                    return new StackPanel { Margin = new Thickness(0) };
            }
            
        }
        
        /*──────────────────────────── 4. List-item rendering ──────────────────────*/
        
        private Control RenderListItem(
            ListItemBlock _li,
            bool           _isOrdered,
            int            _number,
            string          _markdown,
            Action<int,bool> _onTaskToggled,
            Action<string>?     _onWikiLinkClick = null)
        {
            // container for marker + potential nested lists
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing     = 2
            };
            
            // find the paragraph for this list item
            var firstPara = _li.Descendants<ParagraphBlock>().FirstOrDefault();
            var task = firstPara?
                .Inline
                .Descendants<TaskList>()
                .FirstOrDefault();

            if(task != null) // ───── Task-list checkbox ─────
            {
                // capture the exact offset
                var offset = _li.Span.Start;
                // compute zero-based line index
                var lineIndex = _markdown
                    .Substring(0, Math.Min(offset, _markdown.Length))
                    .Count(c => c == '\n');
                
                // build the label text (everything after the [ ] or [x])
                var labelText = string.Concat(
                    firstPara.Inline
                        .SkipWhile(i => !(i is TaskList))
                        .Skip(1)
                        .Select(i => i.ToString())
                ).Trim();

                var cb = new CheckBox
                {
                    IsChecked = task.Checked,
                    Content   = labelText,
                    Margin    = new Thickness(0, 2),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                
                // capture the exact zero-based source line of this ListItemBlock
                cb.Checked   += (_,__) => _onTaskToggled(lineIndex, true);
                cb.Unchecked += (_,__) => _onTaskToggled(lineIndex, false);

                container.Children.Add(cb);
            }
            else // ───── Plain bullet / number ─────
            {
                // plain bullet or number
                var bullet = _isOrdered ? $"{_number}. " : "• ";
                container.Children.Add(new TextBlock
                {
                    Text         = bullet + (
                        firstPara?
                            .Inline
                            .FirstOrDefault()?
                            .ToString()
                        ?? ""
                    ),
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 2)
                });
            }

            /*— Nested lists: indent by 20 px and recurse —*/
            foreach (var nested in _li.OfType<ListBlock>())
            {
                var nestedControl = RenderBlock(nested, _markdown, _onTaskToggled, _onWikiLinkClick);
                nestedControl.Margin = new Thickness(20, 0, 0, 0);
                container.Children.Add(nestedControl);
            }

            return container;
        }
        /// <summary>
        /// Builds (or falls back to) a preview-popup for a wiki-link.
        /// <para>
        /// • If <paramref name="_slug"/> resolves to an existing note, the popup shows a
        ///   mini preview (400 × 300, scrollable, themed border).  
        /// • If it does **not** resolve, the popup shows a minimal red “Note not found”
        ///   message so broken links are obvious.
        /// </para>
        /// <para>
        /// The method is recursive-safe: when it renders the preview it passes the same
        /// <paramref name="_onTaskToggled"/> and <paramref name="_onWikiLinkClick"/>
        /// callbacks, but further wiki-links inside the preview will not open additional
        /// pop-ups; they are rendered with the same rules.
        /// </para>
        /// </summary>
        /// <param name="_slug">Basename of the note (case-insensitive, no “.md”).</param>
        /// <param name="_onTaskToggled">
        /// Callback for task-list checkboxes.  Passed through to <see cref="Render"/> so
        /// toggling a checkbox inside the popup updates the source note.
        /// </param>
        /// <param name="_onWikiLinkClick">
        /// (Optional) Navigation callback used inside the popup so that clicking a
        /// wiki-link inside the preview still navigates the main view.
        /// </param>
        /// <returns>
        /// A fully configured <see cref="Popup"/>. It is returned **closed**
        /// (<c>IsOpen = false</c>).  The caller decides when to open/close.
        /// </returns>
        private Popup CreatePreviewPopup(
            string _slug, 
            Action<int, bool>? _onTaskToggled,
            Action<string>?     _onWikiLinkClick = null)
        {
            /*────────────────── 1. Try resolve the slug ──────────────────*/
            var target = NoteLinkIndex.Resolve(_slug);
            
            /*--------------------------------------------------------------
             * 1-A. Missing note → tiny red popup saying “Note not found”.
             *-------------------------------------------------------------*/
            if (target is null)
            {
                // red “missing” popup
                return new Popup
                {
                    Child = new Border
                    {
                        Background = Brushes.MistyRose, // pale red
                        Padding     = new Thickness(12),
                        Child       = new TextBlock
                        {
                            Text       = "Note not found",
                            Foreground = Brushes.DarkRed
                        }
                    },
                    // Let the caller or pointer-leave close it automatically
                    IsLightDismissEnabled = true   // auto-close on outside click
                };
            }

            /*────────────────── 2. Load and render the note ──────────────*/
            var md     = File.ReadAllText(target.Path); // raw markdown
            var meta   = MetadataParser.Extract(md); // YAML front-matter (if any)
            
            // Recursively call Render to build a mini preview; pass through callbacks
            var preview = Render(md, meta, _onTaskToggled,_onWikiLinkClick);   

            /*────────────────── 3. Wrap in a styled popup ────────────────*/
            return new Popup
            {
                Child = new Border
                {
                    Background   = Brushes.White,
                    CornerRadius = new CornerRadius(8),
                    Padding      = new Thickness(12),
                    Width        = 400,
                    Height       = 300,
                    Child        = new ScrollViewer { Content = preview }
                },
                // Caller controls placement; we handle dismissal on outside click
                IsLightDismissEnabled = true
            };
        }

   }
}