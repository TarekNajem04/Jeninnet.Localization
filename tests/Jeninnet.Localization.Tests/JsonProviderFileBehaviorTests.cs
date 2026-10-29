//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the json provider file behavior tests.
/// </summary>
[TestClass]
public class JsonProviderFileBehaviorTests {
    /// <summary>
    /// Verifies that json provider empty object returns null for any requested key.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_EmptyObject_ReturnsNullForAnyRequestedKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "{}", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            var result = await provider.GetMessageTemplateAsync("ANY_KEY", CultureInfo.InvariantCulture, CancellationToken.None);

            result.Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider empty string value is distinct from missing key.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_EmptyStringValue_IsDistinctFromMissingKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"EMPTY":"","REAL":"value"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("EMPTY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("");
            (await provider.GetMessageTemplateAsync("REAL", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
            (await provider.GetMessageTemplateAsync("MISSING", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider malformed json throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_MalformedJson_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"A":"unterminated""", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider duplicate keys last occurrence wins.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_DuplicateKeys_LastOccurrenceWinsAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"A":"first","A":"second"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("second");
            provider.Resource!.Entries.Should().ContainSingle();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider non string value throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_NonStringValue_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"A":42}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider null value is treated as missing for that key.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_NullValue_IsTreatedAsMissingForThatKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"A":null,"B":"real"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("real");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider uses ordinal case sensitive key matching.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_UsesOrdinalCaseSensitiveKeyMatchingAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"key":"lower","KEY":"upper"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("lower");
            (await provider.GetMessageTemplateAsync("KEY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("upper");
            (await provider.GetMessageTemplateAsync("Key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider loads once and ignores file changes after first lookup.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_LoadsOnceAndIgnoresFileChangesAfterFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"A":"first"}""", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");

            await File.WriteAllTextAsync(tempFile, """{"A":"second","B":"added"}""", TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider Utf8Bom and non ascii values load correctly.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_Utf8BomAndNonAsciiValues_LoadCorrectlyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string value = "مرحبا — ünïcödé ✓";
            const string json = """{"WELCOME":"مرحبا — ünïcödé ✓"}""";
            await File.WriteAllTextAsync(tempFile, json, new System.Text.UTF8Encoding(true), TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

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
