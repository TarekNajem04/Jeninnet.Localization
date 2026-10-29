//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The canonical per-culture localization content loaded from the JSON files embedded in the
/// Shared assembly. This is the single source of truth consumed by the in-memory store and the
/// Memory-provider seed; the committed JSON and XML files are format views of the same content
/// guarded by focused consistency tests.
/// </summary>
public static class LocalizationContent {
    private const string RESOURCE_NAME_PREFIX = "Jeninnet.Localization.Reference.Shared.Content.Localization.";

    /// <summary>
    /// Gets the complete per-culture template tables, keyed by culture code.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } = LoadTemplates();

    /// <summary>
    /// Gets the supported culture codes in registration order.
    /// </summary>
    public static IEnumerable<string> CultureCodes => All.Keys;

    /// <summary>
    /// Gets the template table for a supported culture.
    /// </summary>
    /// <param name="cultureCode">The culture code (e.g. <c>"ar-SY"</c>).</param>
    /// <returns>The read-only key-to-template table for the culture.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cultureCode"/> is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no content is defined for <paramref name="cultureCode"/>.</exception>
    public static IReadOnlyDictionary<string, string> GetTemplates(string cultureCode) {
        ArgumentException.ThrowIfNullOrEmpty(cultureCode);

        if(!All.TryGetValue(cultureCode, out var templates)) {
            throw new KeyNotFoundException($"No localization content is defined for culture '{cultureCode}'.");
        }

        return templates;
    }

    /// <summary>
    /// Attempts to retrieve the template table for a culture.
    /// </summary>
    /// <param name="cultureCode">The culture code.</param>
    /// <param name="templates">The read-only key-to-template table when the culture is supported; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the culture has supported content.</returns>
    public static bool TryGetTemplates(string cultureCode, [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? templates) {
        ArgumentException.ThrowIfNullOrEmpty(cultureCode);
        return All.TryGetValue(cultureCode, out templates);
    }

    /// <summary>
    /// Attempts to retrieve a single template for a culture.
    /// </summary>
    /// <param name="cultureCode">The culture code.</param>
    /// <param name="key">The localization key.</param>
    /// <param name="value">The template when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the key exists for the culture.</returns>
    public static bool TryGetString(string cultureCode, string key, [NotNullWhen(true)] out string? value) {
        ArgumentException.ThrowIfNullOrEmpty(cultureCode);
        ArgumentNullException.ThrowIfNull(key);

        if(All.TryGetValue(cultureCode, out var templates) && templates.TryGetValue(key, out var template)) {
            value = template;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Loads templates.
    /// </summary>
    /// <returns>The templates result.</returns>
    private static ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadTemplates() {
        var requiredKeys = GetAllKeys().Except(GetDemoKeys(), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            var entries = ReadEmbeddedCulture(cultureCode);
            var presentKeys = new HashSet<string>(entries.Keys, StringComparer.Ordinal);
            var missing = requiredKeys.Where(requiredKey => !presentKeys.Contains(requiredKey)).ToArray();
            if(missing.Length > 0) {
                throw new InvalidOperationException(
                    $"Localization content for culture '{cultureCode}' is missing required key(s): {string.Join(", ", missing)}.");
            }

            result[cultureCode] = new ReadOnlyDictionary<string, string>(entries);
        }

        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(result);
    }

    /// <summary>
    /// Reads embedded culture.
    /// </summary>
    /// <param name="cultureCode">The culture.</param>
    /// <returns>The embedded culture result.</returns>
    private static Dictionary<string, string> ReadEmbeddedCulture(string cultureCode) {
        var resourceName = RESOURCE_NAME_PREFIX + cultureCode + ".json";
        using var stream = typeof(LocalizationContent).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded localization resource '{resourceName}' was not found in the reference Shared assembly.");

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"The embedded localization resource '{resourceName}' is empty or invalid.");
    }

    /// <summary>
    /// Gets all keys.
    /// </summary>
    /// <returns>The all keys result.</returns>
    private static HashSet<string> GetAllKeys() =>
        typeof(ReferenceKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => field.GetValue(null))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets demo keys.
    /// </summary>
    /// <returns>The demo keys sequence.</returns>
    private static string[] GetDemoKeys() =>
        [ReferenceKeys.DEMO_SHOWCASE, ReferenceKeys.DEMO_REGIONAL_NOTE, ReferenceKeys.DEMO_MISSING_EVERYWHERE];
}
