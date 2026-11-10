//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the TOML provider file behavior tests.
/// </summary>
[TestClass]
public class TomlProviderFileBehaviorTests {
    /// <summary>
    /// Verifies that TOML provider empty file returns null for any requested key.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_EmptyFile_ReturnsNullForAnyRequestedKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            var result = await provider.GetMessageTemplateAsync("ANY_KEY", CultureInfo.InvariantCulture, CancellationToken.None);

            result.Should().BeNull();
            provider.Resource.Should().NotBeNull();
            provider.Resource!.Entries.Should().BeEmpty();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider blank lines and comments are ignored.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_BlankLinesAndComments_AreIgnoredAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "# leading comment\n\nA = \"1\"\n\n# trailing comment\n";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("1");
            provider.Resource!.Entries.Should().ContainSingle();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider trailing comments after a value are ignored.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_TrailingComment_AfterValue_IsIgnoredAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "A = \"1\" # the answer\nB = 'two'   # words";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("1");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("two");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider empty string value is distinct from missing key.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_EmptyStringValue_IsDistinctFromMissingKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "EMPTY = \"\"\nREAL = \"value\"";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("EMPTY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("");
            (await provider.GetMessageTemplateAsync("REAL", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
            (await provider.GetMessageTemplateAsync("MISSING", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider literal strings are verbatim.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_LiteralStrings_AreVerbatimAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "A = 'C:\\path\\n'";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("C:\\path\\n");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider basic string escapes are decoded.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_BasicStringEscapes_AreDecodedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "A = \"tab\\there\"\nB = \"quote \\\"q\\\"\"\nC = \"smiley \\u263A\"\nD = \"back \\\\ slash\"";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("tab\there");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("quote \"q\"");
            (await provider.GetMessageTemplateAsync("C", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("smiley ☺");
            (await provider.GetMessageTemplateAsync("D", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("back \\ slash");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider section headers qualify keys.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_SectionHeaders_QualifyKeysAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "BARE = \"root\"\n[Validation]\nRequired = \"This field is required.\"\n";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("BARE", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("root");
            (await provider.GetMessageTemplateAsync("Validation.Required", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("This field is required.");
            (await provider.GetMessageTemplateAsync("Required", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
            provider.Resource!.Entries.Should().HaveCount(2);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider quoted keys are supported.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_QuotedKeys_AreSupportedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "\"spaced key\" = \"1\"\n'other' = \"2\"";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("spaced key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("1");
            (await provider.GetMessageTemplateAsync("other", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("2");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider missing separator throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_MissingSeparator_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "NO_SEPARATOR_HERE", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("NO_SEPARATOR_HERE", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<InvalidDataException>();
            exception.Which.Message.Should().Contain("line 1");
            provider.Resource.Should().BeNull("a failed load publishes nothing");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider non string value throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_NonStringValue_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "A = 42", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<InvalidDataException>();
            exception.Which.Message.Should().Contain("line 1");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider dotted keys throw on first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_DottedKeys_ThrowOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "a.b = \"1\"", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("a.b", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider unsupported escape throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_UnsupportedEscape_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "A = \"\\q\"", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider unclosed section throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_UnclosedSection_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "[Validation\nA = \"1\"", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<InvalidDataException>();
            exception.Which.Message.Should().Contain("line 1");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider duplicate qualified keys throw on first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_DuplicateQualifiedKeys_ThrowOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "[Validation]\nRequired = \"first\"\nRequired = \"second\"";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("Validation.Required", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<InvalidDataException>();
            exception.Which.Message.Should().Contain("Validation.Required");
            exception.Which.Message.Should().Contain("line 3");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider failed load never publishes partial tables and retries.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_FailedLoad_NeverPublishesPartialTablesAndRetriesAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "A = \"ok\"\nBROKEN LINE", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>("a failed load publishes nothing");
            provider.Resource.Should().BeNull();

            await File.WriteAllTextAsync(tempFile, "A = \"fixed\"", CancellationToken.None);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None))
                .Should().Be("fixed", "a later lookup retries initialization after a failure");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider uses ordinal case sensitive key matching.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_UsesOrdinalCaseSensitiveKeyMatchingAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string toml = "key = \"lower\"\nKEY = \"upper\"";
            await File.WriteAllTextAsync(tempFile, toml, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("lower");
            (await provider.GetMessageTemplateAsync("KEY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("upper");
            (await provider.GetMessageTemplateAsync("Key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider loads once and ignores file changes after first lookup.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_LoadsOnceAndIgnoresFileChangesAfterFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "A = \"first\"", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");

            await File.WriteAllTextAsync(tempFile, "A = \"second\"\nB = \"added\"", TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that TOML provider Utf8Bom and non ascii values load correctly.
    /// </summary>
    [TestMethod]
    public async Task TomlProvider_Utf8BomAndNonAsciiValues_LoadCorrectlyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string value = "مرحبا — ünïcödé ✓";
            const string toml = "WELCOME = \"مرحبا — ünïcödé ✓\"";
            await File.WriteAllTextAsync(tempFile, toml, new UTF8Encoding(true), TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new TomlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("WELCOME", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be(value);
            provider.Resource!.Entries.Should().ContainSingle()
                .Which.Template.Should().Be(value);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
