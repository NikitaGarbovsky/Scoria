using System.Collections;
using System.Collections.Generic;
using Scoria.Models;

namespace Scoria.Services
{
    public interface IFileExplorerService
    {
        IEnumerable<FileItem> LoadFolder(string _folderPath);
    }
}

