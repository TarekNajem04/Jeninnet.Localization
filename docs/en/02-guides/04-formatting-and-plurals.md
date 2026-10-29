# Formatting and Plurals

Formatting runs after message resolution. This chapter covers the default placeholder formatter,
the opt-in ICU (International Components for Unicode) MessageFormat engine, the plural APIs, the HTML-safe and Markdown formatters, and the
performance characteristics that follow from those designs.

## The formatting stage

`DefaultMessageLocalizer` resolves a template first and calls the registered `IMessageFormatter`
only on a hit. A miss returns `null` and never invokes the formatter. The formatter receives the
template and the caller's parameters, not the key or the provider, which keeps it pure and testable:

```csharp
public string Format(string template, IReadOnlyDictionary<string, object?> parameters);
```

When the formatter implements `ICultureAwareMessageFormatter`, the localizer calls the overload
that also receives the requested culture. The default formatter does not; `IcuMessageFormatter`
does. That is how plural, number, and date rendering follow the requested culture even when the
template came from a parent or default culture.

## The default formatter

`DefaultMessageFormatter` scans for `{...}` spans and substitutes the dictionary value whose key
matches the text between the braces.

```text
Template:    "Hello, {name}! You have {count} items."
Parameters:  name = "Ada", count = 3
Result:      "Hello, Ada! You have 3 items."
```

Behavior details, verified in source:

- **Missing parameter:** the placeholder stays exactly as written.
- **Null or empty value:** substituted as an empty string.
- **Conversion:** `value?.ToString()`; there is no format string or `IFormattable` selection.
- **Matching:** the key is everything up to the next `}`. `{0}` is key `0`; `{name}` is key `name`.
- **Braces:** nested and escaped braces are not part of the contract.
- **Culture:** no culture parameter; `ToString()` uses ambient `CurrentCulture`.

The last point is the one that surprises people. If a template contains `{price}` and the value is
a `decimal`, the rendering follows `CurrentCulture`, not the culture passed to `LocalizeAsync`.
For culture-correct numbers and dates, use the ICU formatter or pre-format the argument and pass a
string.

**Pitfall:** the default formatter does not support composite format specifiers. In
`"Total: {0:N2}"` the key is `0:N2`, not `0`, so a dictionary entry named `0` will not match. The
placeholder stays literal. Use `IcuMessageFormatter` for `{value, number, ::.00}`-style formatting,
or pass a preformatted string.

## ICU MessageFormat

`IcuMessageFormatter` is an opt-in `ICultureAwareMessageFormatter`. Registration is the switch:
register it in place of the default formatter. The DI (Dependency Injection) setup uses `TryAddSingleton`, so a
pre-registered formatter wins:

```csharp
services.AddSingleton<IMessageFormatter>(new IcuMessageFormatter());
services.AddLocalization(options => options.DefaultCulture = "en");
```

A template without an ICU construct takes the exact default path through the inner formatter, so
existing content is unchanged. A template with `{arg, type, ...}` engages the ICU engine. Unknown
argument types also engage the engine and throw a descriptive `FormatException` rather than passing
through silently.

Supported constructs:

- `plural` with `=number` branches, `offset:N`, and CLDR (Common Locale Data Repository) cardinal categories; `selectordinal`;
  `select`.
- `number` (default, `integer`, `currency`, `percent`) and `date`/`time` (default through `full`).
- Nested messages, `#` in plural branches, and ICU apostrophe quoting (`''`, `'{...}'`).
- A `::` skeleton subset: `percent`, `currency/XXX`, fixed precision, and numeric date/time.

Malformed templates, unknown argument types, missing selector arguments, type mismatches, a missing
`other` branch, and duplicate selectors throw `FormatException` with the pattern offset. Missing
simple arguments keep the lenient pipeline contract: the placeholder is left as written.

### Worked example: ICU plural

```csharp
services.AddSingleton<IMessageFormatter>(new IcuMessageFormatter());
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["Cart.Items"] = "{count, plural, =0 {No items} one {# item} other {# items}}"
});

var localizer = provider.GetRequiredService<IMessageLocalizer>();
var parameters = new Dictionary<string, object?> { ["count"] = 3 };
var text = await localizer.LocalizeAsync("Cart.Items", parameters, new CultureInfo("en"));
// "3 items"
```

With `count = 0` the explicit `=0` branch wins and the result is `No items`. With `count = 1` the
`one` branch wins and `#` renders `1`. With `count = 1000` the `other` branch renders `1,000` in
English because `#` uses the requested culture's number formatting.

The same template in Arabic with `count = 3` selects the `few` category, so an Arabic template must
provide a `few` branch (and usually `zero`, `one`, `two`, `many`, and `other`). The engine selects
the category; the translator supplies the branches.

### Number and date formatting

Inside ICU, numeric and date values must have the right runtime type. `{amount, number, currency}`
requires a numeric value; `{when, date, long}` requires `DateTime`, `DateTimeOffset`, or `DateOnly`.
Passing a string throws `FormatException`. This strictness is intentional: silent string
concatenation would hide a type error until a translator or locale exposed it.

