//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core.MissingKeys;

/// <summary>
/// <see cref="IMissingKeySink"/> bridge to <see cref="ILogger"/>: each miss
/// is logged once as a structured warning carrying the key, culture, and call site.
/// Only identifiers are logged — template text never crosses this boundary.
/// </summary>
/// <param name="logger">The logger receiving the miss warnings.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/>.</exception>
public sealed partial class LoggerMissingKeySink(ILogger logger) : IMissingKeySink {
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public void ReportMiss(string key, string culture, string? callSite) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        LogMiss(_logger, key, culture, callSite ?? string.Empty);
    }

    /// <summary>
    /// Performs the log miss operation.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="callSite">The call site.</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Localization miss for key '{Key}' in culture '{Culture}' at '{CallSite}'.")]
    private static partial void LogMiss(ILogger logger, string key, string culture, string callSite);
}
