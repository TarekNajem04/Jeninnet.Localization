//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Mvc.Validation;

/// <summary>
/// A Razor tag helper for inline content localization:
/// <c>&lt;loc key="Cart.Title"&gt;Shopping Cart&lt;/loc&gt;</c> resolves the key through
/// the canonical message pipeline and falls back to the element body when the pipeline
/// misses — no code-behind lookup.
/// </summary>
/// <remarks>
/// <para>
/// Lookup and formatting are the  bridge over <see cref="IMessageLocalizer"/>
///: templates are trusted
/// developer content, <see cref="Arguments"/> values are HTML-encoded through the
/// single <c>HtmlSafeMessageFormatter</c> path, and the culture is the per-call
/// current UI culture applied by request localization. The tag helper performs no
/// lookup, fallback, or encoding logic of its own.
/// </para>
/// <para>
/// On a miss — or when <see cref="Key"/> is null/whitespace — the body is emitted as
/// authored through the Razor content pipeline: static markup passes through, Razor
/// expressions were already encoded exactly once by the compiler, and no second
/// encoding is applied (; re-encoding would double-encode
/// expression output and destroy markup fallbacks). <see cref="Arguments"/> therefore
/// apply to resolved templates only.
/// </para>
/// <para>
/// The <c>&lt;loc&gt;</c> element and its attributes are removed from the output.
/// Requires an <see cref="IMessageLocalizer"/> registration (Core <c>AddLocalization()</c>
/// plus exactly one message provider); the tag helper composes the HTML bridge itself and
/// does not require the <c>AddHtmlMessageLocalizer()</c> opt-in.
/// </para>
/// </remarks>
[HtmlTargetElement("loc")]
public class LocalizeTagHelper : TagHelper {
    private readonly HtmlMessageLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizeTagHelper"/> class.
    /// </summary>
    /// <param name="messageLocalizer">The message localizer owning lookup, fallback, and formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messageLocalizer"/> is null.</exception>
    public LocalizeTagHelper(IMessageLocalizer messageLocalizer) {
        ArgumentNullException.ThrowIfNull(messageLocalizer);

        _localizer = new HtmlMessageLocalizer(messageLocalizer);
    }

    /// <summary>
    /// Gets or sets the message key resolved through the canonical pipeline
    /// (for example <c>Cart.Title</c>). When null or whitespace the element body
    /// renders unchanged.
    /// </summary>
    [HtmlAttributeName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the positional template arguments (<c>{0}</c>, <c>{1}</c>, …), bound
    /// from a Razor expression (<c>arguments="@model.Args"</c>, an <see cref="object"/>
    /// array — the same shape as the peer's <c>localize-args</c>). Values are HTML-encoded
    /// by the  bridge; they apply to resolved templates only.
    /// </summary>
    [HtmlAttributeName("arguments")]
    public object[]? Arguments { get; set; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="output"/> is null.</exception>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        output.TagName = null;
        output.Attributes.Clear();

        var fallback = await output.GetChildContentAsync().ConfigureAwait(false);
        if(string.IsNullOrWhiteSpace(Key)) {
            output.Content.SetHtmlContent(fallback);
            return;
        }

        var localized = _localizer[Key, Arguments ?? []];
        output.Content.SetHtmlContent(localized.IsResourceNotFound ? fallback : localized);
    }
}
