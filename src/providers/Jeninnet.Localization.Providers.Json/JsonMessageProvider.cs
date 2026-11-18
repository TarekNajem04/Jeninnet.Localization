//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Json;

/// <summary>
/// <para>Provides access to localized messages from JSON files.</para>
/// <para>
/// Loading pipeline:
///   JSON File → Storage Model (<see cref="LocalizationResource"/>) → Dictionary Cache
/// </para>
/// </summary>
/// <remarks>
///  lifecycle model: the file path is captured as an immutable construction snapshot and the
/// file is loaded exactly once under synchronized one-shot initialization. The completed snapshot
/// is published atomically; a failed load is never cached and may be retried by a later lookup.
/// The loaded state is immutable for the lifetime of the provider (file providers are snapshot-based).
/// Lookup, refresh, and disposal flow through the shared <see cref="FileProviderShell"/>.
/// <para>
///  hot-reload (opt-in via <see cref="JsonLocalizationOptions.WatchForChanges"/>): after the
/// first successful load the provider watches the file and swaps the whole template table on
/// change (debounced, atomic, never partial). Reload failures keep serving the last-good table
/// and surface through <see cref="AcquisitionFailed"/>; successes surface through
/// <see cref="TranslationAcquired"/>. Watching never throws onto the lookup path. Lookup and
/// culture-fallback semantics are unchanged (the cross-assembly contract ruleI).
/// </para>
/// </remarks>
public class JsonMessageProvider : MessageProviderBase, IInnerMessageProvider, IRefreshableMessageProvider, IProviderEventSource, IDisposable {
    private readonly FileProviderShell _shell;

    /// <summary>
    /// Gets the canonical <see cref="LocalizationResource"/> loaded from the JSON file,
    /// or <c>null</c> if messages have not yet been loaded successfully.
    /// </summary>
    internal LocalizationResource? Resource => _shell.Resource;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageProvider"/> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON localization options containing the file path.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified JSON file is not found.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when watching is enabled with a non-positive debounce.</exception>
    public JsonMessageProvider(JsonLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        FileProviderOptions.Validate(options.FilePath, options.WatchForChanges, options.ReloadDebounce, "JSON");

        var filePath = options.FilePath;
        _shell = new FileProviderShell(
            filePath,
            options.WatchForChanges,
            options.ReloadDebounce,
            () => {
                using var stream = File.OpenRead(filePath);
                var raw = JsonSerializer.Deserialize(stream, JsonProviderJsonContext.Default.DictionaryStringString) ?? [];
                return FileProviderMessages.Create(raw);
            },
            messages => TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(messages, CultureInfo.InvariantCulture)),
            ex => AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(CultureInfo.InvariantCulture, ex)));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageProvider"/> class with the specified options.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing localized messages.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified JSON file is not found.</exception>
    public JsonMessageProvider(string filePath) : this(new JsonLocalizationOptions() { FilePath = filePath }) { }

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <inheritdoc />
    public override ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => _shell.GetAsync(key, culture, cancellationToken);

    /// <inheritdoc />
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default) => _shell.RefreshAsync(cancellationToken);

    /// <summary>
    /// Releases the file watcher when watching was enabled. Providers that live for the
    /// process lifetime never need this; owners that create providers repeatedly must dispose them.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing) {
        if(disposing) {
            _shell.Dispose();
        }
    }
}
