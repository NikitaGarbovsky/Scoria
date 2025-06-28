using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Syntax;
using Markdig.Extensions.Yaml;
using YamlDotNet.Serialization;
using Scoria.Models;

namespace Scoria.Services;


/// <summary>
/// Utility class that extracts <c>YAML front-matter</c> from a Markdown document
/// and maps it onto a strongly-typed <see cref="NoteMetadata"/> record.
/// <para>
///     • It parses the Markdown with Markdig <c>.UseYamlFrontMatter()</c>,  
///       locates the first <c>---\n … \n---</c> block, strips the fences,  
///       and deserialises the inner YAML via YamlDotNet.
/// </para>
/// <para>
///     • Any malformed or empty front-matter is ignored — the method returns
///       <see langword="null" /> so callers can fall back gracefully.
/// </para>
/// <para>
///     • Only the standard keys “<c>date</c>”, “<c>tags</c>”, and “<c>aliases</c>”
///       are mapped explicitly.  All other keys are preserved in
///       <see cref="NoteMetadata.Extra"/> for future use.
/// </para>
/// </summary>
internal static class MetadataParser
{
    /// <summary>
    /// A minimal Markdig pipeline — we only need it to recognise the YAML block.
    /// </summary>
    private static readonly MarkdownPipeline pipeline =
        new MarkdownPipelineBuilder()
           .UseYamlFrontMatter()
           .Build();

    /// <summary>
    /// YamlDotNet deserializer configured to ignore unknown properties
    /// (we handle the mapping manually).
    /// </summary>
    private static readonly IDeserializer yaml =
        new DeserializerBuilder()
           .IgnoreUnmatchedProperties()
           .Build();
    
    /// <summary>
    /// Parses the first YAML fence in <paramref name="_markdown"/> (if any) and
    /// converts it to a <see cref="NoteMetadata"/> instance.
    /// </summary>
    /// <param name="_markdown">Raw Markdown text including any front-matter.</param>
    /// <returns>
    /// A populated <see cref="NoteMetadata"/> or <see langword="null"/> if
    /// no valid YAML front-matter is found.
    /// </returns>
    public static NoteMetadata? Extract(string _markdown)
    {
        // ---------- 1) Locate the YAML block ---------------------------------
        var doc = Markdig.Markdown.Parse(_markdown, pipeline);
        var yamlBlock = doc.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock == null) return null;

        // ---------- 2) Slice the raw YAML (still contains the fences) ---------
        var rawYaml = _markdown.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length)
            .Split('\n');                      // safe on Win/Lin/mac
        if (rawYaml.Length < 3) return null; // fence + something + fence

        // drop first ("---") and last ("---") lines
        var innerYaml = string.Join('\n', rawYaml, 1, rawYaml.Length - 2);

        // ---------- 3) Deserialize to a dictionary --------------------------
        Dictionary<string, object> map;
        try
        {
            map = yaml.Deserialize<Dictionary<string, object>>(innerYaml);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // Corrupted YAML should never crash the app — we just skip metadata.
            return null;   // silently ignore broken front-matter
        }

        // Helper converts scalar or sequence → string[]
        static IReadOnlyList<string> ToStringList(object? _o) =>
            _o switch
            {
                null                           => Array.Empty<string>(),
                string s                       => new[] { s },
                IEnumerable<object> coll       => coll.Select(_x => _x.ToString()!)
                                                      .Where(_s => !string.IsNullOrWhiteSpace(_s))
                                                      .ToArray(),
                _                              => new[] { _o.ToString()! }
            };

        // ---------- 4) Map well-known keys ----------------------------------- TODO add more property types here.
        var tags    = map.TryGetValue("tags",    out var t) ? ToStringList(t) : Array.Empty<string>();
        var aliases = map.TryGetValue("aliases", out var a) ? ToStringList(a) : Array.Empty<string>();
        var dateVal = map.TryGetValue("date",    out var d) && DateOnly.TryParse(d?.ToString(), out var dd)
                       ? dd : (DateOnly?)null;

        // ---------- 5) Preserve any custom keys ------------------------------
        var extra = map.Where(_kv => _kv.Key is not ("tags" or "aliases" or "date"))
                       .ToDictionary(_kv => _kv.Key, _kv => _kv.Value);

        return new NoteMetadata(dateVal, tags, aliases, extra);
    }
}
