//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="CultureInfo"/> and related assembly attributes.
/// </summary>
public static class CultureInfoExtensions {
    private const string DEFAULT_FALLBACK_CULTURE = "nl-NL";

    /// <summary>
    /// Return a particular assembly attribute value for the calling assembly.
    /// </summary>
    /// <returns>The neutral resources language of the calling assembly, or <see cref="CultureInfo.CurrentUICulture"/> when it cannot be resolved.</returns>
    public static CultureInfo GetAssemblyNeutralResourcesLanguage() =>
        CultureInfo.GetCultureInfo(GetAssemblyNeutralResourcesLanguageCultureName(Assembly.GetCallingAssembly())) ??
        CultureInfo.CurrentUICulture;

    /// <summary>
    /// Return a particular assembly attribute value.
    /// </summary>
    /// <param name="assembly">The assembly for which to retrieve the neutral resources language.</param>
    /// <returns>The neutral resources language of <paramref name="assembly"/>, or <see cref="CultureInfo.CurrentUICulture"/> when it cannot be resolved.</returns>
    public static CultureInfo GetAssemblyNeutralResourcesLanguage(this Assembly assembly) =>
        CultureInfo.GetCultureInfo(GetAssemblyNeutralResourcesLanguageCultureName(assembly)) ??
        CultureInfo.CurrentUICulture;

    /// <summary>
    /// Retrieves the neutral resources language culture name for the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to check.</param>
    /// <returns>The culture name string.</returns>
    public static string GetAssemblyNeutralResourcesLanguageCultureName(this Assembly assembly) {
        var attribute = assembly.GetCustomAttribute<NeutralResourcesLanguageAttribute>();

        return attribute?.CultureName ?? DEFAULT_FALLBACK_CULTURE;
    }

    /// <summary>
    /// Check whether the name specified for the culture exists in the system cultures.
    /// </summary>
    /// <param name="cultureName">The name of a culture. Name is not case-sensitive.</param>
    /// <returns>True if the culture exists, otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultureName"/> is null.</exception>
    public static bool CultureExists(string cultureName) {
        ArgumentNullException.ThrowIfNull(cultureName);

        return Array.Exists
              (
                  CultureInfo.GetCultures(CultureTypes.AllCultures),
                  culture => string.Equals(culture.Name, cultureName, StringComparison.OrdinalIgnoreCase)
              );
    }

    /// <summary>
    /// Tries to retrieve a cached, read-only instance of a culture using the specified culture name.
    /// </summary>
    /// <param name="cultureName">The name of a culture. Name is not case-sensitive.</param>
    /// <param name="defaultCultureCode">Default culture if the required culture does not exist.</param>
    /// <param name="culture">The result culture instance.</param>
    /// <returns>True if the culture was retrieved successfully, otherwise false.</returns>
    public static bool TryGetCultureInfo(string cultureName, string defaultCultureCode, out CultureInfo culture) {
        try {
            culture = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch(CultureNotFoundException) {
            if(string.IsNullOrEmpty(defaultCultureCode)) {
                // Fallback Culture
                culture = GetAssemblyNeutralResourcesLanguage();
            } else {
                try {
                    culture = CultureInfo.GetCultureInfo(defaultCultureCode);
                }
                catch(CultureNotFoundException) {
                    // Fallback Culture
                    culture = GetAssemblyNeutralResourcesLanguage();
                }
            }
        }

        return false;
    }
}
