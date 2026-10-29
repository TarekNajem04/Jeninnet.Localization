//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Parity CLI tests: the built tool runs out-of-process against fixture
/// copies (exit-code fidelity), seeded drift is reported with file/line precision,
/// the JSON report is schema-versioned and deterministic, and the documented
/// dogfood command over this repository is green.
/// </summary>
[TestClass]
public sealed class ParityToolTests {
    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Verifies that clean fixture exit zero with golden console.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task CleanFixture_ExitZero_WithGoldenConsoleAsync() {
        var fixture = CopyCleanFixture();
        try {
            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(0);
            run.Stdout.Should().Be(Normalize(
                """
                jeninnet-localization-keys 1.0.0.0
                catalogs: 2 file(s), 2 culture(s), 1 format(s)
                sources: 2 file(s), 2 reference(s), 2 distinct key(s)
                definitions: 2 constant(s)
                excluded: 0 key(s)
                findings: 0
                summary: missing-key=0, unused-key=0, template-drift=0, format-gap=0, placeholder-drift=0, unresolved-reference=0, definition-conflict=0, catalog-error=0
                result: PASS (exit 0)
                """ + "\n"));
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that seeded drift reports with file line and exit one.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task SeededDrift_ReportsWithFileLine_AndExitOneAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "en.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <messages>
                  <App.Title>Title</App.Title>
                  <Cart.Add>DIFFERENT</Cart.Add>
                </messages>

                """,
                Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Be(Normalize(
                """
                jeninnet-localization-keys 1.0.0.0
                catalogs: 3 file(s), 2 culture(s), 2 format(s)
                sources: 2 file(s), 2 reference(s), 2 distinct key(s)
                definitions: 2 constant(s)
                excluded: 0 key(s)
                findings: 1
                  [template-drift] Cart.Add [en] catalogs/en.json:3 (+1 more) - Key 'Cart.Add' has differing templates for culture 'en' (json vs xml).
                summary: missing-key=0, unused-key=0, template-drift=1, format-gap=0, placeholder-drift=0, unresolved-reference=0, definition-conflict=0, catalog-error=0
                result: FAIL (exit 1)
                """ + "\n"));
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that removed key missing only when fallback chain empty.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task RemovedKey_MissingOnlyWhenFallbackChainEmptyAsync() {
        var fixture = CopyCleanFixture();
        try {
            // Removed from ar only: en still serves it, so the fallback chain covers ar.
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "ar.json"),
                """
                {
                  "App.Title": "Title-AR"
                }

                """,
                Cancellation);

            var fallback = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en", "--report", "fallback.json"]);

            fallback.ExitCode.Should().Be(0, "fallback through the default culture is not a finding");
            using(var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture, "fallback.json"), Cancellation))) {
                var matrix = report.RootElement.GetProperty("matrix");
                var cart = matrix.EnumerateArray().First(static row => row.GetProperty("key").GetString() == "Cart.Add");
                var states = cart.GetProperty("cultures").EnumerateArray().ToDictionary(
                    static cell => cell.GetProperty("culture").GetString()!,
                    static cell => cell.GetProperty("state").GetString()!,
                    StringComparer.Ordinal);
                states["en"].Should().Be("served");
                states["ar"].Should().Be("fallback");
            }

            // Removed everywhere: missing in every culture.
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "en.json"),
                """
                {
                  "App.Title": "Title"
                }

                """,
                Cancellation);

            var missing = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            missing.ExitCode.Should().Be(1);
            missing.Stdout.Should().Contain("[missing-key] Cart.Add [ar] src/Consumer.cs:15 - Key 'Cart.Add' is referenced in source but not served for culture 'ar' (chain: ar, en).");
            missing.Stdout.Should().Contain("[missing-key] Cart.Add [en] src/Consumer.cs:15 - Key 'Cart.Add' is referenced in source but not served for culture 'en' (chain: en).");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that extra key reports unused and format gap.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task ExtraKey_ReportsUnused_AndFormatGapAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "en.json"),
                """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Old.Key": "Old"
                }

                """,
                Cancellation);
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "en.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <messages>
                  <App.Title>Title</App.Title>
                  <Cart.Add>Add</Cart.Add>
                </messages>

                """,
                Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("[unused-key] Old.Key [*] catalogs/en.json:4 - Key 'Old.Key' is defined in 1 culture(s) but never referenced in source.");
            run.Stdout.Should().Contain("[format-gap] Old.Key [en] catalogs/en.xml:1 - Key 'Old.Key' is served for culture 'en' but missing from the xml catalog.");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that malformed catalog reports catalog error.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task MalformedCatalog_ReportsCatalogErrorAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), "{ broken", Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("[catalog-error] catalogs/ar.json [ar] catalogs/ar.json:1 - Catalog 'catalogs/ar.json' could not be read: Invalid JSON:");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that exclusions silence seeded drift.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task Exclusions_SilenceSeededDriftAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "en.json"),
                """
                {
                  "App.Title": "Title"
                }

                """,
                Cancellation);
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "ar.json"),
                """
                {
                  "App.Title": "Title-AR"
                }

                """,
                Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en", "--exclude", "Cart.Add"]);

            run.ExitCode.Should().Be(0);
            run.Stdout.Should().Contain("excluded: 1 key(s)");
            run.Stdout.Should().Contain("findings: 0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that unresolved reference is reported.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task UnresolvedReference_ReportedAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "src", "Broken.cs"),
                """
                public static class Broken {
                    public static string X => SampleKeys.NOPE;
                }

                """,
                Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("[unresolved-reference] src/Broken.cs [*] src/Broken.cs:2 - Reference 'NOPE' has no constant definition.");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that definition conflict is reported.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task DefinitionConflict_ReportedAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "src", "OtherKeys.cs"),
                """
                public static class OtherKeys {
                    public const string APP_TITLE = "Different";
                }

                """,
                Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("[definition-conflict]");
            run.Stdout.Should().Contain("APP_TITLE");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that placeholder drift reports with file line culture and exit one.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task PlaceholderDrift_ReportsWithFileLineCulture_AndExitOneAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Greeting": "Hello {0}, you have {1} item(s)."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Greeting": "مرحبا، لديك {0}."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Greeting.cs"), """
                public static class Greeting {
                    public static string Text(dynamic localizer) => localizer["Messages.Greeting"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en", "--report", "placeholder.json"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("[placeholder-drift] Messages.Greeting [ar] catalogs/ar.json:4 - Key 'Messages.Greeting' has placeholder drift for culture 'ar': missing {1} (reference culture 'en' uses placeholders {0, 1}).");

            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixture, "placeholder.json"), Cancellation));
            var findings = report.RootElement.GetProperty("findings");
            findings.GetArrayLength().Should().Be(1);
            findings[0].GetProperty("kind").GetString().Should().Be("placeholder-drift");
            findings[0].GetProperty("key").GetString().Should().Be("Messages.Greeting");
            findings[0].GetProperty("culture").GetString().Should().Be("ar");
            report.RootElement.GetProperty("summary").GetProperty("placeholderDrifts").GetInt32().Should().Be(1);
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that placeholder added in one culture is reported as extra.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task PlaceholderAddedInOneCulture_IsReportedAsExtraAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Greeting": "Hello {0}."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Greeting": "مرحبا {0} {1}."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Greeting.cs"), """
                public static class Greeting {
                    public static string Text(dynamic localizer) => localizer["Messages.Greeting"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("extra {1} (reference culture 'en' uses placeholders {0}).");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that placeholder reorder is not drift.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task PlaceholderReorder_IsNotDriftAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Name": "{first} {last}"
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Name": "{last} {first}"
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Name.cs"), """
                public static class Name {
                    public static string Text(dynamic localizer) => localizer["Messages.Name"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(0, "named-argument reordering is not placeholder drift");
            run.Stdout.Should().Contain("findings: 0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that icu plural branches are not drift.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task IcuPluralBranches_AreNotDriftAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Items": "{n, plural, one {# item} other {# items}}"
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Items": "{n, plural, zero {# عنصر} one {# عنصر} two {# عنصران} few {# عناصر} many {# عنصراً} other {# عنصر}}"
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Items.cs"), """
                public static class Items {
                    public static string Text(dynamic localizer) => localizer["Messages.Items"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(0, "ICU branch-internal placeables are conditional and never count as drift");
            run.Stdout.Should().Contain("findings: 0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that icu outer argument name drift is reported.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task IcuOuterArgumentNameDrift_IsReportedAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Items": "{n, plural, one {# item} other {# items}}"
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Items": "{count, plural, one {# عنصر} other {# عناصر}}"
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Items.cs"), """
                public static class Items {
                    public static string Text(dynamic localizer) => localizer["Messages.Items"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(1);
            run.Stdout.Should().Contain("missing {n}, extra {count} (reference culture 'en' uses placeholders {n}).");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that placeholder exclusion silences drift.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task PlaceholderExclusion_SilencesDriftAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Greeting": "Hello {0}."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Greeting": "مرحبا."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Greeting.cs"), """
                public static class Greeting {
                    public static string Text(dynamic localizer) => localizer["Messages.Greeting"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en", "--exclude", "ar:Messages.Greeting"]);

            run.ExitCode.Should().Be(0);
            run.Stdout.Should().Contain("excluded: 1 key(s)");
            run.Stdout.Should().Contain("findings: 0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that empty template is not is compared.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task EmptyTemplate_IsNotComparedAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Greeting": "Hello {0}."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "ar.json"), """
                {
                  "App.Title": "Title-AR",
                  "Cart.Add": "Add-AR",
                  "Messages.Greeting": ""
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Greeting.cs"), """
                public static class Greeting {
                    public static string Text(dynamic localizer) => localizer["Messages.Greeting"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(0, "empty templates are untranslated and are not compared");
            run.Stdout.Should().Contain("findings: 0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that fallback culture is not is compared.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task FallbackCulture_IsNotComparedAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(Path.Combine(fixture, "catalogs", "en.json"), """
                {
                  "App.Title": "Title",
                  "Cart.Add": "Add",
                  "Messages.Greeting": "Hello {0}."
                }

                """, Cancellation);
            await File.WriteAllTextAsync(Path.Combine(fixture, "src", "Greeting.cs"), """
                public static class Greeting {
                    public static string Text(dynamic localizer) => localizer["Messages.Greeting"];
                }

                """, Cancellation);

            var run = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

            run.ExitCode.Should().Be(0, "a culture resolving through the fallback chain is never compared");
            run.Stdout.Should().Contain("findings: 0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that json report schema versioned and deterministic.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task JsonReport_SchemaVersioned_AndDeterministicAsync() {
        var fixture = CopyCleanFixture();
        try {
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "catalogs", "en.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <messages>
                  <App.Title>Title</App.Title>
                  <Cart.Add>DIFFERENT</Cart.Add>
                </messages>

                """,
                Cancellation);

            var first = await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en", "--report", "first.json"]);
            await RunToolAsync(fixture, ["--catalog", "catalogs", "--source", "src", "--default-culture", "en", "--report", "second.json"]);

            first.ExitCode.Should().Be(1);
            var firstBytes = await File.ReadAllBytesAsync(Path.Combine(fixture, "first.json"), Cancellation);
            var secondBytes = await File.ReadAllBytesAsync(Path.Combine(fixture, "second.json"), Cancellation);
            firstBytes.SequenceEqual(secondBytes).Should().BeTrue("the JSON report is byte-identical across runs");

            using var report = JsonDocument.Parse(Encoding.UTF8.GetString(firstBytes));
            var root = report.RootElement;
            root.GetProperty("schemaVersion").GetInt32().Should().Be(2);
            root.GetProperty("tool").GetString().Should().Be("jeninnet-localization-keys");
            root.GetProperty("defaultCulture").GetString().Should().Be("en");
            var findings = root.GetProperty("findings");
            findings.GetArrayLength().Should().Be(1);
            findings[0].GetProperty("kind").GetString().Should().Be("template-drift");
            findings[0].GetProperty("key").GetString().Should().Be("Cart.Add");
            findings[0].GetProperty("culture").GetString().Should().Be("en");
            var summary = root.GetProperty("summary");
            summary.GetProperty("total").GetInt32().Should().Be(1);
            summary.GetProperty("templateDrifts").GetInt32().Should().Be(1);
            summary.GetProperty("placeholderDrifts").GetInt32().Should().Be(0);
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that usage error exit two.
    /// </summary>
    [TestMethod]
    [Timeout(300000, CooperativeCancellation = true)]
    public async Task UsageError_ExitTwoAsync() {
        var fixture = CopyCleanFixture();
        try {
            var empty = await RunToolAsync(fixture, []);
            empty.ExitCode.Should().Be(2);

            var missing = await RunToolAsync(fixture, ["--catalog", "nope", "--source", "src"]);
            missing.ExitCode.Should().Be(2);

            var version = await RunToolAsync(fixture, ["--version"]);
            version.ExitCode.Should().Be(0);
            version.Stdout.Trim().Should().Be("1.0.0.0");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that dogfood reference corpus is clean.
    /// </summary>
    [TestMethod]
    [Timeout(600000, CooperativeCancellation = true)]
    public async Task Dogfood_ReferenceCorpus_IsCleanAsync() {
        var root = RepositoryRoot();
        var arguments = new List<string> {
            "--catalog", Path.Combine(root, "samples", "Content", "Localization"),
        };

        foreach(var sourceRoot in Directory.GetDirectories(Path.Combine(root, "samples"), "Jeninnet.Localization.*")
            .Where(static directory => !string.Equals(Path.GetFileName(directory), "Jeninnet.Localization.Docs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static directory => directory, StringComparer.Ordinal)) {
            arguments.Add("--source");
            arguments.Add(sourceRoot);
        }

        arguments.AddRange([
            "--exclude", "Demo.MissingEverywhere",
            "--exclude", "Orders.Fields.OrderId",
            "--exclude", "Orders.Fields.Total",
            "--exclude", "Host.App.Title",
            "--exclude", "Host.Welcome",
            "--exclude", "Host.ChooseCulture",
            "--exclude", "Host.CurrentCulture",
            "--exclude", "Host.Direction",
            "--exclude", "Host.Direction.LeftToRight",
            "--exclude", "Host.Direction.RightToLeft",
            "--default-culture", "en",
        ]);

        var run = await RunToolAsync(root, [.. arguments]);

        run.ExitCode.Should().Be(0, $"the documented dogfood command must pass:{Environment.NewLine}{run.Stdout}");
        run.Stdout.Should().Contain("findings: 0");
    }

    /// <summary>
    /// Performs the copy clean fixture operation.
    /// </summary>
    /// <returns>The copy clean fixture text.</returns>
    private static string CopyCleanFixture() {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "tests", "Fixtures", "ParityTool", "clean");
        var target = Path.Combine(Path.GetTempPath(), "jlnparity-" + Guid.NewGuid().ToString("N"));

        CopyDirectory(source, target);
        return target;
    }

    /// <summary>
    /// Performs the copy directory operation.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="target">The destination.</param>
    private static void CopyDirectory(string source, string target) {
        Directory.CreateDirectory(target);
        foreach(var file in Directory.GetFiles(source)) {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach(var directory in Directory.GetDirectories(source)) {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    /// <summary>
    /// Performs the repository root operation.
    /// </summary>
    /// <returns>The repository root text.</returns>
    private static string RepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))) {
            directory = directory.Parent;
        }

        if(directory is null) {
            throw new InvalidOperationException("repository root could not be located from the test output directory");
        }

        return directory.FullName;
    }

    /// <summary>
    /// Performs the tool dll operation.
    /// </summary>
    /// <returns>The tool dll text.</returns>
    private static string ToolDll() {
        var configuration = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
#if NET8_0
        const string toolFramework = "net8.0";
#else
        const string toolFramework = "net10.0";
#endif
        return Path.Combine(RepositoryRoot(), "src", "tooling", "Jeninnet.Localization.Tools.Keys", "bin", configuration, toolFramework, "Jeninnet.Localization.Tools.Keys.dll");
    }

    /// <summary>
    /// Runs tool.
    /// </summary>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>A task whose result is tool.</returns>
    private static async Task<ToolRun> RunToolAsync(string workingDirectory, string[] arguments) {
        var start = new ProcessStartInfo("dotnet", $"\"{ToolDll()}\" {string.Join(" ", arguments.Select(static argument => $"\"{argument}\""))}") {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(start) ?? throw new InvalidOperationException("could not start the parity tool process");
        var stdout = new StringBuilder();
        process.OutputDataReceived += (_, args) => {
            if(args.Data is not null) {
                stdout.AppendLine(args.Data);
            }
        };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, args) => {
            if(args.Data is not null) {
                stderr.AppendLine(args.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync().ConfigureAwait(false);
        return new ToolRun(process.ExitCode, Normalize(stdout.ToString()), Normalize(stderr.ToString()));
    }

    /// <summary>
    /// Performs the normalize operation.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalize text.</returns>
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Deletes quietly.
    /// </summary>
    /// <param name="directory">The directory.</param>
    private static void DeleteQuietly(string directory) {
        try {
            Directory.Delete(directory, recursive: true);
        }
        catch(IOException) {
            // Best effort: temp copies must never fail the test run.
        }
        catch(UnauthorizedAccessException) {
            // Best effort: temp copies must never fail the test run.
        }
    }

    /// <summary>
    /// Represents a tool run.
    /// </summary>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="stdout">The captured standard output.</param>
    /// <param name="stderr">The captured standard error.</param>
    private sealed class ToolRun(int exitCode, string stdout, string stderr) {
        /// <summary>
        /// Gets the exit code.
        /// </summary>
        public int ExitCode { get; } = exitCode;

        /// <summary>
        /// Gets the stdout.
        /// </summary>
        public string Stdout { get; } = stdout;

        /// <summary>
        /// Gets the stderr.
        /// </summary>
        public string Stderr { get; } = stderr;
    }
}
