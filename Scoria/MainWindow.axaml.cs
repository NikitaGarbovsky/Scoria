using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Markdown.Avalonia.Controls;  
using Markdig;
using ReactiveUI;

// MainWindow Code-behind:
// - Watches for changes in the Editor TextBox and updates the Preview live.
// - Supports toggling between Edit/Preview with Ctrl+E shortcut.
// - Uses Markdown.Avalonia.Tight to render Markdown.

namespace Scoria
{
    public partial class MainWindow : Window
    {
        private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        
        private bool _isInPreviewMode = false;
        
        public MainWindow()
        {
            InitializeComponent();

            // Start in editor mode, span full width
            _isInPreviewMode = false;
            Grid.SetColumnSpan(Editor, 2);
            Editor.IsVisible = true;
            Preview.IsVisible = false;
            
            // Re-render the preview whenever the text changes
            Editor.GetObservable(TextBox.TextProperty)
                .Subscribe(_ => UpdatePreview());

            // Setup Ctrl+E for toggling editor/preview
            this.KeyBindings.Add(new KeyBinding 
            {
                Gesture = new KeyGesture(Key.E, KeyModifiers.Control),
                Command = ReactiveCommand.Create(TogglePreviewMode)
            });
            
            // Initial render
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var markdownText = Editor.Text ?? string.Empty;
            // Convert and set rendered Markdown
            Preview.Markdown = markdownText;
        }
        
        private void TogglePreviewMode()
        {
            _isInPreviewMode = !_isInPreviewMode;

            if (_isInPreviewMode)
            {
                // Switch to preview: hide editor, show preview full-width
                Grid.SetColumnSpan(Preview, 2);
                Grid.SetColumn(Preview, 0);
                Editor.IsVisible = false;
                Preview.IsVisible = true;
            }
            else
            {
                // Switch to editor: hide preview, show editor full-width
                Grid.SetColumnSpan(Editor, 2);
                Grid.SetColumn(Editor, 0);
                Preview.IsVisible = false;
                Editor.IsVisible = true;
            }
        }
    }
}