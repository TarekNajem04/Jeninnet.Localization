//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Pseudo;

/// <summary>
/// Pseudo-localization message provider: serves a synthetic
/// culture (default <c>qps-ploc</c>) by deterministically transforming a source
/// catalog — accented letters, <c>[!! … !!]</c> markers, and <c>~</c> expansion —
/// so UI overflow and untranslated-string leaks surface in tests. Placeholders
/// (<c>{…}</c>, including nested ICU (International Components for Unicode) spans) pass through verbatim, so formatted
/// output keeps its parameters.
/// </summary>
/// <remarks>
/// Declares <see cref="IWasmCompatibleMessageProvider"/>: the source table is a
/// copied construction snapshot and resolution performs no I/O.
/// </remarks>
public class PseudoMessageProvider : MessageProviderBase, IInnerMessageProvider, IWasmCompatibleMessageProvider {
    /// <summary>
    /// The default synthetic culture served by the provider.
    /// </summary>
    public const string DEFAULT_PSEUDO_CULTURE = "qps-ploc";

    private readonly Dictionary<string, string> _source;
    private readonly string _pseudoCulture;
    private readonly int _expansionPercent;

    /// <summary>
    /// Initializes a new instance of the <see cref="PseudoMessageProvider"/> class.
    /// </summary>
    /// <param name="options">The pseudo-localization options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or its templates are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the pseudo culture is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the expansion percent is outside 0–100.</exception>
    public PseudoMessageProvider(PseudoLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SourceTemplates);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PseudoCulture);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ExpansionPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.ExpansionPercent, 100);

        //  lifecycle invariant: immutable construction snapshot.
        _source = new Dictionary<string, string>(options.SourceTemplates, StringComparer.Ordinal);
        _pseudoCulture = options.PseudoCulture;
        _expansionPercent = options.ExpansionPercent;
    }

    /// <inheritdoc />
    public override ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(!culture.Name.Equals(_pseudoCulture, StringComparison.OrdinalIgnoreCase)) {
            return ValueTask.FromResult<string?>(null);
        }

        return ValueTask.FromResult(_source.TryGetValue(key, out var template)
            ? PseudoTemplateTransformer.Transform(template, _expansionPercent)
            : null);
    }
}
