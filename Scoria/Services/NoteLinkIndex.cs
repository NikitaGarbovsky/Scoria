using System;
using System.Collections.Generic;
using System.IO;
using Scoria.Models;

namespace Scoria.Services;

/// <summary>
/// Vault-wide lookup <c>slug → FileItem</c> used by wiki-links.
/// Slug is simply the file-name without “.md”, case-insensitive.
/// </summary>
internal static class NoteLinkIndex
{
    private static readonly Dictionary<string, FileItem> _slugToFile =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, FileItem> _pathToFile =
        new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>Add / replace both slug and full-path mappings.</summary>
    public static void AddOrUpdate(FileItem _file)
    {
        var slug = Path.GetFileNameWithoutExtension(_file.Name);
        var key  = Path.GetFullPath(_file.Path);         // unique per file

        _slugToFile[slug] = _file;
        _pathToFile[key]  = _file;
    }

    /// <summary>Try get note by slug; <c>null</c> if none.</summary>
    public static FileItem? Resolve(string _slug) =>
        _slugToFile.TryGetValue(_slug, out var f) ? f : null;
    
    /// <summary>Lookup by absolute path (fast, collision-free).</summary>
    public static FileItem? ResolvePath(string _fullPath) =>
        _pathToFile.TryGetValue(Path.GetFullPath(_fullPath), out var f) ? f : null;

    /// <summary>Rebuilds the index for the entire vault.</summary>
    public static void Rebuild(IEnumerable<FileItem> _allFiles)
    {
        _slugToFile.Clear();
        _pathToFile.Clear();

        foreach (var f in _allFiles)
            if (!f.IsDirectory)
                AddOrUpdate(f);
    }
}