//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
///  file-behavior tests for <see cref="PoMessageProvider"/>: malformed PO (Portable Object) fails fast
/// with file/line diagnostics and never yields partial tables, duplicate/fuzzy/encoding
/// behavior, lifecycle snapshots, and the canonical provider contract.
/// </summary>
[TestClass]
public class PoProviderFileBehaviorTests {
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
    /// Verifies that po provider malformed content throws po parse exception.
    /// </summary>
    /// <param name="content">The content.</param>
    [TestMethod]
    [DataRow("msgid \"abc\nmsgstr \"x\"\n")]
    [DataRow("msgid_plural \"b\"\n")]
    [DataRow("msgstr \"orphan\"\n")]
    [DataRow("msgid \"a\"\nmsgid \"b\"\nmsgstr \"x\"\n")]
    [DataRow("msgid \"a\"\nmsgid_plural \"b\"\nmsgstr \"x\"\n")]
    [DataRow("msgid \"a\"\nmsgid_plural \"b\"\nmsgstr[0] \"x\"\nmsgstr[2] \"z\"\n")]
    [DataRow("msgid \"a\"\n")]
    public async Task PoProvider_MalformedContent_ThrowsPoParseExceptionAsync(string content) {
        var tempFile = await WritePoFileAsync(content);
        try {
            var provider = new PoMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("a", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<PoParseException>();
            exception.Which.FilePath.Should().Be(tempFile);
            exception.Which.LineNumber.Should().BeGreaterThan(0);
            exception.Which.Message.Should().Contain(tempFile);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider NPlurals mismatch throws po parse exception.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_NPluralsMismatch_ThrowsPoParseExceptionAsync() {
        const string catalog = """
            msgid ""
            msgstr ""
            "Plural-Forms: nplurals=2; plural=(n != 1);\n"

            msgid "book"
            msgid_plural "books"
            msgstr[0] "a"
            msgstr[1] "b"
            msgstr[2] "c"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("book", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<PoParseException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider invalid plural forms header throws po parse exception.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_InvalidPluralFormsHeader_ThrowsPoParseExceptionAsync() {
        const string catalog = """
            msgid ""
            msgstr ""
            "Plural-Forms: nplurals=two; plural=(n != 1);\n"

            msgid "a"
            msgstr "b"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("a", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<PoParseException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider duplicate keys throw po parse exception.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_DuplicateKeys_ThrowPoParseExceptionAsync() {
        const string catalog = """
            msgid "A"
            msgstr "first"

            msgid "A"
            msgstr "second"
            """;
        var tempFile = await WritePoFileAsync(catalog);
        try {
            var provider = new PoMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<PoParseException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider failed load never publishes partial tables.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_FailedLoad_NeverPublishesPartialTablesAsync() {
        var tempFile = await WritePoFileAsync("msgid \"broken\n");
        try {
            var provider = new PoMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("anything", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<PoParseException>("a failed load publishes nothing");
            provider.Catalog.Should().BeNull();
            provider.Resource.Should().BeNull();

            await File.WriteAllTextAsync(tempFile, "msgid \"fixed\"\nmsgstr \"value\"\n", CancellationToken.None);

            (await provider.GetMessageTemplateAsync("fixed", CultureInfo.InvariantCulture, CancellationToken.None))
                .Should().Be("value", "a later lookup retries initialization after a failure");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider Utf8Bom and non ascii values load correctly.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_Utf8BomAndNonAsciiValues_LoadCorrectlyAsync() {
        const string catalog = "msgid \"WELCOME\"\nmsgstr \"مرحبا — ünïcödé ✓\"\n";
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, catalog, new System.Text.UTF8Encoding(true), TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new PoMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("WELCOME", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("مرحبا — ünïcödé ✓");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider loaded snapshot survives file removal.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_LoadedSnapshotSurvivesFileRemovalAsync() {
        var tempFile = await WritePoFileAsync("msgid \"key\"\nmsgstr \"value\"\n");

        var provider = new PoMessageProvider(tempFile);
        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");

        File.Delete(tempFile);

        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
            "the provider loads the file exactly once and serves the immutable snapshot afterwards");
    }

    /// <summary>
    /// Verifies that po provider concurrent first lookups all observe consistent snapshot.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_ConcurrentFirstLookups_AllObserveConsistentSnapshotAsync() {
        var tempFile = await WritePoFileAsync("msgid \"key\"\nmsgstr \"value\"\n");
        try {
            var provider = new PoMessageProvider(tempFile);

            var lookups = Enumerable.Range(0, 64)
                .Select(_ => provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None).AsTask())
                .ToArray();
            var results = await Task.WhenAll(lookups);

            results.All(static result => result == "value").Should().BeTrue("all concurrent first lookups observe the same published snapshot");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider options mutation after construction does not affect behavior.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_OptionsMutationAfterConstruction_DoesNotAffectBehaviorAsync() {
        var firstFile = Path.Combine(Path.GetTempPath(), $"po-first-{Guid.NewGuid():N}.po");
        var secondFile = Path.Combine(Path.GetTempPath(), $"po-second-{Guid.NewGuid():N}.po");
        await File.WriteAllTextAsync(firstFile, "msgid \"key\"\nmsgstr \"first\"\n", CancellationToken.None);
        await File.WriteAllTextAsync(secondFile, "msgid \"key\"\nmsgstr \"second\"\n", CancellationToken.None);

        try {
            var options = new PoLocalizationOptions { FilePath = firstFile };
            var provider = new PoMessageProvider(options);

            options.FilePath = secondFile;

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first",
                "the file path must be snapshotted at construction");
        }
        finally {
            File.Delete(firstFile);
            File.Delete(secondFile);
        }
    }

    /// <summary>
    /// Verifies that po provider get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var tempFile = await WritePoFileAsync("msgid \"A\"\nmsgstr \"value\"\n");
        try {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var provider = new PoMessageProvider(tempFile);

            async Task ActAsync() => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, cancellation.Token);

            await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed before any source access and never becomes a missing-resource result");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider get plural template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_GetPluralTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var tempFile = await WritePoFileAsync("msgid \"A\"\nmsgstr \"value\"\n");
        try {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var provider = new PoMessageProvider(tempFile);

            async Task ActAsync() => await provider.GetPluralTemplateAsync("A", "B", 2, CultureInfo.InvariantCulture, cancellation.Token);

            await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider null arguments throw argument null exception.
    /// </summary>
    [TestMethod]
    public async Task PoProvider_NullArguments_ThrowArgumentNullExceptionAsync() {
        var tempFile = await WritePoFileAsync("msgid \"A\"\nmsgstr \"value\"\n");
        try {
            var provider = new PoMessageProvider(tempFile);

            Func<Task> nullKey = async () => await provider.GetMessageTemplateAsync(null!, CultureInfo.InvariantCulture, CancellationToken.None);
            Func<Task> nullCulture = async () => await provider.GetMessageTemplateAsync("A", null!, CancellationToken.None);
            Func<Task> nullSingular = async () => await provider.GetPluralTemplateAsync(null!, "B", 1, CultureInfo.InvariantCulture, CancellationToken.None);
            Func<Task> nullPlural = async () => await provider.GetPluralTemplateAsync("A", null!, 1, CultureInfo.InvariantCulture, CancellationToken.None);
            Func<Task> nullContext = async () => await provider.GetPluralTemplateWithContextAsync("A", "B", null!, 1, CultureInfo.InvariantCulture, CancellationToken.None);

            await nullKey.Should().ThrowAsync<ArgumentNullException>();
            await nullCulture.Should().ThrowAsync<ArgumentNullException>();
            await nullSingular.Should().ThrowAsync<ArgumentNullException>();
            await nullPlural.Should().ThrowAsync<ArgumentNullException>();
            await nullContext.Should().ThrowAsync<ArgumentNullException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po provider throws file not found exception when file does not exist.
    /// </summary>
    [TestMethod]
    public void PoProvider_ThrowsFileNotFoundException_WhenFileDoesNotExist() {
        var act = () => new PoMessageProvider("nonexistent.po");

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that po provider throws on null options.
    /// </summary>
    [TestMethod]
    public void PoProvider_ThrowsOnNullOptions() {
        var act = static () => new PoMessageProvider((PoLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
