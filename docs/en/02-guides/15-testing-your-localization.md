# Testing Your Localization

Localization tests fail in two directions. They can pass while the application is untranslated, and
they can fail because a test leaked `CurrentUICulture` into the next one. A useful suite checks the
lookup algorithm, the catalog contract, and the culture boundary separately.

The repository's suite is a single MSTest project using `Jeninnet.Assertions` for `Should()`
assertions. The patterns below work with any async-capable test framework.

## Test the algorithm, not only the result

A fallback test that asserts only the returned string can pass for the wrong reason. Assert the exact
sequence of provider queries instead:

```csharp
sealed class RecordingProvider(IReadOnlyDictionary<string, string> templates) : IMessageProvider {
    public List<string> QueriedCultures { get; } = [];
    public ValueTask<string?> GetMessageTemplateAsync(
        string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        QueriedCultures.Add(culture.Name);
        return ValueTask.FromResult(
            templates.TryGetValue($"{culture.Name}:{key}", out var value) ? value : null);
    }
}

var provider = new RecordingProvider(new Dictionary<string, string> { ["de:Greeting"] = "Hallo" });
var localizer = new DefaultMessageLocalizer(provider, new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "en" });

await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("de-CH"));
provider.QueriedCultures.Should().Equal("de-CH", "de");
```

Cover the six cases that pin the only fallback algorithm: exact hit, multi-level parent walk,
invariant exclusion, default-culture reachability, total miss returning `null`, and the duplicated
default query when the default equals the requested culture. Provider tests then only need to prove
exact-match behavior.

## Fake providers and stores

Use `MemoryMessageProvider` for a flat static map. Use a recording provider when the test must observe
queries. Use `CultureAwareMessageProvider` when the test needs exact-name-then-two-letter dispatch.

For store tests, implement `ILocalizationStore` directly. The important detail is the synchronous
probe: `CustomStringLocalizerFactory` and `StoreBackedStringLocalizer` call `TryGetResourceSet`. A fake
that throws from that member because "the async path is the real one" makes every `IStringLocalizer`
test fail in a confusing way. Keep a dictionary-backed fake and implement `TryGetResourceSet`,
`TryGetString`, and the async members over the same map.

If a test needs administration, implement a small `IResourceAdminStore` fake rather than reaching for
the EF store. The EF store's runtime tests are environment-gated because they need a real SQL Server.

## Culture-independent tests

Pass the culture explicitly. `LocalizeAsync(key, parameters, culture)` has no ambient dependency, and
most tests should use it.

When a test must use the convenience overload, set the ambient culture and restore it in `finally`:

```csharp
var previous = CultureInfo.CurrentUICulture;
try {
    CultureInfo.CurrentUICulture = new CultureInfo("ar-SY");
    (await localizer.LocalizeAsync("key", parameters)).Should().BeNull();
} finally { CultureInfo.CurrentUICulture = previous; }
```

Tests that mutate ambient culture should not run in parallel with tests that read it. The repository
marks them `[DoNotParallelize]`. Do not rely on test ordering.

Do not assert a `DefaultMessageFormatter` number against a fixed string unless the test passes the
culture explicitly. The formatter uses `value?.ToString()` and ambient `CurrentCulture` for numbers
and dates. A test that passes in `en-US` and fails on a machine with `de-DE` is testing the machine.

## `IStringLocalizer` bridge tests

```csharp
var localizer = new CustomStringLocalizerFactory(store).Create(typeof(HomeController));
localizer["Missing"].Value.Should().Be("[MISSING:Missing]");
localizer["Missing"].ResourceNotFound.Should().BeTrue();
localizer.GetAllStrings(includeParentCultures: false).Select(s => s.Name)
    .Should().NotContain("Missing");
```

Cover type-name mapping, exact-culture capture at creation, re-probe on every lookup, the missing
marker, enumeration excluding missing entries, composite formatting (`{0}`), and a missing store
composition failing as a configuration error rather than a miss.

The generic wrapper is transient. Test it through DI (Dependency Injection) when the test is about composition; test the
factory directly when it is about mapping.

## Pseudo-localization and missing keys

Pseudo-localization catches hard-coded strings and short layouts. Use the Pseudo provider and assert
the deterministic shape:

```csharp
var pseudo = new PseudoMessageProvider(new PseudoLocalizationOptions {
    SourceTemplates = new Dictionary<string, string> { ["Title"] = "Bookstore" }
});
var localizer = new DefaultMessageLocalizer(
    pseudo, new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "qps-ploc" });

var text = await localizer.LocalizeAsync(
    "Title", new Dictionary<string, object?>(), new CultureInfo("qps-ploc"));
text.Should().StartWith("[!! ").And.EndWith(" !!]");
```

