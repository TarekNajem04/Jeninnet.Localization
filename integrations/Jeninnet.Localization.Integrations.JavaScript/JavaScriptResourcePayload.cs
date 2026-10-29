//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.JavaScript;

/// <summary>
/// A ready-to-serve per-culture resource payload: the exact UTF-8 (Unicode Transformation Format, 8-bit) bytes the
/// endpoint writes and the static export stores, plus their content-hash ETag.
/// </summary>
public sealed class JavaScriptResourcePayload {
    private readonly byte[] _utf8Json;

    /// <summary>
    /// Initializes a new instance of the JavaScriptResourcePayload class.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="utf8Json">The utf8Json.</param>
    /// <param name="eTag">The e tag.</param>
    internal JavaScriptResourcePayload(string culture, byte[] utf8Json, string eTag) {
        Culture = culture;
        _utf8Json = utf8Json;
        ETag = eTag;
        Json = Encoding.UTF8.GetString(utf8Json);
    }

    /// <summary>
    /// Gets the normalized culture the payload was built for.
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Gets the quoted strong ETag (SHA-256 (Secure Hash Algorithm, 256-bit) over <see cref="Utf8Json"/>).
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// Gets the JSON document (<c>{key: template}</c> in allow-list order, JS-safe encoded).
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Gets the exact UTF-8 bytes of the payload.
    /// </summary>
    public ReadOnlyMemory<byte> Utf8Json => _utf8Json;
}
