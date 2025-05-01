using System.Collections.ObjectModel;

namespace Scoria.Models
{
    /// <summary>
    /// Represents a folder or markdown file in the tree
    /// </summary>
    public class FileItem
    {
        public string Name { get; }
        public string Path { get; }
        public bool IsDirectory { get; }
        public ObservableCollection<FileItem> Children { get; } = new();

        public FileItem(string _name, string _path, bool _isDirectory)
        {
            Name = _name;
            Path = _path;
            IsDirectory = _isDirectory;
        }
    }
}

