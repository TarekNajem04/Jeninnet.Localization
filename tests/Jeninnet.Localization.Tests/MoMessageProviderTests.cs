//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
///  tests: GNU MO (Machine Object) binary catalogs (both endiannesses, revisions 0/1, contexts,
/// plurals, headers) served through <see cref="PoMessageProvider"/>, byte-for-byte
/// behavioral parity with the PO (Portable Object) text carrier, the reader-selection surface
/// (<see cref="PoCatalogFormat"/>), and malformed-binary failure behavior. Fixtures
/// are authored by <see cref="MoFileWriter"/>; no compiled artifacts are copied.
/// </summary>
[TestClass]
public class MoMessageProviderTests {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    private const string FR_PO_CATALOG = """
        msgid ""
        msgstr ""
        "Content-Type: text/plain; charset=UTF-8\n"
        "Plural-Forms: nplurals=2; plural=(n > 1);\n"

        msgid "apple"
        msgid_plural "apples"
        msgstr[0] "pomme"
        msgstr[1] "pommes"

        msgctxt "cart"
        msgid "file"
        msgid_plural "files"
        msgstr[0] "fichier"
        msgstr[1] "fichiers"

        msgctxt "menu"
        msgid "open"
        msgstr "Ouvrir"

        msgid "welcome"
        msgstr "Bienvenue"
        """;

