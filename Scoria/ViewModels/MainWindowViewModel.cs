using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using ReactiveUI;
using Scoria.Models;
using Scoria.Services;
using Scoria.Rendering;

namespace Scoria.ViewModels
{
    /// <summary>
    /// View-model that backs <c>MainWindow.axaml</c>.
    /// <para/>
    /// **Responsibilities**
    /// <list type="bullet">
    ///   <item>Maintains UI-state (preview ⇄ edit, selected file, tree, etc.).</item>
    ///   <item>Loads / saves markdown files on disk.</item>
    ///   <item>Delegates markdown → Avalonia rendering to <see cref="MarkdownRenderer"/>.</item>
    ///   <item>Intercepts task-list checkbox clicks in the preview and reflects the change
    ///         back into the raw markdown text.</item>
    /// </list>
    /// </summary>
    public class MainWindowViewModel : ReactiveObject
    {
        /*──────────────────────────── 1.  Reactive UI state ───────────────────────────*/
        
        private bool isEditorVisible   = false;  // start in preview
        private bool isPreviewVisible  = true;

        /// <summary>Whether the <see cref="TextBox"/> editor is visible.</summary>
        public bool IsEditorVisible
        {
            get => isEditorVisible;
            private set => this.RaiseAndSetIfChanged(ref isEditorVisible, value);
        }
        
        /// <summary>Whether the rendered markdown preview is visible.</summary>
        public bool IsPreviewVisible
        {
            get => isPreviewVisible;
            private set => this.RaiseAndSetIfChanged(ref isPreviewVisible, value);
        }
        
        /*──────────────────────────── 2.  Services injected via ctor ───────────────────*/
        
        private readonly IFileExplorerService explorer;
        private readonly IToastService toastService;
        private readonly MarkdownRenderer markdownRenderer;
        private bool inPreview = true; // Tracks current mode
        
        /*──────────────────────────── 3.  Public bind-able properties ──────────────────*/
        public ObservableCollection<FileItem> FileTree { get; } = new();
        
        private string editorText = "";
        /// <summary>The plain markdown text currently shown in the editor.</summary>
        public string EditorText
        {
            get => editorText;
            set => this.RaiseAndSetIfChanged(ref editorText, value);
        }
        
        private Control previewControl = new TextBlock();
        /// <summary>Cache of the live preview control returned by <see cref="MarkdownRenderer"/>.</summary>
        public Control PreviewControl
        {
            get => previewControl;
            set => this.RaiseAndSetIfChanged(ref previewControl, value);
        }
        
        private FileItem? selected;
        /// <summary>The file/folder currently selected in the tree.</summary>
        public FileItem? SelectedItem
        {
            get => selected;
            set
            {
                this.RaiseAndSetIfChanged(ref selected, value);
                if (value != null && !value.IsDirectory)
                    LoadFile(value.Path);
            }
        }
        private bool isFolderOpen;
        public bool IsFolderOpen
        {
            get => isFolderOpen;
            private set => this.RaiseAndSetIfChanged(ref isFolderOpen, value);
        }
        private string rootFolder = "";

        public string RootFolder
        {
            get => rootFolder;
            private set => this.RaiseAndSetIfChanged(ref rootFolder, value);
        }

        /*──────────────────────────── 4.  Reactive commands (bound in XAML) ────────────*/
        
        public ReactiveCommand<Unit, Unit> ToggleEditPreviewCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> RenderCommand             { get; }
        public ReactiveCommand<FileItem, Unit> RenameCommand { get; }
        public ReactiveCommand<FileItem, Unit> DeleteCommand { get; }
        /*──────────────────────────── 5.  Construction ────────────────────────────────*/

        /// <summary>DI ctor – receives services from <c>Program.cs</c>.</summary>
        public MainWindowViewModel(
            IFileExplorerService _explorer,
            IToastService _toastService,
            MarkdownRenderer _renderer)
        {
            explorer         = _explorer;
            toastService     = _toastService;
            markdownRenderer = _renderer;
            
            ToggleEditPreviewCommand = ReactiveCommand.Create(ToggleMode);
            SaveCommand              = ReactiveCommand.Create(SaveCurrent);
            RenderCommand            = ReactiveCommand.Create(RenderPreview);
            RenameCommand = ReactiveCommand.Create<FileItem>(BeginInlineRename);
            DeleteCommand = ReactiveCommand.Create<FileItem>(PromptAndDelete);
            
            RenderPreview(); // Show initial empty preview
        }
        
        /*──────────────────────────── 6.  File-tree population helper ─────────────────*/
        
