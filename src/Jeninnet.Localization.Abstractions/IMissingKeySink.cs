//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Opt-in sink for localization lookup misses.
/// The localizer reports a miss only after the full fallback chain resolved
/// nothing, so intermediate steps never produce false positives. Miss semantics
/// are unchanged: the lookup still returns <see langword="null"/> and a
/// <see langword="null"/> sink (the default) adds no overhead.
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>The contract is a single reporting method on values (key, culture code,
/// call site), drafted in Abstractions per the cross-assembly contract rule. Aggregation,
/// persistence, and logging are sink implementations, not contract concerns.
/// </item>
/// <item>Call sites are first-external-frame method names captured lazily and only
/// when a sink is present: the disabled path is a single null check with no
/// allocations and behavior identical to today.
/// </item>
/// </list>
/// </remarks>
public interface IMissingKeySink {
    /// <summary>
    /// Reports one lookup miss after the full fallback chain resolved nothing.
    /// </summary>
    /// <param name="key">The missed lookup key.</param>
    /// <param name="culture">The requested culture code.</param>
    /// <param name="callSite">The external call site, or <see langword="null"/> when unavailable.</param>
    void ReportMiss(string key, string culture, string? callSite);
}
