//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Toml;

/// <summary>
/// Builds a culture-aware TOML localization provider with per-culture file paths.
/// </summary>
public sealed class TomlLocalizationProviderBuilder {
    private readonly CulturePathProviderBuilder _builder = new(".toml", static path => new TomlMessageProvider(path));

    /// <summary>
    /// Adds a culture with the path to its TOML message file.
    /// </summary>
    /// <param name="culture">The culture code (e.g., "en", "ar-SA").</param>
    /// <param name="filePath">The path to the TOML file for this culture.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TomlLocalizationProviderBuilder AddCulture(string culture, string filePath) {
        _builder.AddCulture(culture, filePath);
        return this;
    }

    /// <summary>
    /// Adds multiple cultures using a base directory and subdirectory convention.
    /// Files are expected at <c>{baseDirectory}/{subdirectory}/{culture}.toml</c>.
    /// </summary>
    /// <param name="baseDirectory">The base directory containing localization folders.</param>
    /// <param name="cultures">The culture codes to add.</param>
    /// <param name="subdirectory">The subdirectory under baseDirectory (default "Localization").</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is null or empty, when <paramref name="cultures"/> is empty, or when a culture code is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultures"/> is null.</exception>
    public TomlLocalizationProviderBuilder AddCultures(string baseDirectory, string[] cultures, string subdirectory = "Localization") {
        _builder.AddCultures(baseDirectory, cultures, subdirectory);
        return this;
    }

    /// <summary>
    /// Performs the build operation.
    /// </summary>
    /// <returns>The build result.</returns>
    internal IInnerMessageProvider Build() => _builder.Build();
}