Missing-key sinks are testable without infrastructure. Cover deduplication by `(key, culture)`, the
cap and `DroppedCount`, containment of a throwing sink, and the off-state behavior.

```csharp
var collector = new MissingKeyCollector(maxEntries: 10);
var localizer = new DefaultMessageLocalizer(
    provider, new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "en", MissingKeySink = collector });

await localizer.LocalizeAsync("Absent", parameters, new CultureInfo("fr"));
collector.GetSnapshot().Should().ContainSingle();
collector.GetSnapshot()[0].HitCount.Should().Be(1);
```

## Parity assertions

The parity engine is internal to the command-line tool. Test it the way the repository does: run the
built tool out-of-process against a fixture copy and assert the exit code, console output, and JSON
report.

```csharp
var run = await RunToolAsync(fixture,
    ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

run.ExitCode.Should().Be(1);
run.Stdout.Should().Contain("[missing-key] Cart.Add [ar]");
```

Useful fixtures: clean (exit `0`), removed key resolving through the default (`fallback`, exit `0`),
removed key with no fallback (`missing-key`, exit `1`), two formats with different templates
(`template-drift`), a key absent from one serving format (`format-gap`), a malformed catalog
(`catalog-error` with a line), an exclusion that silences a finding, and a placeholder drop
(`placeholder-drift` with the missing identifier).

Snapshot-style checks work because the output is deterministic. Normalize line endings and keep
fixture paths relative so the golden text is stable.

## Interchange round trips

Test the strong invariant directly: a no-change round trip is byte-identical.

1. Copy source and target catalogs into a temporary directory.
2. Export to PO (Portable Object), CSV, ResX, or XLIFF (XML Localization Interchange File Format).
3. Import with `DryRun = true`; assert nothing changed.
4. Import normally; assert no `Updated` entries and unchanged target bytes.
5. Change one target string, import, and assert only that key is `Updated`.

Also test the failure boundary: a malformed document throws the format-specific exception with a file
and line, and the target file is not rewritten. A dry run never writes, even when there are updates.

## Diagnostics tests

Activity assertions use an in-memory listener:

```csharp
var activities = new List<Activity>();
using var listener = new ActivityListener {
    ShouldListenTo = source => source.Name == LocalizationDiagnostics.SOURCE_NAME,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activities.Add,
};
ActivitySource.AddActivityListener(listener);

await localizer.LocalizeAsync("heading", parameters, new CultureInfo("fr-CA"));
activities.Single(a => a.OperationName == "localization.lookup")
    .GetTagItem("localization.culture").Should().Be("fr-CA");
```

Assert that template text never appears in any tag, that fallback steps are children of the lookup,
and that a faulted lookup records no hit or miss. For metrics, use `MeterListener` and filter by
instrument name.

## Worked example: a regression suite for one plural message

A team adds a plural message. The test set covers the failure modes:

1. Singular count in English selects the singular template.
2. Count zero in Arabic selects the zero category, not the singular.
3. A missing preferred key falls back to the other key.
4. A missing pair returns `null` and reports both keys to the sink.
5. The provider is queried with the requested culture first, then its parents.
6. The ICU (International Components for Unicode) formatter renders `#` with the requested culture.
7. The parity fixture contains the key in every served culture and passes placeholder comparison.

All tests use explicit cultures, a recording provider, and a deterministic formatter. None depends on
the machine's locale or on test ordering.

## Pitfalls

**Pitfall:** asserting only the returned string in a fallback test. It can hide a wrong query chain.

**Pitfall:** forgetting `TryGetResourceSet` in a fake store. The bridge uses the synchronous probe.

**Pitfall:** leaking `CurrentUICulture` between tests. Restore it in `finally` and serialize ambient
culture tests.

**Pitfall:** comparing a `DefaultMessageFormatter` number against a fixed string. It uses ambient
`CurrentCulture`.

**Pitfall:** testing a singleton `IStringLocalizer` across cultures. It captured the culture when it
was created; create a new localizer for the new culture.

**Pitfall:** using the production store in a unit test. A small fake is faster and needs no database.

**Pitfall:** treating a green unit test suite as translation coverage. Run the parity tool as well.

## Where to go next

- [Parity and quality gates](13-parity-and-quality-gates.md) for the CI check and report schema.
- [Cultures and fallback](03-cultures-and-fallback.md) for the chain the tests should pin.
- [Diagnostics and observability](14-diagnostics-and-observability.md) for the names to assert.
