//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
///  provider tests: PO (Portable Object) parsing (multiline strings, contexts, plural headers),
/// fr 2-form + cs 3-form + ar 6-form resolution via the provider plus the  API,
/// context independence, sample parity, location SPI, and missing-key behavior.
/// Malformed-file, lifecycle, and contract behavior live in
/// <see cref="PoProviderFileBehaviorTests"/>.
/// </summary>
[TestClass]
public class PoMessageProviderTests {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    /// <summary>
    /// Writes po file.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>A task whose result is po file.</returns>
    private async Task<string> WritePoFileAsync(string content) {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, content, TestContext?.CancellationToken ?? CancellationToken.None);
        return tempFile;
    }

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(IMessageProvider provider, string defaultCulture = "en") =>
        new(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = defaultCulture });

    private const string FR_CATALOG = """
        msgid ""
        msgstr ""
        "Content-Type: text/plain; charset=UTF-8\n"
        "Plural-Forms: nplurals=2; plural=(n > 1);\n"

        msgid "apple"
        msgid_plural "apples"
        msgstr[0] "pomme"
        msgstr[1] "pommes"
        """;

    private const string CS_CATALOG = """
        msgid ""
        msgstr ""
        "Content-Type: text/plain; charset=UTF-8\n"
        "Plural-Forms: nplurals=3; plural=(n==1) ? 0 : (n>=2 && n<=4) ? 1 : 2;\n"

        msgid "book"
        msgid_plural "books"
        msgstr[0] "kniha"
        msgstr[1] "knihy"
        msgstr[2] "knih"
        """;

    private const string AR_CATALOG = """
        msgid ""
        msgstr ""
        "Content-Type: text/plain; charset=UTF-8\n"
        "Plural-Forms: nplurals=6; plural=n==0 ? 0 : n==1 ? 1 : n==2 ? 2 : n%100>=3 && n%100<=10 ? 3 : n%100>=11 ? 4 : 5;\n"

        msgid "file"
        msgid_plural "files"
        msgstr[0] "zero"
        msgstr[1] "one"
        msgstr[2] "two"
        msgstr[3] "few"
        msgstr[4] "many"
        msgstr[5] "other"
        """;

    /// <summary>
    /// Verifies that po provider french two forms resolve via plural api.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(0, "pomme")]
    [DataRow(1, "pomme")]
    [DataRow(2, "pommes")]
    [DataRow(5, "pommes")]
    [DataRow(100, "pommes")]
    public async Task PoProvider_FrenchTwoForms_ResolveViaPluralApiAsync(int count, string expected) {
        var tempFile = await WritePoFileAsync(FR_CATALOG);
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(tempFile));

            var result = await localizer.LocalizePluralAsync("apple", "apples", count, _noParameters, new CultureInfo("fr"), CancellationToken.None);

            result.Should().Be(expected);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider czech three forms resolve via plural api.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(1, "kniha")]
    [DataRow(2, "knihy")]
    [DataRow(3, "knihy")]
    [DataRow(4, "knihy")]
    [DataRow(0, "knih")]
    [DataRow(5, "knih")]
    [DataRow(6, "knih")]
    public async Task PoProvider_CzechThreeForms_ResolveViaPluralApiAsync(int count, string expected) {
        var tempFile = await WritePoFileAsync(CS_CATALOG);
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(tempFile));

            var result = await localizer.LocalizePluralAsync("book", "books", count, _noParameters, new CultureInfo("cs"), CancellationToken.None);

            result.Should().Be(expected);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider arabic six forms resolve via plural api.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(0, "zero")]
    [DataRow(1, "one")]
    [DataRow(2, "two")]
    [DataRow(3, "few")]
    [DataRow(7, "few")]
    [DataRow(10, "few")]
    [DataRow(11, "many")]
    [DataRow(50, "many")]
    [DataRow(99, "many")]
    [DataRow(100, "other")]
    [DataRow(102, "other")]
    public async Task PoProvider_ArabicSixForms_ResolveViaPluralApiAsync(int count, string expected) {
        var tempFile = await WritePoFileAsync(AR_CATALOG);
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(tempFile));

            var result = await localizer.LocalizePluralAsync("file", "files", count, _noParameters, new CultureInfo("ar"), CancellationToken.None);

            result.Should().Be(expected);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider plural without header falls back to pair mapping.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_PluralWithoutHeader_FallsBackToPairMappingAsync() {
        const string catalog = """
            msgid "apple"
            msgid_plural "apples"
            msgstr[0] "pomme"
            msgstr[1] "pommes"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(tempFile));
            var culture = new CultureInfo("fr");

            (await localizer.LocalizePluralAsync("apple", "apples", 1, _noParameters, culture, CancellationToken.None)).Should().Be("pomme");
            (await localizer.LocalizePluralAsync("apple", "apples", 5, _noParameters, culture, CancellationToken.None)).Should().Be("pommes");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider contexts keep identical msgids independent.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_Contexts_KeepIdenticalMsgidsIndependentAsync() {
        const string catalog = """
            msgid "open"
            msgstr "Open!"

            msgctxt "menu"
            msgid "open"
            msgstr "Open menu"

            msgctxt "dialog"
            msgid "open"
            msgstr "Open dialog"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var localizer = CreateLocalizer(new PoMessageProvider(tempFile));
            var culture = new CultureInfo("en");

            (await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, culture, CancellationToken.None)).Should().Be("Open menu");
            (await localizer.LocalizeWithContextAsync("open", "dialog", _noParameters, culture, CancellationToken.None)).Should().Be("Open dialog");
            (await localizer.LocalizeAsync("open", _noParameters, culture, CancellationToken.None)).Should().Be("Open!");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider contextual plural selects form within context.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_ContextualPlural_SelectsFormWithinContextAsync() {
        const string catalog = """
            msgid ""
            msgstr ""
            "Plural-Forms: nplurals=2; plural=(n != 1);\n"

            msgctxt "cart"
            msgid "apple"
            msgid_plural "apples"
            msgstr[0] "cart-pomme"
            msgstr[1] "cart-pommes"

            msgid "apple"
            msgid_plural "apples"
            msgstr[0] "pomme"
            msgstr[1] "pommes"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);
            var culture = new CultureInfo("en");

            (await provider.GetPluralTemplateWithContextAsync("apple", "apples", "cart", 1, culture, CancellationToken.None)).Should().Be("cart-pomme");
            (await provider.GetPluralTemplateWithContextAsync("apple", "apples", "cart", 3, culture, CancellationToken.None)).Should().Be("cart-pommes");
            (await provider.GetPluralTemplateAsync("apple", "apples", 3, culture, CancellationToken.None)).Should().Be("pommes");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider multiline strings are is concatenated.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_MultilineStrings_AreConcatenatedAsync() {
        const string catalog = """
            msgid "multi"
                  "line"
            msgstr "first "
                   "second"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("multiline", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first second");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider escapes are is decoded.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_Escapes_AreDecodedAsync() {
        const string catalog = """
            msgid "key"
            msgstr "quote: \" backslash: \\ newline:\n tab:\t end"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None))
                .Should().Be("quote: \" backslash: \\ newline:\n tab:\t end");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider missing keys return null.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_MissingKeys_ReturnNullAsync() {
        var tempFile = await WritePoFileAsync(FR_CATALOG);
        try {
            var provider = new PoMessageProvider(tempFile);
            var culture = new CultureInfo("fr");

            (await provider.GetMessageTemplateAsync("UNKNOWN", culture, CancellationToken.None)).Should().BeNull();
            (await provider.GetPluralTemplateAsync("unknown-single", "unknown-plural", 2, culture, CancellationToken.None)).Should().BeNull();
            (await provider.GetPluralTemplateWithContextAsync("apple", "apples", "unknown-context", 2, culture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider untranslated forms report as missing.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_UntranslatedForms_ReportAsMissingAsync() {
        const string catalog = """
            msgid "translated"
            msgstr "value"

            msgid "untranslated"
            msgstr ""
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("translated", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
            (await provider.GetMessageTemplateAsync("untranslated", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider fuzzy entries are is skipped.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_FuzzyEntries_AreSkippedAsync() {
        const string catalog = """
            #, fuzzy
            msgid "old"
            msgstr "old-value"

            msgid "new"
            msgstr "new-value"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("old", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
            (await provider.GetMessageTemplateAsync("new", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("new-value");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider uses ordinal case sensitive key matching.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_UsesOrdinalCaseSensitiveKeyMatchingAsync() {
        const string catalog = """
            msgid "key"
            msgstr "lower"

            msgid "KEY"
            msgstr "upper"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("lower");
            (await provider.GetMessageTemplateAsync("KEY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("upper");
            (await provider.GetMessageTemplateAsync("Key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider multi culture builder dispatches per culture.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_MultiCultureBuilder_DispatchesPerCultureAsync() {
        var frFile = await WritePoFileAsync(FR_CATALOG);
        var arFile = await WritePoFileAsync(AR_CATALOG);
        try {
            var builder = new PoLocalizationProviderBuilder()
                .AddCulture("fr", frFile)
                .AddCulture("ar", arFile);
            var localizer = CreateLocalizer(builder.Build());

            (await localizer.LocalizePluralAsync("apple", "apples", 5, _noParameters, new CultureInfo("fr"), CancellationToken.None)).Should().Be("pommes");
            (await localizer.LocalizePluralAsync("file", "files", 2, _noParameters, new CultureInfo("ar"), CancellationToken.None)).Should().Be("two");
        }
        finally {
            File.Delete(frFile);
            File.Delete(arFile);
        }
    }

    /// <summary>
    /// Verifies that po provider sample content matches json parity.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_SampleContent_MatchesJsonParityAsync() {
        var root = RepositoryRoot();
        foreach(var culture in new[] { "en", "ar" }) {
            var jsonPath = Path.Combine(root, "samples", "Content", "Localization", $"{culture}.json");
            var poPath = Path.Combine(root, "samples", "Content", "Localization", $"{culture}.po");
            File.Exists(jsonPath).Should().BeTrue($"sample JSON must exist for {culture}");
            File.Exists(poPath).Should().BeTrue($"mirrored sample PO must exist for {culture}");

            var json = await File.ReadAllTextAsync(jsonPath, TestContext?.CancellationToken ?? CancellationToken.None);
            var expected = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
            var poProvider = new PoMessageProvider(poPath);
            var cultureInfo = new CultureInfo(culture);

            foreach(var kvp in expected) {
                (await poProvider.GetMessageTemplateAsync(kvp.Key, cultureInfo, CancellationToken.None))
                    .Should().Be(kvp.Value, $"PO mirror must match JSON for {culture}:{kvp.Key}");
            }

            poProvider.Catalog.Should().NotBeNull();
            poProvider.Resource.Should().NotBeNull();
        }
    }

    /// <summary>
    /// Verifies that default po file location provider resolves convention path.
    /// </summary>
    [TestMethod]
    public void DefaultPoFileLocationProvider_ResolvesConventionPath() {
        var locations = new DefaultPoFileLocationProvider("/base", "Localization");

        locations.GetPoFilePath(new CultureInfo("fr")).Should().Be(Path.Combine("/base", "Localization", "fr.po"));
        locations.GetPoFilePath(new CultureInfo("ar-SY")).Should().Be(Path.Combine("/base", "Localization", "ar-SY.po"));
    }

    /// <summary>
    /// Verifies that po builder accepts custom location provider.
    /// </summary>
    [TestMethod]
    public async Task PoBuilder_AcceptsCustomLocationProviderAsync() {
        var tempFile = await WritePoFileAsync(FR_CATALOG);
        try {
            var stub = new StubLocationProvider(tempFile);
            var builder = new PoLocalizationProviderBuilder().AddCultures(stub, ["fr"]);
            var localizer = CreateLocalizer(builder.Build());

            (await localizer.LocalizePluralAsync("apple", "apples", 1, _noParameters, new CultureInfo("fr"), CancellationToken.None)).Should().Be("pomme");
            stub.Requested.Should().Contain("fr");
        }
        finally {
            File.Delete(tempFile);
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
    /// Provides stub location.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    private sealed class StubLocationProvider(string filePath) : IPoFileLocationProvider {
        /// <summary>
        /// Gets the requested.
        /// </summary>
        public List<string> Requested { get; } = [];

        /// <summary>
        /// Gets po file path.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <returns>The po file path text.</returns>
        public string GetPoFilePath(CultureInfo culture) {
            Requested.Add(culture.Name);
            return filePath;
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