        /// <summary>Populates <see cref="FileTree"/> from a disk folder (recursively).</summary>
        public async Task LoadFolderAsync(string _folder)
        {
            // 1) heavy disk scan off-UI thread
            var items = await Task.Run(() => explorer.LoadFolder(_folder).ToList());

            // 2) UI updates on dispatcher
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RootFolder   = _folder;
                FileTree.Clear();
                foreach (var it in items)
                    FileTree.Add(it);
                IsFolderOpen = true;
            });
        }
        /*──────────────────────────── 7.  Mode switching & persistence ────────────────*/
        
        /// <summary>Handles Ctrl + E – toggles preview / edit.TODO move input to dedicated class</summary>
        private void ToggleMode()
        {
            if (inPreview)           // leaving preview → edit
            {
                // show editor
                IsEditorVisible = true;
                IsPreviewVisible = false;
            }
            else                      // leaving edit → preview
            {
                SaveCurrent();        // write file when you exit edit
                RenderPreview();       // then refresh preview
                IsEditorVisible = false;
                IsPreviewVisible = true;
            }
            inPreview = !inPreview;
        }
        /// <summary>Saves <see cref="EditorText"/> to disk and shows a toast.</summary>
        private void SaveCurrent()
        {
            // Early out, nothing to save.
            if (SelectedItem?.IsDirectory != false) return; 

            File.WriteAllText(SelectedItem.Path, EditorText);
            toastService.Show($" {SelectedItem.DisplayName}");
        }
        
        /*──────────────────────────── 8.  File-IO helpers ─────────────────────────────*/
        
        /// <summary>Loads a markdown file into the editor and renders preview.</summary>
        private void LoadFile(string _path)
        {
            EditorText     = File.ReadAllText(_path);
            RenderPreview();
        }
        
        /*──────────────────────────── 9.  Preview rendering ───────────────────────────*/
        
        /// <summary>Regenerates <see cref="PreviewControl"/> from current markdown.</summary>
        private void RenderPreview()
        {
            if (SelectedItem is null)
            { 
                PreviewControl = markdownRenderer.Render(string.Empty, null, null);
             return;
            }
            // 1). Parse the fresh YAML of the _current_ note
            var liveMeta = string.IsNullOrEmpty(EditorText)
                                  ? null
                                  : MetadataParser.Extract(EditorText);
            
            // 2). Render with up-to-date metadata
            PreviewControl = markdownRenderer.Render(
                EditorText,
                liveMeta,
                OnTaskToggled,
                OnWikiLinkClicked);
            
            // 3). Keep the tree-view model in sync for later searches
            SelectedItem.Metadata = liveMeta;
        }
        
        /*──────────────────────────── 10. Task-list handling ──────────────────────────*/
        
        /// <summary>
        /// Regex that matches the task-list marker at the start of a line
        /// <c>- [ ]</c>, <c>- [x]</c> or <c>- [X]</c>.
        /// </summary>
        private static readonly Regex _taskToggleRegex =
            new Regex(@"- \[(?: |x|X)\]", RegexOptions.Compiled);
        /// <summary>
        /// Invoked by <see cref="MarkdownRenderer"/> when a checkbox is toggled
        /// in the preview. Updates only the affected line inside
        /// <see cref="EditorText"/> so the markdown stays in sync.
        /// </summary>
        private void OnTaskToggled(int _lineIndex, bool _isChecked)
        {
            var lines = EditorText.Split('\n').ToList();
            if (_lineIndex >= 0 && _lineIndex < lines.Count)
            {
                // replace any "- [ ]", "- [x]" or "- [X]" with the desired state:
                var replacement = _isChecked ? "- [x]" : "- [ ]";
                lines[_lineIndex] = _taskToggleRegex.Replace(lines[_lineIndex], replacement);
                EditorText = string.Join("\n", lines);
            }
            
            RenderPreview(); // Refresh UI preview to reflect new state
        }
        
        /// <summary>
        /// Navigates to – and renders – the note referenced by a wiki-link.
        /// </summary>
        /// <param name="_slug">
        /// Basename of the target note (no “.md”, case-insensitive).  
        /// Obtained from the link the user clicked.
        /// </param>
        private void OnWikiLinkClicked(string _slug)
        {
            // Resolve the slug to a FileItem in the in-memory index.
            var target = NoteLinkIndex.Resolve(_slug);
            if (target is null) return; // Gracefully ignore broken links

            SelectedItem = target;   // Highlight in the tree-view (VM property)
            
            // LoadFile refreshes EditorText, Preview pane, metadata etc.
            LoadFile(target.Path);
        }
        
        /// <summary>Create a blank “New Note.md” in the currently-selected directory.</summary>
        public async void CreateNote()
        { 
            // Should never be able to be executable if no folders are open.
            if (!IsFolderOpen)        
                return;
            
            // 1) Decide which directory to use
            var dir = SelectedItem switch
            {
                null                          => RootFolder,                       // nothing selected
                { IsDirectory: true } d       => d.Path,                            // folder node is selected
                { IsDirectory: false } f      => Path.GetDirectoryName(f.Path)!,    // note → its parent dir
            };

            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return; // nothing we can do

            // 2) Pick a unique file name
            var baseName = "New Note";
            var name = baseName;
            var i = 1;
            while (File.Exists(Path.Combine(dir, $"{name}.md")))
                name = $"{baseName} {i++}";

            var fullPath = Path.Combine(dir, $"{name}.md");

            // 3) Write a starter file (front-matter + heading)
            var today = DateOnly.FromDateTime(DateTime.Now).ToString("dd-MMM-yy");
            var content = 
$@"---
date: {today}
tags: []
---

# {name}

";
            await File.WriteAllTextAsync(fullPath, content);

            /* 4) Refresh file-tree & open the new note */
            await LoadFolderAsync(RootFolder);
            
    /* --------- Now that the file is created and configured, we want to properly display it to the user --------- */
    
            // 5) Grab the note we just created. 
            var newItem = NoteLinkIndex.ResolvePath(fullPath);

            if (newItem is null) return; // early out if something went wrong TODO probably do something 
            
            // 6) Expand the directory/s that contain the note. 
            ExpandParents(newItem);   
            
            // 7) Select on the *next* UI-tick, when the item container exists
            // (we do this so the background processes that load the file can complete before we display it to the user)
            Dispatcher.UIThread.Post(
                () => SelectedItem = newItem,
                DispatcherPriority.Background);
            
            // 8) Finally load the file and set it to selected state. 
            LoadFile(fullPath);
            newItem.IsSelected = true;   
        }
        /// <summary>Walks up to root and flags every folder as expanded.</summary>
        private static void ExpandParents(FileItem _node)
        {
            for (var p = _node.Parent; p != null; p = p.Parent)
                p.IsExpanded = true;
        }

        private void BeginInlineRename(FileItem _item)
        {
            // 1) Switch template from TextBlock -> TextBox
            _item.IsEditing = true;
            
            // 2) When user presses Enter / Escape the view will call FinishRename()
        }

        public void FinishRename(FileItem _item, bool _commit, string? _newName = null)
        {
            _item.IsEditing = false;
            if (!_commit || string.IsNullOrWhiteSpace(_newName) || _newName == _item.DisplayName) return;
            
            /* Disk rename -> Propagate everywhere */
            string newPath = Path.Combine(Path.GetDirectoryName(_item.Path), $"{_newName}.md");
            if(_item.IsDirectory)
                Directory.Move(_item.Path, newPath);
            else
                File.Move(_item.Path, newPath);

            _item.DisplayName = $"{_newName}{(_item.IsDirectory ? "" : ".md")}";
            _item.Path = newPath;
            
            NoteLinkIndex.AddOrUpdate(_item);
        }

        private async void PromptAndDelete(FileItem _item)
        {
            bool deleteAll = true; // Default for files

            if (_item.IsDirectory)
            {
                var msg = new Window
                {
                    Content = new TextBlock
                    {
                        Text = "Delete folder with all its contents?",
                        Margin = new Thickness(20)
                    }
                };

                var yes = new Button { Content = "Yes, all" };
                var no = new Button { Content = "No, keep files" };
                var cancel = new Button { Content = "Cancel" };
                
                /* Simple three-button dialog */
                var tcs = new TaskCompletionSource<string>();
                yes.Click += (_, __) => tcs.TrySetResult("all");
                no.Click += (_, __) => tcs.TrySetResult("folder");
                cancel.Click += (_, __) => tcs.TrySetResult("cancel");
                
                msg.Content = new StackPanel{ Children = { yes, no, cancel } };
                msg.ShowDialog((Window?)null);
                var result = await tcs.Task;
                msg.Close();

                if (result == "cancel") return;
                deleteAll = result == "all";
            }
            
            /* Physical delete */
            if (_item.IsDirectory)
            {
                if(deleteAll)
                    Directory.Delete(_item.Path, recursive:true);
                else
                {
                    foreach (var f in Directory.GetFiles(_item.Path, "*", SearchOption.AllDirectories))
                        File.Move(f, Path.Combine(Path.GetDirectoryName(_item.Path), Path.GetFileName(f)));
                    Directory.Delete(_item.Path); // Now empty
                }
            }
            else    
                File.Delete(_item.Path);
            
            /* Refresh tree */
            await LoadFolderAsync(RootFolder);
        }
    }
}
