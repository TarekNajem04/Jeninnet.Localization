//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Mvc.Validation;

/// <summary>
/// Creates <see cref="HtmlMessageLocalizer"/> instances for
/// framework features and third-party components that resolve the framework
/// <see cref="IHtmlLocalizerFactory"/> instead of an <see cref="IHtmlLocalizer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every created localizer carries the exact  semantics: key-based lookup through
/// the canonical provider pipeline (no lookup of its own), HTML-encoded arguments,
/// trusted templates, per-call UI-culture capture, and the framework missing-key
/// conventions. <see cref="Create(Type)"/> and <see cref="Create(string, string)"/>
/// therefore behave exactly like the DI-resolved <see cref="IHtmlLocalizer{T}"/> bridge.
/// </para>
/// <para>
/// The parameters do <em>not</em> scope the lookup: the HTML bridge has no resource-set
/// dimension, so no type→resource-set mapping is derived here. The single owner
/// of that mapping remains
/// <see cref="Jeninnet.Localization.Core.Localization.CustomStringLocalizerFactory"/>
///; this factory duplicates nothing. Parameters are validated with
/// the same input contract as the string factory.
/// </para>
/// </remarks>
public class HtmlMessageLocalizerFactory : IHtmlLocalizerFactory {
    private readonly IMessageLocalizer _messageLocalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlMessageLocalizerFactory"/> class.
    /// </summary>
    /// <param name="messageLocalizer">The message localizer owning lookup, fallback, and formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageLocalizer"/> is null.</exception>
    public HtmlMessageLocalizerFactory(IMessageLocalizer messageLocalizer) {
        ArgumentNullException.ThrowIfNull(messageLocalizer);

        _messageLocalizer = messageLocalizer;
    }

    /// <summary>
    /// Creates an HTML-safe localizer for the requested resource type.
    /// </summary>
    /// <param name="resourceSource">The resource type.</param>
    /// <returns>An <see cref="IHtmlLocalizer"/> over the canonical message pipeline.</returns>
    /// <remarks>
    /// The type is validated only; the canonical pipeline is exact-match key lookup, so the
    /// result is the same bridge the generic <see cref="IHtmlLocalizer{T}"/> registration
    /// yields.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceSource"/> is null.</exception>
    public virtual IHtmlLocalizer Create(Type resourceSource) {
        ArgumentNullException.ThrowIfNull(resourceSource);

        return CreateHtmlLocalizer();
    }

    /// <summary>
    /// Creates an HTML-safe localizer for the requested base name and location.
    /// </summary>
    /// <param name="baseName">The base name of the resource.</param>
    /// <param name="location">The location of the resource.</param>
    /// <returns>An <see cref="IHtmlLocalizer"/> over the canonical message pipeline.</returns>
    /// <remarks>
    /// The parameters are validated with the same input contract as
    /// <see cref="Jeninnet.Localization.Core.Localization.CustomStringLocalizerFactory.Create(string, string)"/>
    /// but do not scope the lookup.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseName"/> is null or empty, or <paramref name="location"/> is null.</exception>
    public virtual IHtmlLocalizer Create(string baseName, string location) {
        if(string.IsNullOrEmpty(baseName)) {
            throw new ArgumentNullException(nameof(baseName));
        }

        ArgumentNullException.ThrowIfNull(location);

        return CreateHtmlLocalizer();
    }

    /// <summary>
    /// Creates the HTML-safe localizer over the canonical message pipeline.
    /// </summary>
    /// <returns>An <see cref="IHtmlLocalizer"/> over the canonical message pipeline.</returns>
    protected virtual IHtmlLocalizer CreateHtmlLocalizer() => new HtmlMessageLocalizer(_messageLocalizer);
}
