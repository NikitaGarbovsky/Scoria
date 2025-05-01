using System;
using System.Linq;
using Markdig;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

        private Control RenderBlock(Block block, Action<ListItemBlock, bool> onTaskToggled)
        {
            switch (block)
            {
                case HeadingBlock hb:
                    return new TextBlock
                    {
                        Text = hb.Inline?.FirstChild?.ToString() ?? "",
                        FontSize = 24 - hb.Level*2,
                        FontWeight = FontWeight.Bold
                    };

                case ParagraphBlock pb:
                    var inlines = new Span();
                    foreach (var inline in pb.Inline)
                        inlines.Inlines.Add(new Run(inline.ToString()));
                    return new TextBlock { Inlines = [inlines] };

                case ListItemBlock li:
                    // find the paragraph inside the list item
                    if (li.LastChild is ParagraphBlock paragraphBlock)
                    {
                        // detect the TaskList inline marker
                        var taskMarker = paragraphBlock.Inline.Descendants<TaskList>().FirstOrDefault();
                        if (taskMarker != null)
                        {
                            // everything *after* the marker → the checkbox label
                            var labelText = string.Concat(
                                paragraphBlock.Inline
                                    .SkipWhile(inl => inl != taskMarker) // skip upto marker
                                    .Skip(1)                              // skip the marker itself
                                    .Select(i => i.ToString())
                            );

                            var cb = new CheckBox
                            {
                                IsChecked = taskMarker.Checked,
                                Content   = labelText.Trim()
                            };

                            // wire toggle back into your VM or renderer callback
                            cb.Checked   += (_,__) => onTaskToggled(li, true);
                            cb.Unchecked += (_,__) => onTaskToggled(li, false);

                            return cb;
                        }
                    }

                    // fallback: normal bullet
                    return new TextBlock
                    {
                        Text = "- " + (li.LastChild as ParagraphBlock)?.Inline?.FirstChild?.ToString() ?? ""
                    };


                // TODO: code fences, blockquotes, tables...
                default:
                    return new TextBlock { Text = block.ToString() };
            }
        }
    }
}
