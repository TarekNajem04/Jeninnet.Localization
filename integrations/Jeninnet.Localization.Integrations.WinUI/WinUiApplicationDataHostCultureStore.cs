//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.WinUI;

/// <summary>
/// Persists the host culture in the Windows application-data local settings container.
/// </summary>
public sealed class WinUiApplicationDataHostCultureStore : IHostCultureStore {
    private readonly ApplicationDataContainer _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinUiApplicationDataHostCultureStore"/> class.
    /// </summary>
    /// <param name="settingsKey">The settings key that stores the culture name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="settingsKey"/> is null or empty.</exception>
    public WinUiApplicationDataHostCultureStore(string settingsKey)
        : this(settingsKey, ApplicationData.GetDefault().LocalSettings) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinUiApplicationDataHostCultureStore"/> class
    /// with an explicit application-data container.
    /// </summary>
    /// <param name="settingsKey">The settings key that stores the culture name.</param>
    /// <param name="settings">The application-data settings container.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="settingsKey"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
    public WinUiApplicationDataHostCultureStore(string settingsKey, ApplicationDataContainer settings) {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsKey);
        ArgumentNullException.ThrowIfNull(settings);

        SettingsKey = settingsKey;
        _settings = settings;
    }

    /// <summary>
    /// Gets the settings key that stores the culture name.
    /// </summary>
    public string SettingsKey { get; }

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if(!_settings.Values.TryGetValue(SettingsKey, out var value)) {
            return ValueTask.FromResult<string?>(null);
        }

        var cultureName = value as string;
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(cultureName) ? null : cultureName.Trim());
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string cultureName, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        cancellationToken.ThrowIfCancellationRequested();
        _settings.Values[SettingsKey] = cultureName.Trim();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        _settings.Values.Remove(SettingsKey);
        return ValueTask.CompletedTask;
    }
}