    private static readonly (string Original, string Translation)[] _frMoEntries = [
        ("", "Content-Type: text/plain; charset=UTF-8\nPlural-Forms: nplurals=2; plural=(n > 1);\n"),
        ("apple\0apples", "pomme\0pommes"),
        ("cart\u0004file\0files", "fichier\0fichiers"),
        ("menu\u0004open", "Ouvrir"),
        ("welcome", "Bienvenue"),
    ];

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(IMessageProvider provider, string defaultCulture = "en") =>
        new(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = defaultCulture });

    /// <summary>
    /// Performs the new temp mo path operation.
    /// </summary>
    /// <returns>The new temp mo path text.</returns>
    private static string NewTempMoPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".mo");

    /// <summary>
    /// Writes mo file.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A task whose result is mo file.</returns>
    private async Task<string> WriteMoFileAsync(byte[] bytes) {
        var moFile = NewTempMoPath();
        await File.WriteAllBytesAsync(moFile, bytes, TestContext?.CancellationToken ?? CancellationToken.None);
        return moFile;
    }

    /// <summary>
    /// Asserts malformed mo.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="expectedFragment">The expected fragment.</param>
    private async Task AssertMalformedMoAsync(byte[] bytes, string expectedFragment) {
        var moFile = await WriteMoFileAsync(bytes);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile });
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("a", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<PoParseException>();
            exception.Which.FilePath.Should().Be(moFile);
            exception.Which.LineNumber.Should().Be(0, "binary diagnostics carry no line numbers");
            exception.Which.Message.Should().Contain(expectedFragment);
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider matches po provider served results parity.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_MatchesPoProvider_ServedResultsParityAsync() {
        var poFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(poFile, FR_PO_CATALOG, TestContext?.CancellationToken ?? CancellationToken.None);
        var moFile = await WriteMoFileAsync(MoFileWriter.Write(_frMoEntries));
        try {
            var poProvider = new PoMessageProvider(poFile);
            var moProvider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile });
            var fr = new CultureInfo("fr");

            foreach(var key in new[] { "welcome", "missing" }) {
                (await moProvider.GetMessageTemplateAsync(key, fr, CancellationToken.None))
                    .Should().Be(await poProvider.GetMessageTemplateAsync(key, fr, CancellationToken.None), $"key '{key}' must serve identically from both carriers");
            }

            (await moProvider.GetMessageTemplateAsync("menu\u0004open", fr, CancellationToken.None)).Should().Be("Ouvrir");

            foreach(var count in new[] { 0, 1, 2, 5 }) {
                var mo = await moProvider.GetPluralTemplateAsync("apple", "apples", count, fr, CancellationToken.None);
                var po = await poProvider.GetPluralTemplateAsync("apple", "apples", count, fr, CancellationToken.None);
                mo.Should().Be(po, $"plural count {count} must serve identically from both carriers");
            }

            foreach(var count in new[] { 0, 1, 2, 5 }) {
                var mo = await moProvider.GetPluralTemplateWithContextAsync("file", "files", "cart", count, fr, CancellationToken.None);
                var po = await poProvider.GetPluralTemplateWithContextAsync("file", "files", "cart", count, fr, CancellationToken.None);
                mo.Should().Be(po, $"contextual plural count {count} must serve identically from both carriers");
            }

            (await moProvider.GetPluralTemplateWithContextAsync("file", "files", "cart", 2, fr, CancellationToken.None)).Should().Be("fichiers");
        }
        finally {
            File.Delete(poFile);
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider both endiannesses and revisions serve plurals.
    /// </summary>
    /// <param name="bigEndian">The big endian.</param>
    /// <param name="revision">The revision.</param>
    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 0)]
    [DataRow(false, 1)]
    [DataRow(true, 1)]
    public async Task MoProvider_BothEndiannessesAndRevisions_ServePluralsAsync(bool bigEndian, int revision) {
        var moFile = await WriteMoFileAsync(MoFileWriter.Write(_frMoEntries, bigEndian, (uint)revision));
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(moFile));
            var fr = new CultureInfo("fr");

            (await localizer.LocalizePluralAsync("apple", "apples", 1, _noParameters, fr, CancellationToken.None)).Should().Be("pomme");
            (await localizer.LocalizePluralAsync("apple", "apples", 3, _noParameters, fr, CancellationToken.None)).Should().Be("pommes");
            (await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, fr, CancellationToken.None)).Should().Be("Ouvrir");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider plural forms header drives selection.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(0, "pomme")]
    [DataRow(1, "pomme")]
    [DataRow(2, "pommes")]
    [DataRow(5, "pommes")]
    public async Task MoProvider_PluralFormsHeader_DrivesSelectionAsync(int count, string expected) {
        var moFile = await WriteMoFileAsync(MoFileWriter.Write(_frMoEntries));
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(moFile));

            var result = await localizer.LocalizePluralAsync("apple", "apples", count, _noParameters, new CultureInfo("fr"), CancellationToken.None);

            result.Should().Be(expected);
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider contexts resolve via context api.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_Contexts_ResolveViaContextApiAsync() {
        var moFile = await WriteMoFileAsync(MoFileWriter.Write([
            ("menu\u0004open", "Ouvrir"),
            ("dialog\u0004open", "Ouvrir la boîte"),
        ]));
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(moFile));
            var fr = new CultureInfo("fr");

            (await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, fr, CancellationToken.None)).Should().Be("Ouvrir");
            (await localizer.LocalizeWithContextAsync("open", "dialog", _noParameters, fr, CancellationToken.None)).Should().Be("Ouvrir la boîte");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider hash table present is ignored.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_HashTablePresent_IsIgnoredAsync() {
        var bytes = MoFileWriter.Write([("key", "value")]);
        MoFileWriter.PatchUInt32(bytes, 20, 1);
        MoFileWriter.PatchUInt32(bytes, 24, MoFileWriter.HEADER_SIZE);
        var moFile = await WriteMoFileAsync(bytes);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile });

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
                "the hash table is an optimization and must not be consumed");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider empty catalog serves misses.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_EmptyCatalog_ServesMissesAsync() {
        var moFile = await WriteMoFileAsync(MoFileWriter.Write([]));
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile });

            (await provider.GetMessageTemplateAsync("anything", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
            provider.Catalog.Should().NotBeNull("an empty catalog is valid and loads successfully");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider auto format uppercase extension uses binary reader.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_AutoFormat_UppercaseExtension_UsesBinaryReaderAsync() {
        var moFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".MO");
        await File.WriteAllBytesAsync(moFile, MoFileWriter.Write([("key", "value")]), TestContext?.CancellationToken ?? CancellationToken.None);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile });

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
                "Auto must match .mo extensions case-insensitively");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider auto format non mo extension uses text reader.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_AutoFormat_NonMoExtension_UsesTextReaderAsync() {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "msgid \"key\"\nmsgstr \"value\"\n", TestContext?.CancellationToken ?? CancellationToken.None);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = tempFile });

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
                "Auto must keep the text reader for non-.mo extensions (no content probing)");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider explicit mo format forces binary reader.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_ExplicitMoFormat_ForcesBinaryReaderAsync() {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempFile, MoFileWriter.Write([("key", "value")]), TestContext?.CancellationToken ?? CancellationToken.None);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = tempFile, Format = PoCatalogFormat.Mo });

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider explicit po format forces text reader.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_ExplicitPoFormat_ForcesTextReaderAsync() {
        var moFile = NewTempMoPath();
        await File.WriteAllTextAsync(moFile, "msgid \"key\"\nmsgstr \"value\"\n", TestContext?.CancellationToken ?? CancellationToken.None);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile, Format = PoCatalogFormat.Po });

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
                "explicit Po must win over the .mo extension");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider undefined format throws on construction.
    /// </summary>
    [TestMethod]
    public void MoProvider_UndefinedFormat_ThrowsOnConstruction() {
        var tempFile = Path.GetTempFileName();
        try {
            var act = () => new PoMessageProvider(new PoLocalizationOptions { FilePath = tempFile, Format = (PoCatalogFormat)42 });

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider format snapshot survives options mutation.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_FormatSnapshot_SurvivesOptionsMutationAsync() {
        var moFile = await WriteMoFileAsync(MoFileWriter.Write([("key", "value")]));
        try {
            var options = new PoLocalizationOptions { FilePath = moFile };
            var provider = new PoMessageProvider(options);

            options.Format = PoCatalogFormat.Po;

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
                "the format selection must be snapshotted at construction");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider failed load never publishes partial tables.
    /// </summary>
    [TestMethod]
    public async Task MoProvider_FailedLoad_NeverPublishesPartialTablesAsync() {
        var moFile = await WriteMoFileAsync(new byte[10]);
        try {
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = moFile });
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("anything", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<PoParseException>("a failed load publishes nothing");
            provider.Catalog.Should().BeNull();
            provider.Resource.Should().BeNull();

            await File.WriteAllBytesAsync(moFile, MoFileWriter.Write([("fixed", "value")]), CancellationToken.None);

            (await provider.GetMessageTemplateAsync("fixed", CultureInfo.InvariantCulture, CancellationToken.None))
                .Should().Be("value", "a later lookup retries initialization after a failure");
        }
        finally {
            File.Delete(moFile);
        }
    }

    /// <summary>
    /// Verifies that mo provider too small binary throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_TooSmallBinary_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(new byte[10], "too small");

    /// <summary>
    /// Verifies that mo provider invalid magic throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_InvalidMagic_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(new byte[MoFileWriter.HEADER_SIZE], "Invalid MO magic");

    /// <summary>
    /// Verifies that mo provider unsupported revision throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_UnsupportedRevision_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(MoFileWriter.Write([("a", "b")], revision: 0x00020000), "Unsupported MO revision 2.0");

    /// <summary>
    /// Verifies that mo provider table beyond file throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_TableBeyondFile_ThrowsPoParseExceptionAsync() {
        var bytes = MoFileWriter.Write([("a", "b")]);
        MoFileWriter.PatchUInt32(bytes, 8, 1000);
        return AssertMalformedMoAsync(bytes, "exceeds the file size");
    }

    /// <summary>
    /// Verifies that mo provider string beyond file throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_StringBeyondFile_ThrowsPoParseExceptionAsync() {
        var bytes = MoFileWriter.Write([("a", "b")]);
        MoFileWriter.PatchUInt32(bytes, MoFileWriter.HEADER_SIZE, 0xFFFF);
        return AssertMalformedMoAsync(bytes, "exceeds the file size");
    }

    /// <summary>
    /// Verifies that mo provider invalid utf8 throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_InvalidUtf8_ThrowsPoParseExceptionAsync() {
        var bytes = MoFileWriter.Write([("a", "b")]);
        bytes[MoFileWriter.HEADER_SIZE + 16] = 0xFF;
        return AssertMalformedMoAsync(bytes, "not valid UTF-8");
    }

    /// <summary>
    /// Verifies that mo provider duplicate keys throw po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_DuplicateKeys_ThrowPoParseExceptionAsync() =>
        AssertMalformedMoAsync(MoFileWriter.Write([("a", "first"), ("a", "second")]), "Duplicate message key 'a'");

    /// <summary>
    /// Verifies that mo provider duplicate header throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_DuplicateHeader_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(MoFileWriter.Write([("", "X: 1\n"), ("", "X: 2\n")]), "Duplicate header entry");

    /// <summary>
    /// Verifies that mo provider NPlurals mismatch throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_NPluralsMismatch_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(
            MoFileWriter.Write([
                ("", "Plural-Forms: nplurals=2; plural=(n != 1);\n"),
                ("book\0books", "a\0b\0c"),
            ]),
            "declares nplurals=2");

    /// <summary>
    /// Verifies that mo provider translation nul without plural throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_TranslationNulWithoutPlural_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(MoFileWriter.Write([("apple", "a\0b")]), "has no msgid_plural");

    /// <summary>
    /// Verifies that mo provider original with two nuls throws po parse exception.
    /// </summary>
    [TestMethod]
    public Task MoProvider_OriginalWithTwoNuls_ThrowsPoParseExceptionAsync() =>
        AssertMalformedMoAsync(MoFileWriter.Write([("a\0b\0c", "x\0y")]), "more than one NUL separator");

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
