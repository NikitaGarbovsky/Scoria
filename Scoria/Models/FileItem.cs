using System.Collections.ObjectModel;
using System.IO;
using ReactiveUI;

namespace Scoria.Models
{
    /// <summary>
    /// Represents a folder or markdown file in the hierarchy tree of the opened folder.
    /// Stores a bunch of properties about the file so we can reference it throughout the codebase. 
    /// </summary>
    public class FileItem : ReactiveObject
    {
        public string Name { get; }
        public string Path { get; set; }
        public bool IsDirectory { get; }
        /// <summary>Parsed YAML front-matter; <c>null</c> if the note has none.</summary>
        public NoteMetadata? Metadata { get; set; }
        public ObservableCollection<FileItem> Children { get; } = new();
        
        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set => this.RaiseAndSetIfChanged(ref isExpanded, value);
        }
        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.RaiseAndSetIfChanged(ref isSelected, value);
        }

        private bool isEditing;
        public bool IsEditing
        {
            get => isEditing;
            set => this.RaiseAndSetIfChanged(ref isEditing, value);
        }
        public FileItem(string _name, string _path, bool _isDirectory, NoteMetadata? _metadata, FileItem? _parent = null)
        {
            Name = _name;
            Path = _path;
            IsDirectory = _isDirectory;
            Metadata = _metadata;
            Parent      = _parent;
        }
        public FileItem? Parent { get; init; }
        /// <summary>
        /// What the UI should show.  
        /// Folders keep their name; files lose the “.md” extension.
        /// </summary>
        public string DisplayName
        {
            get => IsDirectory ? Name
                : System.IO.Path.GetFileNameWithoutExtension(Name);

            set
            {
                // ignore no-op or whitespace
                if (string.IsNullOrWhiteSpace(value) || value == DisplayName)
                    return;

                /* 1️⃣  update the internal filename  */
                var newName = IsDirectory
                    ? value                     // folder → plain text
                    : $"{value}.md";            // file  → add extension

                var backingField = Name;
                this.RaiseAndSetIfChanged(ref backingField, newName);

                /* 2️⃣  update the full path so the file system stays in sync */
                var dir = System.IO.Path.GetDirectoryName(Path) ?? "";
                var newFullPath = System.IO.Path.Combine(dir, newName);
                var field = Path;
                this.RaiseAndSetIfChanged(ref field, newFullPath);

                /* 3️⃣  notify UI that DisplayName itself changed */
                this.RaisePropertyChanged(nameof(DisplayName));
            }
        }
    }
}

