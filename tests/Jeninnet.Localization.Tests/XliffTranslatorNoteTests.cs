//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// The XLIFF (XML Localization Interchange File Format) translator-note carrier: export emits
/// <c>note category="translator"</c> for flat keys (null/empty skipped), the parser
/// accepts the category and rejects conflicting duplicates, the import report surfaces
/// notes per key (never dropped, including skipped/conflicted units), and file import
/// never applies notes to catalog files (they have no note storage).
/// </summary>
[TestClass]
public sealed class XliffTranslatorNoteTests {
    private static readonly Dictionary<string, string?> _source = new() {
        ["Greeting"] = "Hello",
        ["Farewell"] = "Bye",
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
    /// Verifies that export emits translator notes for flat keys and parses them back.
    /// </summary>
    [TestMethod]
    public void Export_EmitsTranslatorNotesForFlatKeys_AndParsesThemBack() {
        var notes = new Dictionary<string, string> { ["Greeting"] = "Shown on the home page; friendly tone." };

        var text = XliffCatalogInterchange.ExportCatalog(_source, null, "en", "ar", "catalog", translatorNotes: notes);
        var document = XliffCatalogInterchange.ParseDocument(text, "catalog.xliff");

        text.Should().Contain("category=\"translator\"");
        document.Units.Single(static unit => unit.Id == "Greeting").TranslatorNote.Should().Be("Shown on the home page; friendly tone.");
        document.Units.Single(static unit => unit.Id == "Farewell").TranslatorNote.Should().BeNull();
    }

    /// <summary>
    /// Verifies that export skips null empty and unknown note values.
    /// </summary>
    [TestMethod]
    public void Export_SkipsNullEmptyAndUnknownNoteValues() {
        var notes = new Dictionary<string, string> {
            ["Greeting"] = string.Empty,
            ["Farewell"] = "Kept.",
            ["UnknownKey"] = "No unit.",
        };

        var text = XliffCatalogInterchange.ExportCatalog(_source, null, "en", "ar", "catalog", translatorNotes: notes);
        var document = XliffCatalogInterchange.ParseDocument(text, "catalog.xliff");

        document.Units.Single(static unit => unit.Id == "Greeting").TranslatorNote.Should().BeNull("empty notes are equivalent to no note");
        document.Units.Single(static unit => unit.Id == "Farewell").TranslatorNote.Should().Be("Kept.");
        document.Units.Should().OnlyContain(static unit => unit.Id != "UnknownKey");
    }

    /// <summary>
    /// Verifies that export preserves note text exactly across special characters.
    /// </summary>
    [TestMethod]
    public void Export_PreservesNoteTextExactly_AcrossSpecialCharacters() {
        const string note = "Use <b>bold</b> & \"quotes\" — line one\nline two ␄ أهلاً";
        var notes = new Dictionary<string, string> { ["Greeting"] = note };

        var text = XliffCatalogInterchange.ExportCatalog(_source, null, "en", "ar", "catalog", translatorNotes: notes);
        var document = XliffCatalogInterchange.ParseDocument(text, "catalog.xliff");

        document.Units.Single(static unit => unit.Id == "Greeting").TranslatorNote.Should().Be(note);
    }

    /// <summary>
    /// Verifies that export plural units do not carry translator notes.
    /// </summary>
    [TestMethod]
    public void Export_PluralUnits_DoNotCarryTranslatorNotes() {
        var source = new Dictionary<string, string?> {
            ["Greeting"] = "Hello",
            ["Item"] = "item",
            ["Items"] = "items",
        };
        var pluralEntries = new List<XliffPluralEntry> {
            new() {
                SingularKey = "Item",
                PluralKey = "Items",
                TargetForms = ["Artikel", "Artikelen"],
            },
        };
        var notes = new Dictionary<string, string> { ["Item"] = "Note for the group." };

        var text = XliffCatalogInterchange.ExportCatalog(source, null, "en", "nl", "catalog", pluralEntries, notes);
        var document = XliffCatalogInterchange.ParseDocument(text, "catalog.xliff");

        document.Units.Where(static unit => unit.Plural is not null).Should().NotBeEmpty();
        document.Units.Where(static unit => unit.Plural is not null).Should().OnlyContain(static unit => unit.TranslatorNote is null,
            "the subset attaches translator notes to flat units only");
    }

    // ------------------------------------------------------------------- import

    /// <summary>
    /// Verifies that import surfaces notes per key including skipped and unknown units.
    /// </summary>
    [TestMethod]
    public void Import_SurfacesNotesPerKey_IncludingSkippedAndUnknownUnits() {
        const string xliff = """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="2.0" srcLang="en" trgLang="ar">
              <file id="catalog">
                <unit id="Greeting" xml:space="preserve">
                  <notes><note category="translator">Skipped note.</note></notes>
                  <segment state="initial"><source>Hello</source></segment>
                </unit>
                <unit id="Unknown" xml:space="preserve">
                  <notes><note category="translator">Unknown note.</note></notes>
                  <segment state="initial"><source>Nope</source><target>Nope-ar</target></segment>
                </unit>
              </file>
            </xliff>
            """;

        var document = XliffCatalogInterchange.ParseDocument(xliff, "vendor.xliff");
        var result = XliffCatalogInterchange.ImportDocument(
            document,
            _source,
            new Dictionary<string, string?> { ["Greeting"] = "Marhaba" });

        var skipped = result.Report.Keys.Single(static key => key.Key == "Greeting");
        skipped.Status.Should().Be(CatalogKeyStatus.Skipped);
        skipped.TranslatorNote.Should().Be("Skipped note.", "notes are surfaced even when the target is not applied");

        var unknown = result.Report.Keys.Single(static key => key.Key == "Unknown");
        unknown.Status.Should().Be(CatalogKeyStatus.UnknownKey);
        unknown.TranslatorNote.Should().Be("Unknown note.");
        result.UpdatedTarget.Should().BeNull("nothing was applied");
    }

    /// <summary>
    /// Verifies that import conflicting translator notes on one unit fail validation.
    /// </summary>
    [TestMethod]
    public void Import_ConflictingTranslatorNotesOnOneUnit_FailValidation() {
        const string xliff = """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="2.0" srcLang="en" trgLang="ar">
              <file id="catalog">
                <unit id="Greeting" xml:space="preserve">
                  <notes>
                    <note category="translator">First.</note>
                    <note category="translator">Second.</note>
                  </notes>
                  <segment state="translated"><source>Hello</source><target>Marhaba</target></segment>
                </unit>
              </file>
            </xliff>
            """;

        Action act = () => XliffCatalogInterchange.ParseDocument(xliff, "vendor.xliff");

        act.Should().Throw<XliffInterchangeException>("the whole document validates before anything is applied");
    }

    /// <summary>
    /// Verifies that import identical duplicate translator notes are is accepted.
    /// </summary>
    [TestMethod]
    public void Import_IdenticalDuplicateTranslatorNotes_AreAccepted() {
        const string xliff = """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="2.0" srcLang="en" trgLang="ar">
              <file id="catalog">
                <unit id="Greeting" xml:space="preserve">
                  <notes>
                    <note category="translator">Same.</note>
                    <note category="translator">Same.</note>
                  </notes>
                  <segment state="translated"><source>Hello</source><target>Marhaba</target></segment>
                </unit>
              </file>
            </xliff>
            """;

        var document = XliffCatalogInterchange.ParseDocument(xliff, "vendor.xliff");

        document.Units.Single().TranslatorNote.Should().Be("Same.");
    }

    // --------------------------------------------------------------- file import

    /// <summary>
    /// Verifies that file round trip carries notes without applying them to catalog files.
    /// </summary>
    [TestMethod]
    public async Task FileRoundTrip_CarriesNotes_WithoutApplyingThemToCatalogFilesAsync() {
        var sourcePath = TempPath(".json");
        var targetPath = TempPath(".json");
        var xliffPath = TempPath(".xliff");
        try {
            await File.WriteAllTextAsync(sourcePath, """{"Greeting":"Hello"}""", new UTF8Encoding(false), Cancellation);
            await File.WriteAllTextAsync(targetPath, """{"Greeting":"Marhaba"}""", new UTF8Encoding(false), Cancellation);
            var before = await File.ReadAllBytesAsync(targetPath, Cancellation);

            XliffCatalogInterchange.ExportCatalogFiles(
                sourcePath,
                targetPath,
                xliffPath,
                "en",
                "ar",
                translatorNotes: new Dictionary<string, string> { ["Greeting"] = "Vendor hint." });

            (await File.ReadAllTextAsync(xliffPath, Cancellation)).Should().Contain("Vendor hint.");

            var report = XliffCatalogInterchange.ImportCatalogFiles(xliffPath, sourcePath, targetPath);

            report.Keys.Single(static key => key.Key == "Greeting").TranslatorNote.Should().Be("Vendor hint.");
            (await File.ReadAllBytesAsync(targetPath, Cancellation)).Should().BeEquivalentTo(
                before,
                "catalog files have no note storage; notes are surfaced in the report only");
        }
        finally {
            File.Delete(sourcePath);
            File.Delete(targetPath);
            File.Delete(xliffPath);
        }
    }

    /// <summary>
    /// Performs the temp path operation.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <returns>The temp path text.</returns>
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"xliff-notes-{Guid.NewGuid():N}{extension}");
}
