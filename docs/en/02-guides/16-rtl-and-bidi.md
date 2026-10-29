# RTL and Bidirectional Text

Right-to-left (RTL (Right-to-Left)) support is a host responsibility, not a message-pipeline feature. The library
returns strings; the application decides the document direction, layout, fonts, and bidi isolation.
This chapter shows how to make that decision consistently across the supported hosts and how to avoid
the defects that appear when RTL is treated as "translate the text and reverse the layout".

Arabic, Hebrew, Persian, and Urdu are the common RTL languages, but do not infer direction from a
country code or a language name. Use the culture's text information:

```csharp
var culture = CultureInfo.CurrentUICulture;
var isRtl = culture.TextInfo.IsRightToLeft;
var direction = isRtl ? "rtl" : "ltr";
```

Use `CurrentUICulture` for text and direction, and `CurrentCulture` for numbers, dates, and currency.
The request-localization middleware normally sets both, but they can differ when the application
deliberately separates formatting from UI language.

## What the library does and does not do

- It resolves a template for a culture and formats placeholders.
- It preserves the characters in the template; it does not reverse, reshape, or reorder them.
- It does not set `dir`, `lang`, `FlowDirection`, or `RightToLeft`.
- It does not sanitize HTML or Markdown templates. Direction does not change that boundary.

The rest of this chapter is application code.

## HTML: `lang`, `dir`, and logical CSS

Set both attributes on the document, not only on a container:

```cshtml
@{
    var culture = CultureInfo.CurrentUICulture;
    var direction = culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";
}
<!DOCTYPE html>
<html lang="@culture.Name" dir="@direction">
<head>
    <meta charset="utf-8" />
    <title>@Localizer[ReferenceKeys.APPLICATION_TITLE]</title>
</head>
<body>
    @RenderBody()
</body>
</html>
```

`lang` describes the language; `dir` describes the base direction. Setting only `lang="ar"` does not
change layout, and setting only `dir="rtl"` does not give assistive technology the language.

For a user-generated block whose direction is unknown, `dir="auto"` lets the first strong character
decide. Keep explicit `rtl`/`ltr` for application chrome; guessing is appropriate for user content,
not for the UI frame.

Prefer CSS logical properties over left/right pairs:

```css
.nav-link {
    margin-inline-start: 1rem;
}

.price {
    text-align: end;
}

.card {
    padding-inline: 1rem;
    border-inline-start: 2px solid #ccc;
}
```

Replace `margin-left`, `padding-right`, `left`, `right`, and `text-align: left/right` with their
logical equivalents where the layout should mirror. Keep physical properties where the element must
not mirror, such as a chart axis or a code block.

## Bidi isolation for mixed-direction content

RTL layout does not automatically isolate an inserted value. A Latin order id, URL, file name, or
user name can pull the surrounding punctuation to the wrong side. Isolate the inserted value:

```cshtml
<p>
    @Localizer[ReferenceKeys.ORDER_ID_FIELD]:
    <bdi>@order.Id</bdi>
</p>
```

`<bdi>` is the HTML element for this job. For plain-text contexts (logs, email bodies, desktop
labels), use the Unicode isolate controls:

```csharp
private static string Isolate(string value) => $"\u2068{value}\u2069";
```

U+2068 (First Strong Isolate) starts the isolate and U+2069 (Pop Directional Isolate) ends it. Use it
around inserted values, not around the whole sentence. Test the result in the target renderer; some
terminals and older email clients do not handle isolate controls well.

Avoid `unicode-bidi: bidi-override` unless you are deliberately displaying a controlled value in a
fixed direction. It overrides the bidi algorithm and commonly produces reversed text.

## ASP.NET Core and MVC

The layout example above is the server-rendered path. Two additional rules matter:

- Resolve the request culture before the layout runs. The `UseRequestLocalization` middleware in the
  documented pipeline does this; the layout reads the resulting `CurrentUICulture`.
- Keep interpolated values isolated. A localized sentence with a Latin id in the middle still needs
  `<bdi>` around the id, even when the sentence itself is Arabic.

The reference MVC host already sets `lang` from the current culture; add `dir` in the same place
when you adopt this guidance. See
[ASP.NET Core and MVC](08-aspnet-core-and-mvc.md) for the request-localization and view-localizer
setup.

## Blazor Server

A Blazor layout can set `dir` on a wrapper, but the document element needs `lang` and `dir` as well
for correct browser behavior and assistive technology. One approach is a small JS helper:

```javascript
window.jeninnetSetDocumentCulture = (culture, direction) => {
    document.documentElement.lang = culture;
    document.documentElement.dir = direction;
};
```

Load the helper from the host page (or a JS module loaded at startup). Call it after the culture is
known and after a culture change:

```razor
@inject BlazorCultureService CultureService
@inject IJSRuntime JS

<div class="page" dir="@Direction" lang="@CultureName">
    @Body
</div>

@code {
    private CultureInfo Culture => CultureService.CultureInfo;

    private string CultureName => Culture.Name;

    private string Direction =>
        Culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            await JS.InvokeVoidAsync(
                "jeninnetSetDocumentCulture", CultureName, Direction);
        }
    }
}
```

