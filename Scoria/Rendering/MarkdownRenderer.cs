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
    public class MarkdownRenderer
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownRenderer()
        {
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseTaskLists()
                .Build();
        }
        /// <summary>
        /// Renders the markdown into Avalonia controls.
        /// onTaskToggled is called with (zero-based lineIndex, newCheckedState)
        /// whenever the user clicks a checkbox.
        /// </summary>
        public Control Render(string markdown, Action<int,bool> onTaskToggled)
        {
            // 1) parse into AST once
            var document = Markdig.Markdown.Parse(markdown, _pipeline);
            
            // 2) walk blocks
            var panel    = new StackPanel { Spacing = 8 };
            foreach (var block in document)
            {
                // skip link-reference definitions entirely
                if (block is LinkReferenceDefinitionGroup) 
                    continue;

                panel.Children.Add(RenderBlock(block, markdown, onTaskToggled));
            }

            return panel;
        }

        private Control RenderBlock(
            Block block, 
            string markdown,
            Action<int,bool> onTaskToggled)
        {
            switch (block)
            {
                // ─── Horizontal rule ─────────────────────────────
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
                            RenderListItem(item, lb.IsOrdered, counter++, markdown, onTaskToggled));

                    return listPanel;

                // ─── Fallback ─────────────────────────────────────
                default:
                    // return an empty spacer so we don't see block.ToString()
                    return new StackPanel { Margin = new Thickness(0) };
            }
            
        }
        private Control RenderListItem(
            ListItemBlock li,
            bool           isOrdered,
            int            number,
            string          markdown,
            Action<int,bool> onTaskToggled)
        {
            // container for marker + potential nested lists
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing     = 2
            };
            
            // find the paragraph for this list item
            var firstPara = li.Descendants<ParagraphBlock>().FirstOrDefault();
            var task = firstPara?
                .Inline
                .Descendants<TaskList>()
                .FirstOrDefault();

            if(task != null)
            {
                // capture the exact offset
                var offset = li.Span.Start;
                // compute zero-based line index
                var lineIndex = markdown
                    .Substring(0, Math.Min(offset, markdown.Length))
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
                cb.Checked   += (_,__) => onTaskToggled(lineIndex, true);
                cb.Unchecked += (_,__) => onTaskToggled(lineIndex, false);

                container.Children.Add(cb);
            }
            else
            {
                // plain bullet or number
                var bullet = isOrdered ? $"{number}. " : "• ";
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

            // now handle **nested** lists (indent by 20px)
            foreach (var nested in li.OfType<ListBlock>())
            {
                var nestedControl = RenderBlock(nested, markdown, onTaskToggled);
                nestedControl.Margin = new Thickness(20, 0, 0, 0);
                container.Children.Add(nestedControl);
            }

            return container;
        }
   }
}