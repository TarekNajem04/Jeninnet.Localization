//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// PO (Portable Object) file interchange: standard-shape export, round-trip fidelity
/// (escapes, contexts, plurals, notes, headers), runtime-provider interop with the
/// / plural vectors, the status subset (Skipped/UnknownKey/UnsupportedKey/
/// Unchanged/Updated), dry-run purity, malformed diagnostics, and byte-identical
/// no-change catalog round-trips (including the committed sample PO).
/// </summary>
[TestClass]
public sealed class PoInterchangeTests {
    private const string ARABIC_PLURAL_FORMS = "nplurals=6; plural=n==0 ? 0 : n==1 ? 1 : n==2 ? 2 : n%100>=3 && n%100<=10 ? 3 : n%100>=11 ? 4 : 5;";

    private static readonly Dictionary<string, string?> _source = new() {
        ["Hello"] = "Hello",
        ["Bye"] = "Bye",
        ["Open"] = "Open",
    };

    private static readonly Dictionary<string, string?> _target = new() {
        ["Hello"] = "Bonjour",
        ["Bye"] = "Au revoir",
    };

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    // ------------------------------------------------------------------- export

    /// <summary>
    /// Verifies that export produces the standard shape.
    /// </summary>
    [TestMethod]
    public void Export_ProducesTheStandardShape() {
        var catalog = new Dictionary<string, string?> {
            ["book"] = "livre",
            ["A"] = "Alpha",
            ["menu\u0004Open"] = "Ouvrir",
        };
        var plurals = new List<PoPluralEntry> {
            new() {
                SingularKey = "book",
                PluralKey = "books",
                Forms = ["livre", "livres"],
            },
        };
        var notes = new Dictionary<string, string> { ["A"] = "Friendly tone." };

        var text = PoCatalogInterchange.ExportCatalog(catalog, "ar", plurals, notes, "nplurals=2; plural=(n != 1);");

        var expected = string.Join(
            "\n",
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "\"Language: ar\\n\"",
            "\"Plural-Forms: nplurals=2; plural=(n != 1);\\n\"",
            "",
            "#. Friendly tone.",
            "msgid \"A\"",
            "msgstr \"Alpha\"",
            "",
            "msgid \"book\"",
            "msgid_plural \"books\"",
            "msgstr[0] \"livre\"",
            "msgstr[1] \"livres\"",
            "",
            "msgctxt \"menu\"",
            "msgid \"Open\"",
            "msgstr \"Ouvrir\"");
        text.Should().Be(expected, "standard header, ordinal ordering, grouped pair keys excluded from the flat entries, no trailing newline");
    }

    /// <summary>
    /// Verifies that export omitted plural forms header is absent.
    /// </summary>
    [TestMethod]
    public void Export_OmittedPluralFormsHeader_IsAbsent() {
        var text = PoCatalogInterchange.ExportCatalog(_target, "ar");
        var document = PoCatalogInterchange.ParseDocument(text, "catalog.po");

        document.Headers.ContainsKey("Plural-Forms").Should().BeFalse("the header is written only when supplied (never fabricated)");
        document.Headers["Language"].Should().Be("ar");
        document.Headers["Content-Type"].Should().Be("text/plain; charset=UTF-8");
    }

