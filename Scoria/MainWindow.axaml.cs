using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Markdown.Avalonia.Controls; 
using Avalonia.Threading; // for DispatcherTimer
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
        
        private string? _currentFilePath = null;
        private string  _originalText   = string.Empty;
        /// <summary>
        /// When the window closes, save the markdown file.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            TrySaveIfChanged();
            base.OnClosing(e);
        }
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
            
            // Ctrl+S → Save now
            this.KeyBindings.Add(new KeyBinding 
            {
                Gesture = new KeyGesture(Key.S, KeyModifiers.Control),
                Command = ReactiveCommand.Create(TrySaveIfChanged)
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
                // Exiting edit → preview: save changes
                TrySaveIfChanged();
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
        /// Populate the TreeView *without* a top-level folder node.
        /// Instead, we show the *contents* of that folder as the root items.
        /// </summary>
        private void LoadFolder(string folderPath)
        {
            FileExplorer.Items.Clear();

            // 1) Add all subdirectories (skipping .obsidian)
            foreach (var dir in Directory.GetDirectories(folderPath)
                         .Where(d => Path.GetFileName(d) != ".obsidian")
                         .OrderBy(d => d))
            {
                var dirItem = new TreeViewItem
                {
                    Header = Path.GetFileName(dir),
                    Tag    = dir
                };
                AddChildItems(dirItem, dir);
                FileExplorer.Items.Add(dirItem);
            }

            // 2) Then add all top-level .md files
            foreach (var file in Directory.GetFiles(folderPath, "*.md")
                         .OrderBy(f => f))
            {
                var fileItem = new TreeViewItem
                {
                    Header = Path.GetFileName(file),
                    Tag    = file
                };
                FileExplorer.Items.Add(fileItem);
            }
        }
        /// <summary>
        /// Adds subfolders and markdown files under `currentPath` as TreeViewItems.
        /// </summary>
        private void AddChildItems(TreeViewItem _parent, string _currentPath)
        {
            // 0) Make sure we start with an empty list
            _parent.Items.Clear();
            
            // 1) iterate subfolders (skip .obsidian)
            foreach (var dir in Directory.GetDirectories(_currentPath)
                         .Where(_d => Path.GetFileName(_d) != ".obsidian")
                         .OrderBy(_d => _d))
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

            // 2) iterate .md files
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
        /// Single-click (selection) handler:
        /// - If you clicked a folder, it toggles expansion.
        /// - If you clicked a .md file, it loads it into the editor.
        /// </summary>
        private void FileExplorer_SelectionChanged(object? sender, SelectionChangedEventArgs _selectionChangedEventArgs)
        {
            
            // First, save any dirty edits before switching
            TrySaveIfChanged();
            
            if (FileExplorer.SelectedItem is TreeViewItem item 
                && item.Tag is string path)
            {
                if (Directory.Exists(path))
                {
                    // Toggle expansion on folders
                    item.IsExpanded = !item.IsExpanded;
                }
                else if (File.Exists(path))
                {
                    // Load the new file
                    var text = File.ReadAllText(path);
                    Editor.Text = text;
                    
                    // Cache
                    _currentFilePath = path;
                    _originalText    = text;
                }
            }
        }
        /// <summary>
        /// If the editor text differs from the last loaded text, write to disk,
        /// update the cache, and show a brief "Saved: filename" popup.
        /// </summary>
        private void TrySaveIfChanged()
        {
            if (_currentFilePath == null)
                return;

            var currentText = Editor.Text ?? string.Empty;
            if (currentText != _originalText)
            {
                File.WriteAllText(_currentFilePath, currentText);
                _originalText = currentText;
                ShowSavedPopup(Path.GetFileName(_currentFilePath));
            }
        }

        /// <summary>
        /// Displays a small toast popup in the bottom-right corner for 2 seconds.
        /// </summary>
        private void ShowSavedPopup(string fileName)
        {
            // 1) Create and configure Popup content and configuration
            var border = new Border
            {
                Background   = Brushes.DimGray,
                Opacity      = 1.0,
                CornerRadius = new CornerRadius(4),
                Padding      = new Thickness(8),
                Child        = new TextBlock
                {
                    Text       = $"Saved: {fileName}",
                    Foreground = Brushes.White,
                },
                // 2) Add a fade transition on Opacity
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Border.OpacityProperty,
                        Duration = TimeSpan.FromSeconds(5)
                    }
                }
            };

            // 3) Create the popup
            var popup = new Popup
            {
                PlacementTarget   = this.RootPanel,
                PlacementMode     = PlacementMode.AnchorAndGravity,
        
                // Anchor at the bottom-center of the target
                PlacementAnchor   = PopupAnchor.BottomRight,
                // Gravity = push *down* from that anchor
                PlacementGravity  = PopupGravity.Bottom,

                // nudge it a few pixels so it’s within the application window
                // TODO change this so its dynamic, currently its fixed size, so larger file names would exceed the window.
                HorizontalOffset  = -100,
                VerticalOffset    = -40,

                // light dismiss if you tap anywhere else
                //IsLightDismissEnabled = true,

                Child             = border
            };

            // 4) Display the popup
            RootPanel.Children.Add(popup);
            popup.IsOpen = true;

            // 5) After 2s, kick off the fade, then remove after the transition
            DispatcherTimer fadeTimer = null;
            fadeTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, __) =>
            {
                fadeTimer.Stop();

                // trigger fade-out
                border.Opacity = 0;

                // remove once the 5s transition has finished TODO magic number, also probably want this in a settings window.
                DispatcherTimer removeTimer = null;
                removeTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, __2) =>
                {
                    removeTimer.Stop();
                    popup.IsOpen = false;
                    RootPanel.Children.Remove(popup);
                });
                removeTimer.Start();
            });
            fadeTimer.Start();
        }

        /* --------------- Opening a folder containing markdown files, displaying it through a treeview --------------- */
    }
}