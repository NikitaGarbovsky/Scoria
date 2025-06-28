using System;
using System.Collections.Generic;

namespace Scoria.Models;

/// <summary>
/// Strongly-typed view of the YAML front-matter of a note.
/// Any unknown keys are preserved in <c>Extra</c> (if used) so data is never lost.
/// </summary>
public sealed record NoteMetadata(
    DateOnly?                    Date,
    IReadOnlyList<string>        Tags,
    IReadOnlyList<string>        Aliases,
    IReadOnlyDictionary<string, object> Extra);