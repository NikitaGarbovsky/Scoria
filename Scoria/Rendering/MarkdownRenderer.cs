using System;
using System.IO;
using System.Linq;
using Markdig;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Renderers.Normalize;
using Markdig.Syntax.Inlines;

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
        /*──────────────────────────── 1. Pipeline set-up ───────────────────────────*/
        private readonly MarkdownPipeline pipeline;

        /// <summary>Creates a pipeline with most “advanced” extensions and task lists.</summary>
        public MarkdownRenderer()
        {
            pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseTaskLists()
                .Build();
        }
        
        /// <summary>
        /// Parse <paramref name="_markdown"/>, build an Avalonia visual tree and
        /// return its root container.
        /// </summary>
        /// <param name="_markdown">Raw markdown source.</param>
        /// <param name="_onTaskToggled">
        /// Callback invoked whenever the user clicks a checkbox
        /// – arguments: <c>(lineIndex, newCheckedState)</c>.
        /// </param>
        /// <remarks>
        /// The returned control is a plain <see cref="StackPanel"/> (no internal
        /// <see cref="ScrollViewer"/>).  The caller’s XAML manages scrolling/
        /// measuring so wrapping works.
        /// </remarks>
        public Control Render(string _markdown, Action<int,bool> _onTaskToggled)
        {
            /* 1) Parse into an abstract syntax tree (AST). */
            var document = Markdig.Markdown.Parse(_markdown, pipeline);
            
            /* 2) Walk all top-level blocks and convert each one. */
            var panel    = new StackPanel { Spacing = 8 };
            foreach (var block in document)
            {
                // Link-reference definitions are purely meta – skip them.
                if (block is LinkReferenceDefinitionGroup) 
                    continue;

                panel.Children.Add(RenderBlock(block, _markdown, _onTaskToggled));
            }

            return panel; // caller will wrap in a ScrollViewer
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