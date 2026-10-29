//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// <para>Provides access to localized messages from gettext catalogs: PO (Portable Object) text files or compiled GNU MO (Machine Object) binaries.</para>
/// <para>
/// Loading pipeline:
///   Catalog File (PO text or MO binary) → <see cref="PoCatalog"/> (exact-match templates plus plural-form tables) → Dictionary Cache
/// </para>
/// </summary>
/// <remarks>
/// Architecture decision: per-culture <c>.po</c> files
/// (<c>{baseDirectory}/{subdirectory}/{culture}.po</c>); the context-key encoding reuses
///  (<c>context + '\u0004' + key</c>, EOT (End of Transmission), NGettext-compatible); plural-form
/// selection honors the catalog <c>Plural-Forms</c> header where present and the
/// <see cref="PluralRules"/> engine otherwise. The reader is selected by
/// <see cref="PoLocalizationOptions.Format"/>: <c>Auto</c> by file extension
/// (<c>.mo</c> → binary reader), with explicit <c>Po</c>/<c>Mo</c> forcing; MO binaries are
/// strict UTF-8 (Unicode Transformation Format, 8-bit) and their hash table is ignored.
/// <para>
///  lifecycle model: the file path is captured as an immutable construction snapshot and the
/// file is loaded exactly once under synchronized one-shot initialization. The completed snapshot
/// is published atomically; a failed load is never cached and may be retried by a later lookup.
/// The loaded state is immutable for the lifetime of the provider (file providers are snapshot-based).
/// </para>
/// <para>
///  hot-reload (opt-in via <see cref="PoLocalizationOptions.WatchForChanges"/>): after the
/// first successful load the provider watches the file and swaps the whole catalog (exact-match
/// templates plus plural-form tables) on change (debounced, atomic, never partial). Reload
/// failures keep serving the last-good catalog and surface through
/// <see cref="AcquisitionFailed"/>; successes surface through
/// <see cref="TranslationAcquired"/>. Watching never throws onto the lookup path. Lookup and
/// culture-fallback semantics are unchanged (the cross-assembly contract ruleI).
/// </para>
/// </remarks>
public class PoMessageProvider : MessageProviderBase, IInnerMessageProvider, IPluralFormMessageProvider, IRefreshableMessageProvider, IProviderEventSource {
    private readonly string _filePath;
    private readonly PoCatalogFormat _format;
    private readonly FileMessageProviderEngine<LoadedMessages> _engine;

    /// <summary>
    /// Gets the parsed <see cref="PoCatalog"/> loaded from the catalog file,
    /// or <c>null</c> if messages have not yet been loaded successfully.
    /// </summary>
    internal PoCatalog? Catalog => _engine.Current?.Catalog;

    /// <summary>
    /// Gets the canonical <see cref="LocalizationResource"/> flattened from the catalog file,
    /// or <c>null</c> if messages have not yet been loaded successfully.
    /// </summary>
    internal LocalizationResource? Resource => _engine.Resource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoMessageProvider"/> class with the specified options.
    /// </summary>
    /// <param name="options">The PO localization options containing the file path.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified PO file is not found.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when watching is enabled with a non-positive debounce, or when <see cref="PoLocalizationOptions.Format"/> is undefined.</exception>
    public PoMessageProvider(PoLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        FileProviderOptions.Validate(options.FilePath, options.WatchForChanges, options.ReloadDebounce, "PO");

        if(!Enum.IsDefined(options.Format)) {
            throw new ArgumentOutOfRangeException(nameof(options), options.Format, "Format must be a defined PoCatalogFormat value.");
        }

        _filePath = options.FilePath;
        _format = options.Format;
        _engine = new FileMessageProviderEngine<LoadedMessages>(
            _filePath,
            options.WatchForChanges,
            options.ReloadDebounce,
            LoadMessages,
            messages => TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(messages, CultureInfo.InvariantCulture)),
            ex => AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(CultureInfo.InvariantCulture, ex)));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoMessageProvider"/> class with the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the catalog file containing localized messages.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified PO file is not found.</exception>
    public PoMessageProvider(string filePath) : this(new PoLocalizationOptions() { FilePath = filePath }) { }

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Loads messages.
    /// </summary>
    /// <returns>The messages result.</returns>
    private LoadedMessages LoadMessages() {
        var catalog = ReadCatalog();
        var messages = FileProviderMessages.Create(catalog.Messages);
        return new LoadedMessages(catalog, messages.Resource, messages.Messages);
    }

    /// <summary>
    /// Reads catalog.
    /// </summary>
    /// <returns>The catalog result.</returns>
    private PoCatalog ReadCatalog() {
        var format = _format;
        if(format == PoCatalogFormat.Auto) {
            format = _filePath.EndsWith(".mo", StringComparison.OrdinalIgnoreCase) ? PoCatalogFormat.Mo : PoCatalogFormat.Po;
        }

        if(format == PoCatalogFormat.Mo) {
            return MoParser.Parse(File.ReadAllBytes(_filePath), _filePath);
        }

        string text;
        using(var reader = new StreamReader(_filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)) {
            text = reader.ReadToEnd();
        }

        return PoParser.Parse(text, _filePath);
    }

    /// <inheritdoc />
    public override ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_engine.GetMessage(key));
    }

    /// <inheritdoc />
    public ValueTask<string?> GetPluralTemplateAsync(string singularKey, string pluralKey, decimal count, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = _engine.EnsureLoaded();

        return ValueTask.FromResult(
            loaded.Catalog.TrySelectPluralForm(singularKey, pluralKey, null, count, culture, out var template) ? template : null);
    }

    /// <inheritdoc />
    public ValueTask<string?> GetPluralTemplateWithContextAsync(string singularKey, string pluralKey, string context, decimal count, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = _engine.EnsureLoaded();

        var effectiveContext = context.Length == 0 ? null : context;
        return ValueTask.FromResult(
            loaded.Catalog.TrySelectPluralForm(singularKey, pluralKey, effectiveContext, count, culture, out var template) ? template : null);
    }

    /// <inheritdoc />
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default) => _engine.RefreshAsync(cancellationToken);

    /// <summary>
    /// Represents loaded messages.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="resource">The localization resource.</param>
    /// <param name="messages">The message templates.</param>
    private sealed class LoadedMessages(PoCatalog catalog, LocalizationResource resource, IReadOnlyDictionary<string, string?> messages)
        : FileLoadedMessages(resource, messages) {
        /// <summary>
        /// Gets the catalog.
        /// </summary>
        public PoCatalog Catalog { get; } = catalog;
    }
}
