//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the ini provider file behavior tests.
/// </summary>
[TestClass]
public class IniProviderFileBehaviorTests {
    /// <summary>
    /// Verifies that ini provider empty file returns null for any requested key.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_EmptyFile_ReturnsNullForAnyRequestedKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

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
    /// Verifies that ini provider blank lines and comments are ignored.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_BlankLinesAndComments_AreIgnoredAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "; leading comment\n# hash comment\n\nA = 1\n\n; trailing comment\n";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("1");
            provider.Resource!.Entries.Should().ContainSingle();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider empty string value is distinct from missing key.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_EmptyStringValue_IsDistinctFromMissingKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "EMPTY =\nREAL = value";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("EMPTY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("");
            (await provider.GetMessageTemplateAsync("REAL", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
            (await provider.GetMessageTemplateAsync("MISSING", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider supports both equals and colon separators.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_ColonSeparator_IsSupportedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "A : one\nB = two";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("one");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("two");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider section headers qualify keys.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_SectionHeaders_QualifyKeysAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "BARE = root\n[Validation]\nRequired = This field is required.\n";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

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
    /// Verifies that ini provider trims whitespace around keys values and section names.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_WhitespaceAroundKeysValuesAndSections_IsTrimmedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "  A   =   spaced  \n[  Validation  ]\n  Required  =  value  ";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("spaced");
            (await provider.GetMessageTemplateAsync("Validation.Required", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider missing separator throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_MissingSeparator_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "NO_SEPARATOR_HERE", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
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
    /// Verifies that ini provider empty key throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_EmptyKey_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, " = value", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider unclosed section throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_UnclosedSection_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "[Validation\nA = 1", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            var exception = await act.Should().ThrowAsync<InvalidDataException>();
            exception.Which.Message.Should().Contain("line 1");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider empty section name throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_EmptySectionName_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "[]\nA = 1", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider duplicate qualified keys throw on first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_DuplicateQualifiedKeys_ThrowOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "[Validation]\nRequired = first\nRequired = second";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
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
    /// Verifies that ini provider duplicate bare keys throw on first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_DuplicateBareKeys_ThrowOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "A = first\nA = second";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider failed load never publishes partial tables and retries.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_FailedLoad_NeverPublishesPartialTablesAndRetriesAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "A = ok\nBROKEN LINE", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidDataException>("a failed load publishes nothing");
            provider.Resource.Should().BeNull();

            await File.WriteAllTextAsync(tempFile, "A = fixed", CancellationToken.None);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None))
                .Should().Be("fixed", "a later lookup retries initialization after a failure");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider uses ordinal case sensitive key matching.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_UsesOrdinalCaseSensitiveKeyMatchingAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string ini = "key = lower\nKEY = upper";
            await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("lower");
            (await provider.GetMessageTemplateAsync("KEY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("upper");
            (await provider.GetMessageTemplateAsync("Key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider loads once and ignores file changes after first lookup.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_LoadsOnceAndIgnoresFileChangesAfterFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "A = first", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");

            await File.WriteAllTextAsync(tempFile, "A = second\nB = added", TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ini provider Utf8Bom and non ascii values load correctly.
    /// </summary>
    [TestMethod]
    public async Task IniProvider_Utf8BomAndNonAsciiValues_LoadCorrectlyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string value = "مرحبا — ünïcödé ✓";
            const string ini = "WELCOME = مرحبا — ünïcödé ✓";
            await File.WriteAllTextAsync(tempFile, ini, new UTF8Encoding(true), TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new IniMessageProvider(tempFile);

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
