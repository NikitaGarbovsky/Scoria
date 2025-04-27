using System;
using Avalonia;
using Avalonia.Controls;
using Markdig;

namespace Scoria
{
    public partial class MainWindow : Window
    {
        private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public MainWindow()
        {
            InitializeComponent();

            // Re-render the preview whenever the text changes
            Editor.GetObservable(TextBox.TextProperty)
                .Subscribe(_ => UpdatePreview());

            // Initial render
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var markdown = Editor.Text ?? string.Empty;
            // Convert to HTML
            var html = Markdown.ToHtml(markdown, _pipeline);
            Preview.Text = html;
        }
    }
}