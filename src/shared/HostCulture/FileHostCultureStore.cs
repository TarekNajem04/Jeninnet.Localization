// Shared host-culture source. Compiled into each desktop/mobile integration package.
#if JENINNET_HOSTS_AVALONIA
namespace Jeninnet.Localization.Integrations.Avalonia;
#elif JENINNET_HOSTS_MAUI
namespace Jeninnet.Localization.Integrations.Maui;
#elif JENINNET_HOSTS_WINUI
namespace Jeninnet.Localization.Integrations.WinUI;
#else
#error Compile HostCulture sources with a JENINNET_HOSTS_* constant.
#endif

/// <summary>
/// Persists one culture name in a small UTF-8 text file. Suitable for desktop hosts; mobile
/// hosts may supply a platform-preferences store instead.
/// </summary>
public sealed class FileHostCultureStore : IHostCultureStore {
    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileHostCultureStore"/> class.
    /// </summary>
    /// <param name="filePath">The absolute or relative path of the culture file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
    public FileHostCultureStore(string filePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the configured culture file path.
    /// </summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public async ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) {
        if(!File.Exists(FilePath)) {
            return null;
        }

        var value = await File.ReadAllTextAsync(FilePath, _utf8, cancellationToken).ConfigureAwait(false);
        value = value.Trim();
        return value.Length == 0 ? null : value;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(string cultureName, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        var directory = Path.GetDirectoryName(FilePath);
        if(!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(FilePath, cultureName.Trim(), _utf8, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if(File.Exists(FilePath)) {
            File.Delete(FilePath);
        }

        return ValueTask.CompletedTask;
    }
}
