using System;
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
            Action<int, bool>? _onTaskToggled = null)
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
                rootPanel.Children.Add(RenderBlock(block, _markdown, _onTaskToggled)); 
            }

            return rootPanel;
        }
        /*──────────────────────────── 3. Block rendering ──────────────────────────*/
        
        private Control RenderBlock(
            Block _block, 
            string _markdown,
            Action<int,bool> _onTaskToggled)
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

                // ¶ Paragraph ──────────────────────────────────────
                case ParagraphBlock pb:
                    var paragraphText = string.Concat(pb.Inline.Select(i => i.ToString()));
                    return new TextBlock
                    {
                        Text         = paragraphText,
                        TextWrapping = TextWrapping.Wrap,
                        Margin       = new Thickness(0,2)
                    };

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
                            RenderListItem(item, lb.IsOrdered, counter++, _markdown, _onTaskToggled));

                    return listPanel;

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
            Action<int,bool> _onTaskToggled)
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
                var nestedControl = RenderBlock(nested, _markdown, _onTaskToggled);
                nestedControl.Margin = new Thickness(20, 0, 0, 0);
                container.Children.Add(nestedControl);
            }

            return container;
        }
   }
}