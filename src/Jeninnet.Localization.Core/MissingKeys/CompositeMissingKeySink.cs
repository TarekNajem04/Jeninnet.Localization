//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.MissingKeys;

/// <summary>
/// Fan-out <see cref="IMissingKeySink"/>: reports each miss to every inner
/// sink in order, so a collector and a logger (or any other pair) can share one
/// localizer.
/// </summary>
public sealed class CompositeMissingKeySink : IMissingKeySink {
    private readonly IMissingKeySink[] _sinks;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeMissingKeySink"/> class.
    /// </summary>
    /// <param name="sinks">The inner sinks, in reporting order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sinks"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any inner sink is <see langword="null"/>.</exception>
    public CompositeMissingKeySink(params IMissingKeySink[] sinks) {
        ArgumentNullException.ThrowIfNull(sinks);
        if(Array.Exists(sinks, static sink => sink is null)) {
            throw new ArgumentException("Inner sinks must not contain null.", nameof(sinks));
        }

        _sinks = [.. sinks];
    }

    /// <inheritdoc/>
    public void ReportMiss(string key, string culture, string? callSite) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        foreach(var sink in _sinks) {
            sink.ReportMiss(key, culture, callSite);
        }
    }
}
