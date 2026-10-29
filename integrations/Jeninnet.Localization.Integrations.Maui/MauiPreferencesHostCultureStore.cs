//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Maui;

/// <summary>
/// Persists the host culture in MAUI's platform preferences store.
/// </summary>
public sealed class MauiPreferencesHostCultureStore : IHostCultureStore {
    /// <summary>
    /// Initializes a new instance of the <see cref="MauiPreferencesHostCultureStore"/> class.
    /// </summary>
    /// <param name="preferencesKey">The preferences key that stores the culture name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="preferencesKey"/> is null or empty.</exception>
    public MauiPreferencesHostCultureStore(string preferencesKey) {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferencesKey);
        PreferencesKey = preferencesKey;
    }

    /// <summary>
    /// Gets the preferences key that stores the culture name.
    /// </summary>
    public string PreferencesKey { get; }

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Preferences.Default.Get(PreferencesKey, string.Empty);
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string cultureName, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Set(PreferencesKey, cultureName.Trim());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Remove(PreferencesKey);
        return ValueTask.CompletedTask;
    }
}
