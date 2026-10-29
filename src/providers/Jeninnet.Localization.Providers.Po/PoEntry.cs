//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// A single parsed PO (Portable Object) entry (<c>msgctxt</c> / <c>msgid</c> / <c>msgid_plural</c> / <c>msgstr[n]</c>).
/// </summary>
internal sealed class PoEntry {
    /// <summary>
    /// Gets the message context (<c>msgctxt</c>), or <see langword="null"/> when absent.
    /// </summary>
    internal string? Context { get; init; }

    /// <summary>
    /// Gets the message id (<c>msgid</c>).
    /// </summary>
    internal required string MsgId { get; init; }

    /// <summary>
    /// Gets the plural message id (<c>msgid_plural</c>), or <see langword="null"/> when absent.
    /// </summary>
    internal string? MsgIdPlural { get; init; }

    /// <summary>
    /// Gets the translation forms. A singular entry carries exactly one form
    /// (<c>msgstr</c>); a plural entry carries <c>msgstr[0..n-1]</c> in index order,
    /// preserving empty (untranslated) forms so indices stay aligned.
    /// </summary>
    internal required string[] Forms { get; init; }

    /// <summary>
    /// Gets the 1-based line number where the entry starts (diagnostics).
    /// </summary>
    internal int LineNumber { get; init; }
}
