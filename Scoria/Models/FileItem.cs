using System.Collections.ObjectModel;
using System.IO;  

namespace Scoria.Models
{
    /// <summary>
    /// Represents a folder or markdown file in the hierarchy tree of the opened folder.
    /// </summary>
    public class FileItem
    {
        public string Name { get; }
        public string Path { get; }
        public bool IsDirectory { get; }
        /// <summary>Parsed YAML front-matter; <c>null</c> if the note has none.</summary>
        public NoteMetadata? Metadata { get; set; }
        public ObservableCollection<FileItem> Children { get; } = new();

        public FileItem(string _name, string _path, bool _isDirectory, NoteMetadata? _metadata)
        {
            Name = _name;
            Path = _path;
            IsDirectory = _isDirectory;
            Metadata = _metadata;
        }
        
        /// <summary>
        /// What the UI should show.  
        /// Folders keep their name; files lose the “.md” extension.
        /// </summary>
        public string DisplayName =>
            IsDirectory ? Name
                : System.IO.Path.GetFileNameWithoutExtension(Name); // TODO confirm if this is cross platform or not
    }
}

