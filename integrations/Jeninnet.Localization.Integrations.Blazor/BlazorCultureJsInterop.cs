//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// The self-contained browser storage bridge used by the culture stores (
/// decision 3).
/// </summary>
/// <remarks>
/// Every invocation is a small self-contained script evaluated with <c>eval</c>; all
/// dynamic values are serialized with the source-generated
/// <see cref="BlazorCultureJsonContext"/> so the generated JavaScript cannot be broken
/// out of (JSON string literals are valid JavaScript string literals) and no
/// reflection-based serializer path is used. Hosts need no JavaScript file,
/// which is what removes the hand-rolled interop the reference hosts previously carried.
/// </remarks>
public sealed class BlazorCultureJsInterop {
    private const string EVAL_IDENTIFIER = "eval";

    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorCultureJsInterop"/> class.
    /// </summary>
    /// <param name="jsRuntime">The host JavaScript runtime.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsRuntime"/> is null.</exception>
    public BlazorCultureJsInterop(IJSRuntime jsRuntime) {
        ArgumentNullException.ThrowIfNull(jsRuntime);

        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Writes a cookie on the document.
    /// </summary>
    /// <param name="name">The cookie name.</param>
    /// <param name="value">The cookie value.</param>
    /// <param name="expires">The optional absolute expiry (UTC is used).</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the browser applied the cookie.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    public ValueTask WriteCookieAsync(string name, string value, DateTimeOffset? expires = null, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);

        return _jsRuntime.InvokeVoidAsync(EVAL_IDENTIFIER, cancellationToken, BuildWriteCookieScript(name, value, expires));
    }

    /// <summary>
    /// Reads a cookie value from the document.
    /// </summary>
    /// <param name="name">The cookie name.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The raw cookie value, or <see langword="null"/> when the cookie does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    public ValueTask<string?> ReadCookieAsync(string name, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _jsRuntime.InvokeAsync<string?>(EVAL_IDENTIFIER, cancellationToken, BuildReadCookieScript(name));
    }

    /// <summary>
    /// Writes a local-storage item.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The storage value.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the browser stored the item.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
    public ValueTask WriteLocalStorageAsync(string key, string value, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var script = "window.localStorage.setItem(" + SerializeJavaScriptString(key) + ", " + SerializeJavaScriptString(value) + ")";
        return _jsRuntime.InvokeVoidAsync(EVAL_IDENTIFIER, cancellationToken, script);
    }

    /// <summary>
    /// Reads a local-storage item.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
    public ValueTask<string?> ReadLocalStorageAsync(string key, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var script = "window.localStorage.getItem(" + SerializeJavaScriptString(key) + ")";
        return _jsRuntime.InvokeAsync<string?>(EVAL_IDENTIFIER, cancellationToken, script);
    }

    /// <summary>
    /// Reads the browser's preferred languages: <c>navigator.languages</c>
    /// when non-empty, otherwise <c>[navigator.language]</c>, otherwise an empty array.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The browser language preferences in browser order (never <see langword="null"/> from the script).</returns>
    public ValueTask<string[]> ReadBrowserLanguagesAsync(CancellationToken cancellationToken = default) {
        const string script = "window.navigator.languages && window.navigator.languages.length > 0 ? window.navigator.languages : (window.navigator.language ? [window.navigator.language] : [])";
        return _jsRuntime.InvokeAsync<string[]>(EVAL_IDENTIFIER, cancellationToken, script);
    }

    /// <summary>
    /// Builds write cookie script.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="expires">The expiry time.</param>
    /// <returns>The write cookie script text.</returns>
    private static string BuildWriteCookieScript(string name, string value, DateTimeOffset? expires) {
        var script = "document.cookie = " + SerializeJavaScriptString(name) + " + \"=\" + " + SerializeJavaScriptString(value) + " + \"; path=/\"";
        if(expires is not null) {
            var expiry = SerializeJavaScriptString(expires.Value.UtcDateTime.ToString("R", CultureInfo.InvariantCulture));
            script += " + \"; expires=\" + " + expiry;
        }

        return script;
    }

    /// <summary>
    /// Builds read cookie script.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The read cookie script text.</returns>
    private static string BuildReadCookieScript(string name) =>
        "(function () { var prefix = " + SerializeJavaScriptString(name) + " + \"=\"; var parts = document.cookie.split(\"; \"); for (var i = 0; i < parts.length; i++) { if (parts[i].indexOf(prefix) === 0) { return parts[i].substring(prefix.length); } } return null; })()";

    /// <summary>
    /// Serializes a caller-supplied value as a JSON string literal through the
    /// source-generated context.
    /// </summary>
    /// <param name="value">The value to serialize as a JSON string literal.</param>
    /// <returns>The JSON string literal, including its quotes.</returns>
    private static string SerializeJavaScriptString(string value) =>
        JsonSerializer.Serialize(value, BlazorCultureJsonContext.Default.String);
}