`BlazorCultureService.CultureInfo` is the circuit's selected culture; the fallback keeps the first
render safe before initialization. Re-run the interop after a culture change if your layout does not
reload the document.

See [Blazor](09-blazor.md) for the circuit-scoped culture service, the cookie store, and the WASM (WebAssembly)
provider guard.

## Blazor Hybrid

Blazor Hybrid renders HTML inside the WebView, so the same `dir` and CSS rules apply. The reference
host sets direction on its root container:

```razor
@{
    var culture = CultureService.CultureInfo;
    var direction = culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";
}

<div class="page" dir="@direction" lang="@culture.Name">
    ...
</div>
```

The embedded host page (`wwwroot/index.html`) should also carry a sensible default `lang` and
`dir`; the root component can update them when the user switches culture. The
[Blazor Hybrid host](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Reference.BlazorHybrid/README.md) shows the
working structure.

## WinForms

Set direction on the form and reapply it after a culture switch:

```csharp
private void ApplyDirection() {
    var isRtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
    RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
    RightToLeftLayout = isRtl;
}
```

`RightToLeft` changes text alignment and control behavior. `RightToLeftLayout` mirrors the layout of
supported controls; test owner-drawn controls, images, and icons separately because they do not all
mirror automatically. The reference WinForms host applies `RightToLeft` in `ApplyCulture` and is a
working example.

## WPF and XAML

`FlowDirection` inherits down the visual tree:

```csharp
var isRtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
```

Set it on the `Window` or the root element. Layout mirrors, but images, icons, and custom drawing do
not. Mirror an arrow only when its meaning is directional; do not mirror numbers, charts, or logos.
For text alignment, prefer `TextAlignment` and flow-aware panels over hard-coded margins.

The reference WPF host uses ordinary bindings and does not invent a XAML markup extension for
localization; direction is application code in the same way.

## Formatting, placeholders, and translation

- **Use complete sentences.** Translating fragments and concatenating them breaks word order in every
  language and makes bidi bugs harder to see.
- **Preserve placeholders.** A translator may reorder `{0}`/`{1}` or named placeholders; the runtime
  substitutes them after translation. Dropping a placeholder is a defect, not a layout choice.
- **Isolate only the inserted value.** Apply `<bdi>` or Unicode isolates around ids, URLs, names, and
  other mixed-direction values, not around the whole translated sentence.
- **Format values with `CurrentCulture`.** A price or date inside an RTL sentence should use the
  culture's number/date formatting, not a manually rearranged string.
- **Do not reverse strings.** Reversing characters breaks Arabic shaping, numbers, and bidi runs.
  Let the platform's bidi algorithm and the host's layout do the work.
- **Keep direction in the host.** A message template has no direction metadata. One Arabic string
  can appear in an RTL page, an LTR (Left-to-Right) admin table, and a plain-text email; the host decides.

## Testing checklist

| Check | What to verify |
|---|---|
| Document attributes | `lang` matches the culture and `dir` is `rtl` for RTL cultures |
| Layout | Navigation, margins, borders, and alignment mirror with logical CSS |
| Mixed content | Ids, URLs, names, and numbers stay readable inside RTL sentences |
| Numbers and dates | Values use the intended culture's formatting, not reversed text |
| Icons and images | Directional icons mirror; logos, charts, and numbers do not |
| Forms | Labels, validation messages, and error summaries align and read correctly |
| Culture switch | Direction updates immediately after the switch, without a stale wrapper |
| Desktop hosts | `RightToLeft`/`RightToLeftLayout` or `FlowDirection` reapplied on culture change |
| Accessibility | Screen readers receive the correct `lang` and document direction |
| Visual regression | Arabic/Hebrew screenshots are reviewed, not only string assertions |

Use real Arabic and Hebrew content in tests, not pseudo-localization alone. Pseudo-localization helps
find layout overflow; it does not exercise bidi behavior.

## Pitfalls

**Pitfall:** setting `lang="ar"` and expecting the browser to switch direction. `lang` is metadata;
`dir` controls the base direction.

**Pitfall:** applying `text-align: right` everywhere. It breaks LTR content embedded in an RTL page
and ignores logical start/end semantics. Use `start`/`end` and logical properties.

**Pitfall:** wrapping the entire layout in `<bdi>`. Isolate inserted values, not whole pages.

**Pitfall:** reversing a string to "fix" Arabic. That destroys shaping and produces wrong numbers.

**Pitfall:** mirroring every icon. Only directional icons should mirror; a mirrored logo is a brand
defect.

**Pitfall:** forgetting the culture switch. A direction set only at startup leaves the page in the
old direction after the user changes language.

## Where to go next

- [Cultures and fallback](03-cultures-and-fallback.md) for how the request or host culture is chosen.
- [Formatting and plurals](04-formatting-and-plurals.md) for placeholders, ICU (International Components for Unicode), and number/date
  formatting.
- [ASP.NET Core and MVC](08-aspnet-core-and-mvc.md) for request localization and views.
- [Blazor](09-blazor.md) for circuit-scoped and static culture management.
- [Security](../04-operations/02-security.md) for trusted templates and output encoding.
- [Known limitations](../03-reference/10-known-limitations.md) for the boundary that direction is
  host-owned.
