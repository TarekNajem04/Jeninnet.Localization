//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Mvc.Validation;

/// <summary>
/// An <see cref="IViewLocalizer"/> over the Jeninnet message pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Architecture decision: the executing
/// view path — normalized to a resource-style name such as
/// <c>Views.Catalog.Index</c> — is the view scope, represented as a gettext
/// context. A key resolves view-scoped first (through the full
/// canonical culture chain), then shared through the plain pipeline lookup (also
/// through the full canonical culture chain). Each step is a single
/// <see cref="IMessageLocalizer"/> call, so the single fallback algorithm owned by
/// <see cref="DefaultMessageLocalizer"/> is reused verbatim and no parallel
/// fallback exists. View-scoped entries are therefore authored as
/// <c>msgctxt</c>-scoped keys in the configured provider (gettext-canonical
/// <c>context + '\u0004' + key</c> encoding) and stay independent of the shared
/// entry for the same key. With no derivable view path the bridge degrades to
/// shared-only behavior; a total miss keeps the framework missing convention.
/// </para>
/// <para>
/// The bridge composes the  <see cref="HtmlMessageLocalizer"/>: argument
/// HTML-encoding, <see cref="Microsoft.AspNetCore.Mvc.Localization.LocalizedHtmlString"/>
/// wrapping, missing semantics, per-call culture capture, and the documented
/// sync-over-async behavior all remain owned by the base class. MVC calls
/// <see cref="Contextualize"/> on the instance when an <c>@inject IViewLocalizer</c>
/// property is activated; the scope follows the executing file, so partial views
/// and view-location-expanded locations (themes, areas) resolve under their own
/// resolved location.
/// </para>
/// </remarks>
public sealed class ViewMessageLocalizer : HtmlMessageLocalizer, IViewLocalizer, IViewContextAware {
    private readonly ViewScopedMessageLocalizer _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewMessageLocalizer"/> class.
    /// </summary>
    /// <param name="localizer">The message localizer owning lookup, fallback, and formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localizer"/> is null.</exception>
    public ViewMessageLocalizer(IMessageLocalizer localizer)
        : this(CreateResolver(localizer)) {
    }

    /// <summary>
    /// Initializes a new instance of the ViewMessageLocalizer class.
    /// </summary>
    /// <param name="resolver">The resolver.</param>
    private ViewMessageLocalizer(ViewScopedMessageLocalizer resolver)
        : base(resolver, culture: null) => _resolver = resolver;

    /// <summary>
    /// Applies the executing view's location as the view scope.
    /// </summary>
    /// <param name="viewContext">The view context of the executing view.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="viewContext"/> is null.</exception>
    public void Contextualize(ViewContext viewContext) {
        ArgumentNullException.ThrowIfNull(viewContext);

        _resolver.Scope = ResolveScope(viewContext);
    }

    /// <summary>
    /// Creates resolver.
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    /// <returns>The resolver result.</returns>
    private static ViewScopedMessageLocalizer CreateResolver(IMessageLocalizer localizer) {
        ArgumentNullException.ThrowIfNull(localizer);

        return new ViewScopedMessageLocalizer(localizer);
    }

    /// <summary>
    /// Derives the view scope from the executing view path: uses
    /// <see cref="ViewContext.ExecutingFilePath"/> and falls back to
    /// <see cref="ViewContext.View"/> (BCL (Base Class Library) order), strips one leading directory
    /// separator and the file extension, and replaces directory separators with
    /// dots (<c>/Views/Catalog/Index.cshtml</c> → <c>Views.Catalog.Index</c>).
    /// </summary>
    /// <param name="viewContext">The executing view context.</param>
    /// <returns>The scope, or <see langword="null"/> when no usable path exists.</returns>
    private static string? ResolveScope(ViewContext viewContext) {
        var path = viewContext.ExecutingFilePath;
        if(string.IsNullOrEmpty(path)) {
            path = viewContext.View?.Path;
        }

        if(string.IsNullOrEmpty(path)) {
            return null;
        }

        var start = path[0] is '/' or '\\' ? 1 : 0;
        var length = path.Length - start - Path.GetExtension(path).Length;
        if(length <= 0) {
            return null;
        }

        var scope = path.Substring(start, length).Replace('/', '.').Replace('\\', '.');
        return scope.Length == 0 ? null : scope;
    }
}

/// <summary>
/// The internal scope-aware resolver behind <see cref="ViewMessageLocalizer"/>
///.
/// </summary>
/// <remarks>
/// Implements: the scoped (context) lookup runs first and,
/// only on a complete miss, the plain shared lookup runs — each delegated to the
/// wrapped canonical <see cref="IMessageLocalizer"/>, so culture fallback stays
/// owned by <see cref="DefaultMessageLocalizer"/> and is never re-implemented
/// here. Plural and context members are unreachable from the
/// <see cref="IViewLocalizer"/> surface; they throw
/// <see cref="NotSupportedException"/> rather than invent view-scope composition
/// semantics outside 's scope.
/// </remarks>
internal sealed class ViewScopedMessageLocalizer : IMessageLocalizer {
    private const string UNSCOPED_SURFACE_MESSAGE =
        "View scoping applies to plain lookups only: the IViewLocalizer surface (IHtmlLocalizer) never invokes plural or context lookups, " +
        "and composing them with the view scope is outside. Resolve them through the canonical IMessageLocalizer.";

    private readonly IMessageLocalizer _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewScopedMessageLocalizer"/> class.
    /// </summary>
    /// <param name="inner">The canonical pipeline localizer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    public ViewScopedMessageLocalizer(IMessageLocalizer inner) {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <summary>
    /// Gets or sets the view scope applied to plain lookups, or <see langword="null"/>
    /// for shared-only behavior (not contextualized yet, or no derivable view path).
    /// </summary>
    public string? Scope { get; set; }

    /// <inheritdoc/>
    public async ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default) {
        var scope = Scope;
        if(scope is not null) {
            var scoped = await _inner
                .LocalizeWithContextAsync(key, scope, parameters, culture, cancellationToken)
                .ConfigureAwait(false);
            if(scoped is not null) {
                return scoped;
            }
        }

        return await _inner
            .LocalizeAsync(key, parameters, culture, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => LocalizeAsync(key, parameters, CultureInfo.CurrentUICulture, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(UNSCOPED_SURFACE_MESSAGE);

    /// <inheritdoc/>
    public ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(UNSCOPED_SURFACE_MESSAGE);

    /// <inheritdoc/>
    public ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(UNSCOPED_SURFACE_MESSAGE);

    /// <inheritdoc/>
    public ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(UNSCOPED_SURFACE_MESSAGE);
}
