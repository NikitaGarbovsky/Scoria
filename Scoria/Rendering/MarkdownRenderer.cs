using System;
using System.Linq;
using Markdig;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
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

        public Control Render(string markdown, Action<ListItemBlock, bool> onTaskToggled)
        {   
            var document = Markdig.Markdown.Parse(markdown, _pipeline);
            var panel = new StackPanel { Spacing = 8 };

            foreach (var block in document)
                panel.Children.Add(RenderBlock(block, onTaskToggled));

            return new ScrollViewer { Content = panel };
        }

        private Control RenderBlock(Block block, Action<ListItemBlock,bool> onTaskToggled)
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
                        Margin       = new Thickness(0,4),
                    };
                    // concatenate all inline text
                    header.Text = string.Concat(hb.Inline.Select(i => i.ToString()));
                    // size by level
                    header.FontSize = Math.Max(12, 32 - (hb.Level * 4));
                    return header;

                // ¶ Paragraph ──────────────────────────────────────
                case ParagraphBlock pb:
                    var para = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Margin       = new Thickness(0,2)
                    };
                    foreach (var inline in pb.Inline)
                        para.Inlines.Add(new Run(inline.ToString()));
                    return para;

                 // ─── a list (ordered or bullet) ──────
                case ListBlock lb:
                    var listPanel = new StackPanel 
                    {
                        Spacing = 4,
                        Margin  = new Thickness(0,2)
                    };

                    // only for ordered lists: could actually number things
                    int counter = 0;
                    if(lb.IsOrdered && !int.TryParse(lb.OrderedStart, out counter))
                        counter = 1;

                    foreach(var child in lb.OfType<ListItemBlock>())
                        listPanel.Children.Add(RenderListItem(child, lb.IsOrdered, counter++, onTaskToggled));

                    return listPanel;

                // ─── Fallback ─────────────────────────────────────
                default:
                    return new TextBlock
                    {
                        Text          = block.ToString(),
                        TextWrapping  = TextWrapping.Wrap,
                        Margin        = new Thickness(0,2)
                    };
            }
            
        }
        private Control RenderListItem(
            ListItemBlock item,
            bool            isOrdered,
            int             number,
            Action<ListItemBlock,bool> onTaskToggled)
        {
            // Container VStack for this item + any nested lists
            var container = new StackPanel {
                Orientation = Orientation.Vertical,
                Spacing     = 2
            };

            // 1) The “marker” line: checkbox / number / bullet
            Control marker;

            // detect a task‐list in the first paragraph
            var firstPara = item.Descendants<ParagraphBlock>().FirstOrDefault();
            var task = firstPara?
                .Inline
                .Descendants<TaskList>()
                .FirstOrDefault();

            if(task != null)
            {
                // build the label text AFTER the [ ] or [x]
                var text = string.Concat(
                    firstPara.Inline
                        .SkipWhile(i=>!(i is TaskList))
                        .Skip(1)
                        .Select(i=>i.ToString())
                ).TrimStart();

                var label = new TextBlock 
                {
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Text         = text
                };

                var cb = new CheckBox 
                {
                    IsChecked = task.Checked,
                    Content   = label,
                };
                cb.Checked   += (_,__) => onTaskToggled(item, true);
                cb.Unchecked += (_,__) => onTaskToggled(item, false);
                marker = cb;
            }
            else
            {
                // not a task‐list → bullet or number
                var bulletText = isOrdered
                    ? $"{number}. "
                    : "• ";

                marker = new TextBlock 
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0,2),
                    Text         = $"{bulletText}{firstPara?.Inline.FirstOrDefault()?.ToString() ?? ""}"
                };
            }

            container.Children.Add(marker);

            // 2) Handle nested lists: indent them by 20px
            foreach (var nested in item.OfType<ListBlock>())
            {
                var nestedControl = RenderBlock(nested, onTaskToggled);
                // Add left-indent for child lists TODO this only works with a single indentation, if you need more,
                // figure out a recursive system
                nestedControl.Margin = new Thickness(20, 0, 0, 0);
                container.Children.Add(nestedControl);
            }

            return container;
        }
   }
}