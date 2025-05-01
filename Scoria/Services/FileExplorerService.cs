using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scoria.Models;

namespace Scoria.Services
{
    public class FileExplorerService : IFileExplorerService
    {
        public IEnumerable<FileItem> LoadFolder(string _folderPath)
        {
            // Top-level folders & .md files
            foreach (var directory in Directory.GetDirectories(_folderPath)
                         .Where(_d => Path.GetFileName(_d) != ".obsidian")
                         .OrderBy(_d => _d))
            {
                var item = new FileItem(Path.GetFileName(directory), directory, true);
                AddChildren(item,directory);
                yield return item;
            }

            foreach (var file in Directory.GetFiles(_folderPath, "*.md")
                         .OrderBy(_d => _d))
            {
                yield return new FileItem(Path.GetFileName(file), file, false);
            }
        }

        private void AddChildren(FileItem _parent, string _path)
        {
            foreach (var dir in Directory.GetDirectories(_path)
                         .Where(_d => Path.GetFileName(_d) != ".obsidian")
                         .OrderBy(_d => _d))
            {
                var child = new FileItem(Path.GetFileName(dir), dir, true);
                AddChildren(_parent, dir);
                _parent.Children.Add(child);
            }

            foreach (var file in Directory.GetFiles(_path, "*.md").OrderBy(_d => _d))
            {
                _parent.Children.Add(new FileItem(Path.GetFileName(file), file, false));
            }
        }
    }    
}