## Plural APIs

Two plural shapes exist.

### The key-pair API

`LocalizePluralAsync` takes a singular key and a plural key:

```csharp
var text = await localizer.LocalizePluralAsync(
    "Cart.One",
    "Cart.Other",
    count,
    new Dictionary<string, object?> { ["count"] = count },
    CultureInfo.CurrentUICulture);
```

The form is selected by `PluralRules.GetCardinalCategory(culture, count)`:

- `PluralCategory.One` selects the singular key.
- Every other category selects the plural key.

Each key resolves through the same culture-fallback chain as a normal lookup. The category is
computed in the requested culture even when the winning template comes from a parent or the default
culture. If the preferred key is missing, the localizer tries the other key; if neither exists, the
result is `null`. The count is not injected into the parameter dictionary, so templates that render
`{count}` require the caller to supply it.

For languages with more than two rendered forms, this pair API is a documented degradation: it
distinguishes `one` from "not one" and does not select `few`, `many`, or `two` templates
individually. Use ICU plural branches, or a provider that serves per-form plural tables.

### Per-form provider tables

If the resolved provider implements `IPluralFormMessageProvider`, the localizer first asks it for a
count-aware form. The PO (Portable Object) provider implements this when a catalog carries a `Plural-Forms` header.
The provider selects the exact `msgstr` index for the count and culture. If the selected form is
empty or the entry is missing, the localizer falls through to the key-pair path, so providers
without per-form support keep their documented behavior.

The provider remains exact-match per culture. The localizer calls the provider once per fallback
step, exactly as it does for ordinary lookups.

## Plural category coverage

`PluralRules.GetCardinalCategory` is a hand-ported CLDR subset covering Germanic, Romance, Arabic,
Slavic, Baltic, and Celtic languages, Hebrew, Maltese, and several single-form languages. Negative
values use magnitude; unknown languages resolve to `Other`. Ordinals cover English only.

Documented limitations: matching is by two-letter language, so `pt-PT` follows the `pt` rules rather
than the region-specific Catalan-like rules; compact-notation operands (`c`/`e`) are out of scope;
languages outside the table resolve to `Other`.

**Pitfall:** `PluralCategory.Other` does not mean "plural". It means "none of the other categories".
For English it is the plural form; for Arabic it is a distinct category. Author every category the
target template selects.

## HTML-safe formatting

`HtmlSafeMessageFormatter` passes trusted template markup through and HTML-encodes argument values
with `HtmlEncoder.Default`; numeric, boolean, and date/time values pass by identity so ICU keeps
working. It does not sanitize templates. The output is already-encoded HTML and must be emitted raw;
a normal HTML-encoding sink would double-encode it.

## Markdown formatting

`MarkdownMessageFormatter` renders a documented CommonMark subset first, then hands the HTML to an
inner formatter for substitution. Rendering first means argument values are never parsed as
Markdown or HTML. The default inner is `HtmlSafeMessageFormatter` (itself defaulting to
`DefaultMessageFormatter`), so `new MarkdownMessageFormatter()` carries the already-encoded
guarantee. Compose explicitly for ICU or pre-encoded arguments:

```csharp
new MarkdownMessageFormatter(new HtmlSafeMessageFormatter(new IcuMessageFormatter()));
new MarkdownMessageFormatter(new DefaultMessageFormatter());
```

Blocks: paragraphs and simple ordered/unordered lists. Inlines: emphasis, strong, single-backtick
code spans, links, and backslash escapes. Constructs outside the subset render literally. Raw HTML
is inert unless `allowRawHtml` is enabled. Output is HTML and must be emitted raw; link destinations
are attribute-encoded, but scheme vetting remains the author's responsibility.

## Escaping rules at a glance

| Context | Rule |
|---|---|
| Default placeholder | `{key}`; unknown keys remain literal; no format specifiers |
| ICU | `''` for a literal apostrophe; `'{...}'` for literal text; `#` inside plural |
| Markdown template | Backslash escapes for `` \ ` * _ [ ] ( ) ``; raw HTML inert by default |
| HTML argument | Encoded by `HtmlSafeMessageFormatter`; templates trusted |
| Markdown output | Already HTML; emit raw, do not HTML-encode again |

## Performance notes

- The default formatter uses a compiled regex and allocates one result string per call.
- ICU parses only when a construct is present; complex templates parse per call.
- Markdown allocates a builder and line array per call and does not cache rendered templates.
- Fallback cost is dominated by provider I/O; in-process dictionaries are cheap.
- Diagnostics hooks are listener-guarded, so unobserved hosts pay only guard checks.

For hot template families, cache the formatted string at the call site with a culture key. Do not
cache inside a shared formatter; it has no invalidation signal and parameters vary per call.

## Where to go next

- [Cultures and fallback](03-cultures-and-fallback.md) for how the requested culture reaches the
  formatter.
- [Machine translation](12-machine-translation.md) for how translated templates interact with
  formatting and caching.
- [Performance](../04-operations/03-performance.md) for measurement and capacity planning.
