//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.MissingKeys;

/// <summary>
/// One aggregated missing-key entry: the missed key and culture, the first observed
/// call site, how often it missed, and when it first missed.
/// </summary>
public sealed class MissingKeyEntry {
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingKeyEntry"/> class.
    /// </summary>
    /// <param name="key">The missed lookup key.</param>
    /// <param name="culture">The requested culture code.</param>
    /// <param name="callSite">The first observed call site (empty when unknown).</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public MissingKeyEntry(string key, string culture, string callSite) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(callSite);

        Key = key;
        Culture = culture;
        CallSite = callSite;
        HitCount = 1;
        FirstSeenUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the missed lookup key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the requested culture code.
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Gets the first observed call site (empty when unknown).
    /// </summary>
    public string CallSite { get; }

    /// <summary>
    /// Gets how often this key missed.
    /// </summary>
    public int HitCount { get; internal set; }

    /// <summary>
    /// Gets when this key first missed (UTC).
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; }
}
