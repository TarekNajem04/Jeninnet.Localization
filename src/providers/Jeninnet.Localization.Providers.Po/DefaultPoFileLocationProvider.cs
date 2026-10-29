//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// The default <see cref="IPoFileLocationProvider"/>: resolves a culture to
/// <c>{baseDirectory}/{subdirectory}/{culture}.po</c>.
/// </summary>
public sealed class DefaultPoFileLocationProvider : IPoFileLocationProvider {
    private readonly string _baseDirectory;
    private readonly string _subdirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPoFileLocationProvider"/> class.
    /// </summary>
    /// <param name="baseDirectory">The base directory containing localization folders.</param>
    /// <param name="subdirectory">The subdirectory under baseDirectory (default "Localization").</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> or <paramref name="subdirectory"/> is null or empty.</exception>
    public DefaultPoFileLocationProvider(string baseDirectory, string subdirectory = "Localization") {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentException.ThrowIfNullOrEmpty(subdirectory);

        _baseDirectory = baseDirectory;
        _subdirectory = subdirectory;
    }

    /// <inheritdoc />
    public string GetPoFilePath(CultureInfo culture) {
        ArgumentNullException.ThrowIfNull(culture);

        return Path.Combine(_baseDirectory, _subdirectory, $"{culture.Name}.po");
    }
}
