using System.Collections;
using System.Collections.Generic;
using Scoria.Models;

namespace Scoria.Services
{
    /// <summary>
    /// Loads a folder hierarchy and converts it into an in-memory tree of
    /// <see cref="FileItem"/> nodes (used by the left-hand TreeView).
    /// </summary>
    public interface IFileExplorerService
    {
        /// <summary>
        /// Recursively enumerates <paramref name="_folderPath"/>.
        /// </summary>
        /// <remarks>
        /// * Directories are returned first (alphabetical order)  
        /// * Then all <c>*.md</c> files (alphabetical order)  
        /// * The “.obsidian” folder is skipped entirely
        /// </remarks>
        IEnumerable<FileItem> LoadFolder(string _folderPath);
    }
}

