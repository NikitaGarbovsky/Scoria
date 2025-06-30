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
        public string Path { get; }
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
        public string DisplayName =>
            IsDirectory ? Name
                : System.IO.Path.GetFileNameWithoutExtension(Name); // TODO confirm if this is cross platform or not
    }
}

