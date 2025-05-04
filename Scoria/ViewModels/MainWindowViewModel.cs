using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using ReactiveUI;
using Scoria.Models;
using Scoria.Services;
using Scoria.Rendering;

namespace Scoria.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private bool _isEditorVisible   = false;  // start in preview
        private bool _isPreviewVisible  = true;

        public bool IsEditorVisible
        {
            get => _isEditorVisible;
            private set => this.RaiseAndSetIfChanged(ref _isEditorVisible, value);
        }
        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            private set => this.RaiseAndSetIfChanged(ref _isPreviewVisible, value);
        }
        private readonly IFileExplorerService _explorer;
        private readonly IToastService _toast;
        private readonly MarkdownRenderer _markdownRenderer;

        private bool _inPreview = true;
        
        public ObservableCollection<FileItem> FileTree { get; } = new();
        public ReactiveCommand<Unit, Unit> ToggleEditPreviewCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
        public ReactiveCommand<Unit, Unit> RenderCommand             { get; }
        private string _editorText = "";

        public string EditorText
        {
            get => _editorText;
            set => this.RaiseAndSetIfChanged(ref _editorText, value);
        }

        private Control _previewControl = new TextBlock();
        
        public Control PreviewControl
        {
            get => _previewControl;
            set => this.RaiseAndSetIfChanged(ref _previewControl, value);
        }

        private FileItem? _selected;
        public FileItem? SelectedItem
        {
            get => _selected;
            set
            {
                this.RaiseAndSetIfChanged(ref _selected, value);
                if (value != null && !value.IsDirectory)
                    LoadFile(value.Path);
            }
        }

        public MainWindowViewModel(
            IFileExplorerService explorer,
            IToastService toast,
            MarkdownRenderer renderer)
        {
            _explorer        = explorer;
            _toast           = toast;
            _markdownRenderer= renderer;

            ToggleEditPreviewCommand = ReactiveCommand.Create(ToggleMode);
            
            ToggleEditPreviewCommand = ReactiveCommand.Create(ToggleMode);
            SaveCommand               = ReactiveCommand.Create(SaveCurrent);
            RenderCommand             = ReactiveCommand.Create(RenderPreview);
            
            RenderPreview();
        }
        public void LoadFolder(string folder)
        {
            FileTree.Clear();
            foreach (var n in _explorer.LoadFolder(folder))
                FileTree.Add(n);
        }
/* ---------- helpers ---------- */

        private void ToggleMode()
        {
            if (_inPreview)           // leaving preview → edit
            {
                // show editor
                IsEditorVisible = true;
                IsPreviewVisible = false;
            }
            else                      // leaving edit → preview
            {
                SaveCurrent();        // write file when you exit edit
                RenderPreview();
                IsEditorVisible = false;
                IsPreviewVisible = true;
            }
            _inPreview = !_inPreview;
        }
        private void SaveCurrent()
        {
            if (_selected == null || _selected.IsDirectory) return;

            File.WriteAllText(_selected.Path, EditorText);
            _toast.Show($"Saved: {_selected.Name}");
        }

        private void LoadFile(string path)
        {
            EditorText     = File.ReadAllText(path);
            RenderPreview();
        }
        private void RenderPreview()
        {
            PreviewControl = _markdownRenderer.Render(
                EditorText,
                OnTaskToggled
            );
        }
        /// <summary>
        /// Used by OnTaskToggled to convert 
        /// </summary>
        private static readonly Regex _taskToggleRegex =
            new Regex(@"- \[(?: |x|X)\]", RegexOptions.Compiled);
        /// <summary>
        /// Called whenever user clicks a checkbox in the rendered view.
        /// We flip exactly that line in our raw EditorText, then re-render.
        /// </summary>
        private void OnTaskToggled(int lineIndex, bool isChecked)
        {
            var lines = EditorText.Split('\n').ToList();
            if (lineIndex >= 0 && lineIndex < lines.Count)
            {
                // replace any "- [ ]", "- [x]" or "- [X]" with the desired state:
                var replacement = isChecked ? "- [x]" : "- [ ]";
                lines[lineIndex] = _taskToggleRegex.Replace(lines[lineIndex], replacement);
                EditorText = string.Join("\n", lines);
            }
            RenderPreview();
        }
    }
}