    /// <summary>
    /// Verifies that export validates arguments.
    /// </summary>
    [TestMethod]
    public void Export_ValidatesArguments() {
        Action nullCatalog = () => PoCatalogInterchange.ExportCatalog(null!, "ar");
        Action emptyCulture = () => PoCatalogInterchange.ExportCatalog(_target, string.Empty);
        Action emptyKey = () => PoCatalogInterchange.ExportCatalog(new Dictionary<string, string?> { [string.Empty] = "x" }, "ar");
        Action nullValue = () => PoCatalogInterchange.ExportCatalog(new Dictionary<string, string?> { ["Key"] = null }, "ar");
        Action badHeader = () => PoCatalogInterchange.ExportCatalog(_target, "ar", pluralForms: "nplurals=2");
        Action badNPlurals = () => PoCatalogInterchange.ExportCatalog(_target, "ar", pluralForms: "nplurals=x; plural=(n != 1);");
        Action noExpression = () => PoCatalogInterchange.ExportCatalog(_target, "ar", pluralForms: "nplurals=2; plural=");
        Action emptyForms = () => PoCatalogInterchange.ExportCatalog(_target, "ar", pluralEntries: [new PoPluralEntry { SingularKey = "a", PluralKey = "b", Forms = [] }]);

        nullCatalog.Should().Throw<ArgumentNullException>();
        emptyCulture.Should().Throw<ArgumentException>();
        emptyKey.Should().Throw<ArgumentException>();
        nullValue.Should().Throw<ArgumentException>();
        badHeader.Should().Throw<ArgumentException>();
        badNPlurals.Should().Throw<ArgumentException>();
        noExpression.Should().Throw<ArgumentException>();
        emptyForms.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------- round-trip

    /// <summary>
    /// Verifies that round trip preserves escapes contexts plurals and notes.
    /// </summary>
    [TestMethod]
    public void RoundTrip_PreservesEscapesContextsPluralsAndNotes() {
        var catalog = new Dictionary<string, string?> {
            ["Tricky"] = "say \"hi\"\r\nline two\tend \\ done",
            ["menu\u0004Open"] = "Ouvrir",
        };
        var plurals = new List<PoPluralEntry> {
            new() {
                SingularKey = "book",
                PluralKey = "books",
                Context = "shelf",
                Forms = ["livre", "livres"],
            },
        };
        var notes = new Dictionary<string, string> { ["Tricky"] = "Line one.\nLine two." };

        var text = PoCatalogInterchange.ExportCatalog(catalog, "ar", plurals, notes, "nplurals=2; plural=(n != 1);");
        var document = PoCatalogInterchange.ParseDocument(text, "catalog.po");

        document.Headers["Plural-Forms"].Should().Be("nplurals=2; plural=(n != 1);", "the caller-supplied header is preserved verbatim");
        document.Entries.Single(static entry => entry.Key == "Tricky").Value.Should().Be("say \"hi\"\r\nline two\tend \\ done");
        document.Entries.Single(static entry => entry.Key == "Tricky").TranslatorNote.Should().Be("Line one.\nLine two.");
        document.Entries.Single(static entry => entry.Key == "Open").Context.Should().Be("menu");
        var plural = document.Entries.Single(static entry => entry.PluralKey is not null);
        plural.Key.Should().Be("book");
        plural.Context.Should().Be("shelf");
        plural.Forms.Should().BeEquivalentTo(["livre", "livres"], "the forms round-trip");
    }

    /// <summary>
    /// Verifies that provider interop exported file serves the same templates and plural semantics.
    /// </summary>
    [TestMethod]
    public async Task ProviderInterop_ExportedFile_ServesTheSameTemplatesAndPluralSemanticsAsync() {
        var catalog = new Dictionary<string, string?> {
            ["Hello"] = "مرحبا",
            ["item"] = "صفر",
        };
        var plurals = new List<PoPluralEntry> {
            new() {
                SingularKey = "item",
                PluralKey = "items",
                Forms = ["صفر", "واحد", "اثنان", "قليل", "كثير", "أخرى"],
            },
        };
        var poPath = TempPath(".po");
        try {
            PoCatalogInterchange.ExportCatalogFiles(await WriteCatalogAsync(catalog), poPath, "ar", plurals, pluralForms: ARABIC_PLURAL_FORMS);

            var provider = new PoMessageProvider(poPath);
            var culture = new CultureInfo("ar");

            (await provider.GetMessageTemplateAsync("Hello", culture, Cancellation)).Should().Be("مرحبا", "the runtime provider serves the exported flat template");

            var expected = new (decimal Count, string Form)[]
            {
                (0, "صفر"),
                (1, "واحد"),
                (2, "اثنان"),
                (3, "قليل"),
                (10, "قليل"),
                (11, "كثير"),
                (100, "أخرى"),
            };
            foreach(var (count, form) in expected) {
                (await provider.GetPluralTemplateAsync("item", "items", count, culture, Cancellation))
                    .Should().Be(form, $"count {count.ToString(CultureInfo.InvariantCulture)} selects the same form the provider serves");
            }
        }
        finally {
            File.Delete(poPath);
        }
    }

    /// <summary>
    /// Verifies that file round trip sample catalog is byte identical.
    /// </summary>
    [TestMethod]
    public async Task FileRoundTrip_SampleCatalog_IsByteIdenticalAsync() {
        var root = RepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), $"po-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try {
            var sourcePath = Path.Combine(work, "en.json");
            var targetPath = Path.Combine(work, "ar.json");
            File.Copy(Path.Combine(root, "samples", "Content", "Localization", "en.json"), sourcePath);
            File.Copy(Path.Combine(root, "samples", "Content", "Localization", "ar.json"), targetPath);
            var poPath = Path.Combine(work, "ar.po");
            var before = await File.ReadAllBytesAsync(targetPath, Cancellation);

            PoCatalogInterchange.ExportCatalogFiles(targetPath, poPath, "ar");
            var report = PoCatalogInterchange.ImportCatalogFiles(poPath, sourcePath, targetPath);

            report.UpdatedCount.Should().Be(0);
            (await File.ReadAllBytesAsync(targetPath, Cancellation)).Should().BeEquivalentTo(before, "an unchanged import rewrites nothing");
        }
        finally {
            Directory.Delete(work, true);
        }
    }

    /// <summary>
    /// Verifies that import committed sample po is unchanged.
    /// </summary>
    [TestMethod]
    public void Import_CommittedSamplePo_IsUnchanged() {
        var root = RepositoryRoot();
        var poPath = Path.Combine(root, "samples", "Content", "Localization", "ar.po");
        var sourcePath = Path.Combine(root, "samples", "Content", "Localization", "en.json");
        var targetPath = Path.Combine(root, "samples", "Content", "Localization", "ar.json");

        var report = PoCatalogInterchange.ImportCatalogFiles(poPath, sourcePath, targetPath);

        report.UpdatedCount.Should().Be(0, "the committed sample PO matches the canonical Arabic catalog");
        report.ConflictCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that import applies only changed targets preserving catalog formatting.
    /// </summary>
    [TestMethod]
    public async Task Import_AppliesOnlyChangedTargets_PreservingCatalogFormattingAsync() {
        var sourcePath = await WriteCatalogAsync(_source);
        var targetPath = await WriteCatalogAsync(_target);
        var poPath = TempPath(".po");
        try {
            PoCatalogInterchange.ExportCatalogFiles(targetPath, poPath, "ar");
            var po = await File.ReadAllTextAsync(poPath, Cancellation);
            po = po.Replace("msgstr \"Bonjour\"", "msgstr \"Marhaba\"", StringComparison.Ordinal);
            await File.WriteAllTextAsync(poPath, po, new UTF8Encoding(false), Cancellation);

            var report = PoCatalogInterchange.ImportCatalogFiles(poPath, sourcePath, targetPath);

            report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated);
            report.Keys.Single(static k => k.Key == "Bye").Status.Should().Be(CatalogKeyStatus.Unchanged);
            var rewritten = await File.ReadAllTextAsync(targetPath, Cancellation);
            rewritten.Should().Contain("\"Hello\": \"Marhaba\"");
            rewritten.IndexOf("\"Hello\"", StringComparison.Ordinal).Should().BeLessThan(rewritten.IndexOf("\"Bye\"", StringComparison.Ordinal),
                "the catalog key order is preserved");
        }
        finally {
            Delete(sourcePath, targetPath, poPath);
        }
    }

    /// <summary>
    /// Verifies that import dry run changes nothing.
    /// </summary>
    [TestMethod]
    public void Import_DryRun_ChangesNothing() {
        var sourcePath = WriteCatalog(_source);
        var targetPath = WriteCatalog(_target);
        var poPath = TempPath(".po");
        try {
            PoCatalogInterchange.ExportCatalogFiles(targetPath, poPath, "ar");
            var po = File.ReadAllText(poPath).Replace("msgstr \"Bonjour\"", "msgstr \"Marhaba\"", StringComparison.Ordinal);
            File.WriteAllText(poPath, po, new UTF8Encoding(false));
            var before = File.ReadAllBytes(targetPath);

            var report = PoCatalogInterchange.ImportCatalogFiles(poPath, sourcePath, targetPath, options: new PoImportOptions { DryRun = true });

            report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated, "the preview reports what would apply");
            File.ReadAllBytes(targetPath).Should().BeEquivalentTo(before, "dry-run changes nothing");
        }
        finally {
            Delete(sourcePath, targetPath, poPath);
        }
    }

    // ------------------------------------------------------------ status subset

    /// <summary>
    /// Verifies that import empty msgstr is skipped and never clears existing translations.
    /// </summary>
    [TestMethod]
    public void Import_EmptyMsgstr_IsSkipped_AndNeverClearsExistingTranslations() {
        var result = Import(Po("Hello", string.Empty));

        result.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.Skipped);
        result.UpdatedTarget.Should().BeNull("existing templates are never cleared by an empty msgstr");
    }

    /// <summary>
    /// Verifies that import fuzzy entries are is skipped.
    /// </summary>
    [TestMethod]
    public void Import_FuzzyEntries_AreSkipped() {
        var text = string.Join(
            "\n",
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "",
            "#, fuzzy",
            "msgid \"Hello\"",
            "msgstr \"Salut\"");

        var result = Import(text);

        result.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.Skipped);
        result.Report.Keys.Single().Detail.Should().Contain("fuzzy");
        result.UpdatedTarget.Should().BeNull();
    }

    /// <summary>
    /// Verifies that import unknown key is reported never is applied.
    /// </summary>
    [TestMethod]
    public void Import_UnknownKey_IsReportedNeverApplied() {
        var result = Import(Po("Ghost", "Boo"));

        result.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.UnknownKey);
        result.Report.ConflictCount.Should().Be(1);
        result.UpdatedTarget.Should().BeNull();
    }

    /// <summary>
    /// Verifies that import contextual key against an xml target is unsupported.
    /// </summary>
    [TestMethod]
    public void Import_ContextualKeyAgainstAnXmlTarget_IsUnsupported() {
        var text = string.Join(
            "\n",
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "",
            "msgctxt \"menu\"",
            "msgid \"Open\"",
            "msgstr \"Ouvrir\"");

        var result = PoCatalogInterchange.ImportDocument(
            PoCatalogInterchange.ParseDocument(text, "catalog.po"),
            _source,
            _target,
            xmlTarget: true);

        result.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.UnsupportedKey, "contextual keys cannot be XML element names");
    }

    /// <summary>
    /// Verifies that import surfaces extracted comments as translator notes.
    /// </summary>
    [TestMethod]
    public void Import_SurfacesExtractedCommentsAsTranslatorNotes() {
        var text = string.Join(
            "\n",
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "",
            "#. Vendor hint.",
            "#. Second line.",
            "msgid \"Hello\"",
            "msgstr \"Marhaba\"");

        var result = Import(text);

        result.Report.Keys.Single().TranslatorNote.Should().Be("Vendor hint.\nSecond line.");
    }

    // ------------------------------------------------------------------ plurals

    /// <summary>
    /// Verifies that import plural forms with tables update or report count mismatch.
    /// </summary>
    [TestMethod]
    public void Import_PluralForms_WithTables_UpdateOrReportCountMismatch() {
        var currentPlurals = new List<PoPluralEntry> {
            new() { SingularKey = "item", PluralKey = "items", Forms = ["صفر", "واحد", "اثنان", "قليل", "كثير", "أخرى"] },
        };
        var source = new Dictionary<string, string?> { ["item"] = "item", ["items"] = "items" };
        var current = new Dictionary<string, string?> { ["item"] = "صفر", ["items"] = "واحد" };

        var changed = PoCatalogInterchange.ImportDocument(
            PoCatalogInterchange.ParseDocument(PoPlural(["صفر", "واحد", "اثنان", "تعديل", "كثير", "أخرى"]), "catalog.po"),
            source,
            current,
            currentPlurals);

        changed.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.Updated);
        changed.UpdatedPlurals.Should().NotBeNull();
        changed.UpdatedPlurals!.Single().Forms[3].Should().Be("تعديل", "the changed form lands in the updated table");

        var mismatch = PoCatalogInterchange.ImportDocument(
            PoCatalogInterchange.ParseDocument(PoPlural(["one", "other"]), "catalog.po"),
            source,
            current,
            currentPlurals);

        mismatch.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.UnsupportedKey, "form counts must match the current table");
        mismatch.UpdatedPlurals.Should().BeNull();
    }

    /// <summary>
    /// Verifies that import plural forms without tables use pair slots.
    /// </summary>
    [TestMethod]
    public void Import_PluralForms_WithoutTables_UsePairSlots() {
        var source = new Dictionary<string, string?> { ["item"] = "item", ["items"] = "items" };
        var current = new Dictionary<string, string?> { ["item"] = "old-singular", ["items"] = "old-plural" };

        var pair = PoCatalogInterchange.ImportDocument(
            PoCatalogInterchange.ParseDocument(PoPlural(["livre", "livres"]), "catalog.po"),
            source,
            current);

        pair.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.Updated);
        pair.UpdatedTarget!["item"].Should().Be("livre");
        pair.UpdatedTarget["items"].Should().Be("livres");
        pair.UpdatedPlurals.Should().BeNull();

        var beyondPair = PoCatalogInterchange.ImportDocument(
            PoCatalogInterchange.ParseDocument(PoPlural(["a", "b", "c"]), "catalog.po"),
            source,
            current);

        beyondPair.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.UnsupportedKey, "forms beyond the pair slots need the current plural tables");
        beyondPair.UpdatedTarget.Should().BeNull();
    }

    // -------------------------------------------------------------- diagnostics

    /// <summary>
    /// Verifies that parse malformed po fails with file line diagnostics.
    /// </summary>
    [TestMethod]
    public void Parse_MalformedPo_FailsWithFileLineDiagnostics() {
        var header = string.Join("\n", "msgid \"\"", "msgstr \"\"", "\"Content-Type: text/plain; charset=UTF-8\\n\"");
        var cases = new (string Text, string Expected)[]
        {
            ("bogus line", "Unrecognized line"),
            ("msgid \"A\"\nmsgstr \"unterminated", "Malformed quoted string"),
            ("msgid \"A\"", "without msgstr"),
            ("msgid \"A\"\nmsgstr \"x\"\n\nmsgid \"A\"\nmsgstr \"y\"", "Duplicate message key"),
            ("msgid \"A\"\nmsgid_plural \"B\"\nmsgstr[0] \"x\"\nmsgstr[2] \"z\"", "misses msgstr[1]"),
            ("msgid \"A\"\nmsgid_plural \"B\"\nmsgstr \"plain\"", "must use indexed msgstr[n]"),
            ("msgid \"A\"\nmsgstr \"\\q\"", "Malformed escape"),
            ("msgid \"A\"\nmsgstr[0] \"x\"", "without msgstr"),
            ("msgid \"A\"\nmsgstr \"x\"\nmsgstr[0] \"y\"", "must use plain msgstr"),
            ($"{header}\n\"Plural-Forms: nplurals=2; plural=(n != 1);\\n\"\n\nmsgid \"a\"\nmsgid_plural \"b\"\nmsgstr[0] \"x\"\nmsgstr[1] \"y\"\nmsgstr[2] \"z\"", "nplurals=2"),
            ($"{header}\n\"Plural-Forms: nplurals=x; plural=(n != 1);\\n\"", "Invalid Plural-Forms nplurals"),
        };

        foreach(var (text, expected) in cases) {
            var exception = Assert.ThrowsExactly<PoInterchangeException>(
                () => PoCatalogInterchange.ParseDocument(text, "broken.po"));
            exception.Message.Should().Contain(expected);
            exception.FilePath.Should().Be("broken.po");
        }
    }

    /// <summary>
    /// Verifies that parse tolerates bom header and obsolete comments.
    /// </summary>
    [TestMethod]
    public void Parse_ToleratesBomHeaderAndObsoleteComments() {
        var text = string.Join(
            "\n",
            "\uFEFFmsgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "\"Language: ar\\n\"",
            "",
            "#: src/Program.cs:12",
            "# translator note",
            "msgid \"Hello\"",
            "msgstr \"مرحبا\"",
            "",
            "#~ msgid \"Obsolete\"",
            "#~ msgstr \"قديم\"");

        var document = PoCatalogInterchange.ParseDocument(text, "catalog.po");

        document.Headers["Language"].Should().Be("ar");
        document.Entries.Should().HaveCount(1);
        document.Entries[0].Key.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that import malformed catalog file throws po interchange exception.
    /// </summary>
    [TestMethod]
    public void Import_MalformedCatalogFile_ThrowsPoInterchangeException() {
        var sourcePath = TempPath(".json");
        var targetPath = WriteCatalog(_target);
        var poPath = TempPath(".po");
        try {
            File.WriteAllText(sourcePath, "{ not json", new UTF8Encoding(false));
            File.WriteAllText(poPath, Po("Hello", "Marhaba"), new UTF8Encoding(false));

            var exception = Assert.ThrowsExactly<PoInterchangeException>(
                () => PoCatalogInterchange.ImportCatalogFiles(poPath, sourcePath, targetPath));

            exception.FilePath.Should().Be(sourcePath, "the shared codec failure is wrapped into the public PO exception");
            exception.Message.Should().Contain("Malformed JSON catalog");
        }
        finally {
            Delete(sourcePath, targetPath, poPath);
        }
    }

    /// <summary>
    /// Verifies that import validates arguments.
    /// </summary>
    [TestMethod]
    public void Import_ValidatesArguments() {
        var document = PoCatalogInterchange.ParseDocument(Po("Hello", "Marhaba"), "catalog.po");

        Action nullDocument = () => PoCatalogInterchange.ImportDocument(null!, _source, _target);
        Action nullSource = () => PoCatalogInterchange.ImportDocument(document, null!, _target);
        Action nullTarget = () => PoCatalogInterchange.ImportDocument(document, _source, null!);

        nullDocument.Should().Throw<ArgumentNullException>();
        nullSource.Should().Throw<ArgumentNullException>();
        nullTarget.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------- helpers

    /// <summary>
    /// Performs the import operation.
    /// </summary>
    /// <param name="poText">The po text.</param>
    /// <returns>The import result.</returns>
    private static PoCatalogImportResult Import(string poText) =>
        PoCatalogInterchange.ImportDocument(
            PoCatalogInterchange.ParseDocument(poText, "catalog.po"),
            _source,
            _target);

    /// <summary>
    /// Performs the po operation.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="value">The value.</param>
    /// <returns>The po text.</returns>
    private static string Po(string key, string value) =>
        string.Join(
            "\n",
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "",
            $"msgid \"{key}\"",
            $"msgstr \"{value}\"");

    /// <summary>
    /// Performs the po plural operation.
    /// </summary>
    /// <param name="forms">The forms.</param>
    /// <returns>The po plural text.</returns>
    private static string PoPlural(IReadOnlyList<string> forms) {
        var lines = new List<string> {
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            "",
            "msgid \"item\"",
            "msgid_plural \"items\"",
        };
        for(var index = 0; index < forms.Count; index++) {
            lines.Add($"msgstr[{index.ToString(CultureInfo.InvariantCulture)}] \"{forms[index]}\"");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Performs the temp path operation.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <returns>The temp path text.</returns>
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"po-{Guid.NewGuid():N}{extension}");

    /// <summary>
    /// Writes catalog.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The catalog text.</returns>
    private static string WriteCatalog(IReadOnlyDictionary<string, string?> entries) {
        var path = TempPath(".json");
        File.WriteAllText(path, BuildJson(entries), new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Writes catalog.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>A task whose result is catalog.</returns>
    private async Task<string> WriteCatalogAsync(IReadOnlyDictionary<string, string?> entries) {
        var path = TempPath(".json");
        await File.WriteAllTextAsync(path, BuildJson(entries), new UTF8Encoding(false), Cancellation);
        return path;
    }

    /// <summary>
    /// Builds json.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The json text.</returns>
    private static string BuildJson(IReadOnlyDictionary<string, string?> entries) {
        var builder = new StringBuilder();
        builder.Append("{\r\n");
        var first = true;
        foreach(var kvp in entries) {
            if(!first) {
                builder.Insert(builder.Length - 2, ",");
            }

            first = false;
            builder.Append(CultureInfo.InvariantCulture, $"  \"{kvp.Key}\": \"{kvp.Value}\"\r\n");
        }

        builder.Append('}');
        return builder.ToString();
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
    /// Performs the delete operation.
    /// </summary>
    /// <param name="paths">The paths.</param>
    private static void Delete(params string[] paths) {
        foreach(var path in paths) {
            File.Delete(path);
        }
    }
}
