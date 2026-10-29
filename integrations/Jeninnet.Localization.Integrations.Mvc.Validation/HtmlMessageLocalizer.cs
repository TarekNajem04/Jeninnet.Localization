//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Mvc.Validation;

/// <summary>
/// An <see cref="IHtmlLocalizer"/> over the Jeninnet message pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Architecture decision: arguments are HTML-encoded (through
/// <see cref="HtmlSafeMessageFormatter.EncodeArgument(object?)"/>) before
/// delegating to the wrapped <see cref="IMessageLocalizer"/>, so template
/// markup passes through verbatim while argument markup renders inert.
/// Culture fallback stays owned by <see cref="DefaultMessageLocalizer"/> —
/// this bridge performs no lookup of its own and resolves through the same
/// provider graph as plain lookups. Templates are trusted developer content
/// and are never sanitized.
/// </para>
/// <para>
/// The current UI culture is captured per call (matching framework view
/// semantics) unless fixed with <see cref="WithCulture(CultureInfo)"/>. The
/// framework contract is synchronous while the pipeline is asynchronous; the
/// wait is safe in ASP.NET Core (no synchronization context) and documented
/// here. Key enumeration is not part of the message pipeline, so
/// <see cref="GetAllStrings(bool)"/> throws <see cref="NotSupportedException"/>.
/// Every member (including the <see cref="IStringLocalizer"/> surface below)
/// encodes arguments; only the result wrapper differs.
/// </para>
/// </remarks>
public class HtmlMessageLocalizer : IHtmlLocalizer {
    private readonly IMessageLocalizer _localizer;
    private readonly CultureInfo? _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlMessageLocalizer"/> class.
    /// </summary>
    /// <param name="localizer">The message localizer owning lookup, fallback, and formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localizer"/> is null.</exception>
    public HtmlMessageLocalizer(IMessageLocalizer localizer)
        : this(localizer, culture: null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlMessageLocalizer"/> class with a fixed culture.
    /// </summary>
    /// <param name="localizer">The message localizer owning lookup, fallback, and formatting.</param>
    /// <param name="culture">The fixed culture, or <see langword="null"/> to capture the current UI culture per call.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localizer"/> is null.</exception>
    protected HtmlMessageLocalizer(IMessageLocalizer localizer, CultureInfo? culture) {
        ArgumentNullException.ThrowIfNull(localizer);

        _localizer = localizer;
        _culture = culture;
    }

    /// <inheritdoc/>
    public virtual LocalizedHtmlString this[string name] {
        get {
            ArgumentNullException.ThrowIfNull(name);

            return Resolve(name, []);
        }
    }

    /// <inheritdoc/>
    public virtual LocalizedHtmlString this[string name, params object[] arguments] {
        get {
            ArgumentNullException.ThrowIfNull(name);

            return Resolve(name, arguments ?? []);
        }
    }

    /// <inheritdoc/>
    public virtual LocalizedString GetString(string name) {
        ArgumentNullException.ThrowIfNull(name);

        var message = ResolveMessage(name, []);
        return message is null
            ? new LocalizedString(name, name, resourceNotFound: true)
            : new LocalizedString(name, message, resourceNotFound: false);
    }

    /// <inheritdoc/>
    public virtual LocalizedString GetString(string name, params object[] arguments) {
        ArgumentNullException.ThrowIfNull(name);

        var message = ResolveMessage(name, arguments ?? []);
        return message is null
            ? new LocalizedString(name, name, resourceNotFound: true)
            : new LocalizedString(name, message, resourceNotFound: false);
    }

    /// <inheritdoc/>
    public virtual IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) {
        _ = includeParentCultures;
        throw new NotSupportedException($"Key enumeration is not part of the Jeninnet message pipeline ({nameof(IMessageProvider)} is exact-match lookup only); {nameof(GetAllStrings)} is not supported by {nameof(HtmlMessageLocalizer)}.");
    }

    /// <inheritdoc/>
    public virtual IHtmlLocalizer WithCulture(CultureInfo culture) {
        ArgumentNullException.ThrowIfNull(culture);

        return new HtmlMessageLocalizer(_localizer, culture);
    }

    /// <summary>
    /// Performs the resolve operation.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The resolve result.</returns>
    private LocalizedHtmlString Resolve(string name, object[] arguments) {
        var message = ResolveMessage(name, arguments);
        return message is null
            ? new LocalizedHtmlString(name, name, isResourceNotFound: true)
            : new LocalizedHtmlString(name, message, isResourceNotFound: false);
    }

    /// <summary>
    /// Resolves message.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The message result.</returns>
    private string? ResolveMessage(string name, object[] arguments) {
        var culture = _culture ?? CultureInfo.CurrentUICulture;
        var parameters = new Dictionary<string, object?>(arguments.Length);
        for(var index = 0; index < arguments.Length; index++) {
            parameters[index.ToString(CultureInfo.InvariantCulture)] = HtmlSafeMessageFormatter.EncodeArgument(arguments[index]);
        }

        // Sync-over-async is inherent to the framework synchronous contract.
        // Blocking here is safe under ASP.NET Core, which flows no
        // synchronization context. AsTask materializes the wait explicitly
        // instead of consuming the single-use ValueTask inline.
        return _localizer.LocalizeAsync(name, parameters, culture).AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>
/// A generic <see cref="IHtmlLocalizer{T}"/> over the Jeninnet message pipeline.
/// </summary>
/// <remarks>
/// Behaves exactly like <see cref="HtmlMessageLocalizer"/>; the type parameter
/// only provides a distinct DI (Dependency Injection) identity for view-specific localizers.
/// </remarks>
/// <typeparam name="T">The resource type identity (unused for lookup).</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="HtmlMessageLocalizer{T}"/> class.
/// </remarks>
/// <param name="localizer">The message localizer owning lookup, fallback, and formatting.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="localizer"/> is null.</exception>
public sealed class HtmlMessageLocalizer<T>(IMessageLocalizer localizer) : HtmlMessageLocalizer(localizer, culture: null), IHtmlLocalizer<T> {
}
