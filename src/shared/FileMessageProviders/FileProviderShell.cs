//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#if JENINNET_PROVIDER_SHARED_JSON
namespace Jeninnet.Localization.Providers.Json;
#elif JENINNET_PROVIDER_SHARED_XML
namespace Jeninnet.Localization.Providers.Xml;
#elif JENINNET_PROVIDER_SHARED_PO
namespace Jeninnet.Localization.Providers.Po;
#elif JENINNET_PROVIDER_SHARED_INI
namespace Jeninnet.Localization.Providers.Ini;
#elif JENINNET_PROVIDER_SHARED_TOML
namespace Jeninnet.Localization.Providers.Toml;
#else
#error A provider project must define JENINNET_PROVIDER_SHARED_JSON, JENINNET_PROVIDER_SHARED_XML, JENINNET_PROVIDER_SHARED_PO, JENINNET_PROVIDER_SHARED_INI, or JENINNET_PROVIDER_SHARED_TOML.
#endif

/// <summary>
/// Shared shell for snapshot file providers: one-shot synchronized load, hot-reload swap,
/// cache lookup, refresh, and disposal over <see cref="FileMessageProviderEngine{TLoaded}"/>.
/// The provider owns its options validation, load function, and public events; this shell owns
/// the lifecycle.
/// </summary>
internal sealed class FileProviderShell : IDisposable {
    private readonly FileMessageProviderEngine<FileLoadedMessages> _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileProviderShell"/> class.
    /// </summary>
    /// <param name="filePath">The content file path (captured as an immutable snapshot).</param>
    /// <param name="watchForChanges">Whether to watch the file for hot reload.</param>
    /// <param name="reloadDebounce">The hot-reload debounce.</param>
    /// <param name="load">Loads the file content into the storage model. Failures are never cached and may be retried by a later lookup.</param>
    /// <param name="acquired">Invoked when a load or reload publishes templates (the provider re-raises its public event).</param>
    /// <param name="failed">Invoked when a load or reload fails (the provider re-raises its public event).</param>
    internal FileProviderShell(
        string filePath,
        bool watchForChanges,
        TimeSpan reloadDebounce,
        Func<FileLoadedMessages> load,
        Action<IReadOnlyDictionary<string, string?>> acquired,
        Action<Exception> failed) {
        _engine = new FileMessageProviderEngine<FileLoadedMessages>(
            filePath,
            watchForChanges,
            reloadDebounce,
            load,
            messages => acquired(messages),
            failed);
    }

    /// <summary>
    /// Gets the canonical <see cref="LocalizationResource"/> loaded from the file,
    /// or <c>null</c> if messages have not yet been loaded successfully.
    /// </summary>
    internal LocalizationResource? Resource => _engine.Resource;

    /// <summary>
    /// Resolves one key from the loaded table.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The requested culture (file providers are culture-agnostic snapshots; the culture selects nothing).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The template, or <see langword="null"/> on a miss.</returns>
    internal ValueTask<string?> GetAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_engine.GetMessage(key));
    }

    /// <summary>
    /// Reloads the file on demand.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    internal ValueTask RefreshAsync(CancellationToken cancellationToken = default) => _engine.RefreshAsync(cancellationToken);

    /// <summary>
    /// Releases the file watcher when watching was enabled.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    private void Dispose(bool disposing) {
        if(disposing) {
            _engine.Dispose();
        }
    }
}
