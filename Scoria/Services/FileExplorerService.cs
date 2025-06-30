using System.Collections.Generic;
using System.IO;
using System.Linq;
using AvaloniaEdit.Utils;
using Scoria.Models;

namespace Scoria.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IFileExplorerService"/> that
    /// walks the real file-system using <see cref="System.IO.Directory"/>.
    /// </summary>
    public class FileExplorerService : IFileExplorerService
    {
        /// <inheritdoc/>
        public IEnumerable<FileItem> LoadFolder(string _folderPath)
        {
            return Enumerate(_folderPath, parent: null);
            
            IEnumerable<FileItem> Enumerate(string dir, FileItem? parent)
            {
                /* 1)  Folders (skip .obsidian) */
                foreach (var subDir in Directory.GetDirectories(dir)
                             .Where(d => Path.GetFileName(d) != ".obsidian")
                             .OrderBy(d => d))
                {
                    var folderItem = new FileItem(Path.GetFileName(subDir), subDir, true,null, parent);
                    folderItem.Children.AddRange(Enumerate(subDir, folderItem));
                    yield return folderItem;
                }

                /* 2)  Markdown files */
                foreach (var md in Directory.GetFiles(dir, "*.md").OrderBy(f => f))
                {
                    var text = File.ReadAllText(md);
                    var meta = MetadataParser.Extract(text);

                    var note = new FileItem(Path.GetFileName(md), md, false, meta, parent);
                    NoteLinkIndex.AddOrUpdate(note); // Register for wiki-links
                    yield return note;
                }
            }
            /* 1)  Enumerate top-level sub-folders (skip “.obsidian”). */
            // foreach (var directory in Directory.GetDirectories(_folderPath)
            //              .Where(_d => Path.GetFileName(_d) != ".obsidian")
            //              .OrderBy(_d => _d))
            // {
            //     var item = new FileItem(Path.GetFileName(directory), directory, true, null);
            //     AddChildren(item, directory);
            //     yield return item;
            // }
            //
            // /* 2) Enumerate top-level markdown files. */
            // foreach (var file in Directory.GetFiles(_folderPath, "*.md")
            //              .OrderBy(_d => _d))
            // {
            //     var markdown = File.ReadAllText(file);
            //     var meta     = MetadataParser.Extract(markdown);
            //
            //     var note = new FileItem(Path.GetFileName(file), file, false, meta);
            //     NoteLinkIndex.AddOrUpdate(note);   // Register for wiki-links
            //     yield return note;
            // }
        }

        /// <summary>
        /// Recursively populate Children for a directory node.
        /// </summary>
        /// <param name="_parent"></param>
        /// <param name="_path"></param>
        private void AddChildren(FileItem _parent, string _path)
        {
            /* a) Sub-directories */
            foreach (var dir in Directory.GetDirectories(_path)
                         .Where(_d => Path.GetFileName(_d) != ".obsidian")
                         .OrderBy(_d => _d))
            {
                var markdown = File.ReadAllText(_path);
                var meta     = MetadataParser.Extract(markdown);
                var child    = new FileItem(Path.GetFileName(dir), _path, false, meta);
                AddChildren(_parent, dir);
                _parent.Children.Add(child);
            }

            /* b) Markdown files */
            foreach (var file in Directory.GetFiles(_path, "*.md").OrderBy(_d => _d))
            {
                var markdown = File.ReadAllText(file);
                var meta     = MetadataParser.Extract(markdown);
                
                var childFile = new FileItem(Path.GetFileName(file), file, false, meta);
                _parent.Children.Add(new FileItem(Path.GetFileName(file), file, false, meta));
                NoteLinkIndex.AddOrUpdate(childFile); 
            }
        }
    }    
}
