//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// Builds a culture-aware PO (Portable Object) localization provider with per-culture file paths.
/// </summary>
public sealed class PoLocalizationProviderBuilder {
    private readonly CulturePathProviderBuilder _builder = new(".po", static path => new PoMessageProvider(path));

    /// <summary>
    /// Adds a culture with the path to its message catalog file (PO text or GNU MO (Machine Object)
    /// binary,; the reader follows the path extension).
    /// </summary>
    /// <param name="culture">The culture code (e.g., "en", "ar-SY").</param>
    /// <param name="filePath">The path to the PO or MO file for this culture.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="culture"/> or <paramref name="filePath"/> is null or empty.</exception>
    public PoLocalizationProviderBuilder AddCulture(string culture, string filePath) {
        _builder.AddCulture(culture, filePath);
        return this;
    }

    /// <summary>
    /// Adds multiple cultures using a base directory and subdirectory convention.
    /// Files are expected at <c>{baseDirectory}/{subdirectory}/{culture}.po</c>.
    /// </summary>
    /// <param name="baseDirectory">The base directory containing localization folders.</param>
    /// <param name="cultures">The culture codes to add.</param>
    /// <param name="subdirectory">The subdirectory under baseDirectory (default "Localization").</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is null or empty, when <paramref name="cultures"/> is empty, or when a culture code is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultures"/> is null.</exception>
    public PoLocalizationProviderBuilder AddCultures(string baseDirectory, string[] cultures, string subdirectory = "Localization") {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        var locations = new DefaultPoFileLocationProvider(baseDirectory, subdirectory);
        return AddCultures(locations, cultures);
    }

    /// <summary>
    /// Adds multiple cultures using a custom <see cref="IPoFileLocationProvider"/>
    ///.
    /// </summary>
    /// <param name="locationProvider">The location provider resolving per-culture PO paths.</param>
    /// <param name="cultures">The culture codes to add.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="locationProvider"/> or <paramref name="cultures"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cultures"/> is empty or contains null/empty culture.</exception>
    public PoLocalizationProviderBuilder AddCultures(IPoFileLocationProvider locationProvider, string[] cultures) {
        ArgumentNullException.ThrowIfNull(locationProvider);
        _builder.AddCultures(cultures, culture => locationProvider.GetPoFilePath(new CultureInfo(culture)));
        return this;
    }

    /// <summary>
    /// Builds the culture-aware provider.
    /// </summary>
    /// <returns>The composed inner message provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no culture was added.</exception>
    internal IInnerMessageProvider Build() => _builder.Build();
}
