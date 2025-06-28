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

    /// <summary>Add / replace a mapping.</summary>
    public static void AddOrUpdate(FileItem file)
    {
        var slug = Path.GetFileNameWithoutExtension(file.Name);
        _slugToFile[slug] = file;
    }

    /// <summary>Try get note by slug; <c>null</c> if none.</summary>
    public static FileItem? Resolve(string slug) =>
        _slugToFile.TryGetValue(slug, out var f) ? f : null;

    /// <summary>Rebuilds the index for the entire vault.</summary>
    public static void Rebuild(IEnumerable<FileItem> allFiles)
    {
        _slugToFile.Clear();
        foreach (var f in allFiles)
            if (!f.IsDirectory)
                AddOrUpdate(f);
    }
}