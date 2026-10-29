//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Storage;

#if JENINNET_PROVIDER_SHARED_JSON
namespace Jeninnet.Localization.Providers.Json;
#elif JENINNET_PROVIDER_SHARED_XML
namespace Jeninnet.Localization.Providers.Xml;
#elif JENINNET_PROVIDER_SHARED_PO
namespace Jeninnet.Localization.Providers.Po;
#else
#error A provider project must define JENINNET_PROVIDER_SHARED_JSON, JENINNET_PROVIDER_SHARED_XML, or JENINNET_PROVIDER_SHARED_PO.
#endif

internal interface IFileLoadedMessages {
    LocalizationResource Resource { get; }

    IReadOnlyDictionary<string, string?> Messages { get; }
}

internal class FileLoadedMessages(LocalizationResource resource, IReadOnlyDictionary<string, string?> messages) : IFileLoadedMessages {
    public LocalizationResource Resource { get; } = resource;

    public IReadOnlyDictionary<string, string?> Messages { get; } = messages;
}

internal static class FileProviderMessages {
    public static FileLoadedMessages Create(IEnumerable<KeyValuePair<string, string>> raw) {
        var resource = new LocalizationResource {
            Entries = [.. raw.Select(static kv => new LocalizedMessageEntry {
                MessageKey = kv.Key,
                Template = kv.Value,
            })]
        };

        var messages = resource.Entries.ToDictionary(static e => e.MessageKey, static e => (string?)e.Template, StringComparer.Ordinal);
        return new FileLoadedMessages(resource, messages);
    }
}

internal static class FileProviderOptions {
    public static void Validate(string filePath, bool watchForChanges, TimeSpan reloadDebounce, string formatLabel, string optionsParameterName = "options") {
        if(!File.Exists(filePath)) {
            throw new FileNotFoundException($"The specified {formatLabel} file was not found: {filePath}");
        }

        if(watchForChanges && reloadDebounce <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(optionsParameterName, "ReloadDebounce must be positive when WatchForChanges is enabled.");
        }
    }
}

internal sealed class FileMessageProviderEngine<TLoaded>(
    string filePath,
    bool watchEnabled,
    TimeSpan debounce,
    Func<TLoaded> load,
    Action<IReadOnlyDictionary<string, string?>> acquired,
    Action<Exception> failed) where TLoaded : class, IFileLoadedMessages {
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private TLoaded? _loaded;
    private DebouncedFileReloader? _reloader;

    public TLoaded? Current => Volatile.Read(ref _loaded);

    public LocalizationResource? Resource => Current?.Resource;

    public TLoaded EnsureLoaded() {
        var loaded = Volatile.Read(ref _loaded);
        if(loaded is not null) {
            return loaded;
        }

        lock(_sync) {
            loaded = Volatile.Read(ref _loaded);
            if(loaded is not null) {
                return loaded;
            }

            loaded = load();
            Volatile.Write(ref _loaded, loaded);
            EnsureReloaderLocked();
        }

        acquired(loaded.Messages);
        return loaded;
    }

    public string? GetMessage(string key) {
        var loaded = EnsureLoaded();
        return loaded.Messages.TryGetValue(key, out var message) ? message : null;
    }

    public ValueTask RefreshAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        TLoaded loaded;
        try {
            loaded = load();
        }
        catch(Exception ex) {
            failed(ex);
            throw;
        }

        lock(_sync) {
            Volatile.Write(ref _loaded, loaded);
            EnsureReloaderLocked();
        }

        acquired(loaded.Messages);
        return default;
    }

    private void EnsureReloaderLocked() {
        if(watchEnabled && _reloader is null) {
            _reloader = new DebouncedFileReloader(filePath, debounce, TryReloadAsync);
            _reloader.Start();
        }
    }

    private ValueTask<bool> TryReloadAsync() {
        TLoaded loaded;
        try {
            loaded = load();
        }
        catch(Exception ex) {
            failed(ex);
            return ValueTask.FromResult(false);
        }

        lock(_sync) {
            Volatile.Write(ref _loaded, loaded);
        }

        acquired(loaded.Messages);
        return ValueTask.FromResult(true);
    }
}
