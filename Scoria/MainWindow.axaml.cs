using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        /// <summary>
        /// Renders the current markdown text in the Preview pane.
        /// </summary>
        private void UpdatePreview()
        {
            var markdownText = Editor.Text ?? string.Empty;
            // Convert and set rendered Markdown
            Preview.Markdown = markdownText;
        }
        
        /// <summary>
        /// Allows dragging the window when the user clicks the transparent title area.
        /// </summary>
        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
        private void TogglePreviewMode()
        {
            _isInPreviewMode = !_isInPreviewMode;

            if (_isInPreviewMode)
            {
                // Switch to preview: hide editor, show preview full-width
                Grid.SetColumnSpan(Preview, 1);
                Grid.SetColumn(Preview, 2);
                Editor.IsVisible = false;
                Preview.IsVisible = true;
            }
            else
            {
                // Switch to editor: hide preview, show editor full-width
                Grid.SetColumnSpan(Editor, 1);
                Grid.SetColumn(Editor, 1);
                Preview.IsVisible = false;
                Editor.IsVisible = true;
            }
        }
        
        /* --------------- Opening a folder containing markdown files, displaying it through a treeview --------------- */
        
        /// <summary>
        /// Handler for toolbar "📂 Open Folder" button.
        /// Shows a folder picker and loads all .md files into the TreeView.
        /// </summary>
        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select a Folder"
            };

            var path = await dialog.ShowAsync(this);
            if (string.IsNullOrEmpty(path)) return;
            
            LoadFolder(path);
        }
        /// <summary>
        /// Recursively loads all .md files under folderPath
        /// and populates the TreeView. Stores the full path in each item's Tag.
        /// </summary>
        private void LoadFolder(string folderPath)
        {
            // 1) Clear any existing items
            FileExplorer.Items.Clear();
            
            // 2) Create the root node
            var rootItem = new TreeViewItem { Header = Path.GetFileName(folderPath), Tag = folderPath };
            
            // 3) Recursively fill it
            AddChildItems(rootItem, folderPath);
            
            // 4) Add it into the TreeView
            FileExplorer.Items.Add(rootItem);
        }
        /// <summary>
        /// Adds subfolders and markdown files under `currentPath` as TreeViewItems.
        /// </summary>
        private void AddChildItems(TreeViewItem _parent, string _currentPath)
        {
            // Add folders
            foreach (var dir in Directory.GetDirectories(_currentPath).OrderBy(_d => _d))
            {
                var dirItem = new TreeViewItem
                {
                    Header = Path.GetFileName(dir),
                    Tag = dir
                };
                // Recurse
                AddChildItems(dirItem, dir);
                // Add into the parent's existing Items collection
                _parent.Items.Add(dirItem);
            }

            // Add markdown files
            foreach (var file in Directory.GetFiles(_currentPath, "*.md").OrderBy(_f => _f))
            {
                var fileItem = new TreeViewItem
                {
                    Header = Path.GetFileName(file),
                    Tag = file
                };
                _parent.Items.Add(fileItem);
            }
        }
        
        /// <summary>
        /// When the user double‐clicks an entry in the TreeView,
        /// if it’s a file (Tag is a .md path), load it into the editor.
        /// </summary>
        private void FileExplorer_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (FileExplorer.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is string filePath)
            {
                Editor.Text = File.ReadAllText(filePath);
            }
        }

        /* --------------- Opening a folder containing markdown files, displaying it through a treeview --------------- */
    }
}