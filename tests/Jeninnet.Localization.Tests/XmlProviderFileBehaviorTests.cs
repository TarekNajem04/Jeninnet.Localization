//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the xml provider file behavior tests.
/// </summary>
[TestClass]
public class XmlProviderFileBehaviorTests {
    /// <summary>
    /// Verifies that xml provider throws on null options.
    /// </summary>
    [TestMethod]
    public void XmlProvider_ThrowsOnNullOptions() {
        var act = static () => new XmlMessageProvider((XmlLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that xml provider empty root returns null for any requested key.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_EmptyRoot_ReturnsNullForAnyRequestedKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages />", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            var result = await provider.GetMessageTemplateAsync("ANY_KEY", CultureInfo.InvariantCulture, CancellationToken.None);

            result.Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider empty file throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_EmptyFile_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<System.Xml.XmlException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider malformed xml throws on first lookup.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_MalformedXml_ThrowsOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><A>unterminated", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<System.Xml.XmlException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider duplicate keys throw on first lookup.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_DuplicateKeys_ThrowOnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><A>first</A><A>second</A></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);
            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider empty string value is distinct from missing key.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_EmptyStringValue_IsDistinctFromMissingKeyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><EMPTY></EMPTY><REAL>value</REAL></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("EMPTY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("");
            (await provider.GetMessageTemplateAsync("REAL", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");
            (await provider.GetMessageTemplateAsync("MISSING", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider whitespace in template is preserved.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_WhitespaceInTemplate_IsPreservedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><A>  padded  </A></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("  padded  ");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider special characters are is decoded.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_SpecialCharacters_AreDecodedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><A>a &amp; b &lt;c&gt; &#x3C0; &apos;q&apos;</A></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("a & b <c> π 'q'");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider comments are is ignored.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_Comments_AreIgnoredAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><!--ignored payload--><A>1</A><!--trailing--></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("1");
            provider.Resource!.Entries.Should().ContainSingle();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider root name is not is enforced.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_RootName_IsNotEnforcedAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<dictionary><A>1</A></dictionary>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("1");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider nested elements are flattened into value.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_NestedElements_AreFlattenedIntoValueAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><A><inner>text</inner></A></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("text");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider uses ordinal case sensitive key matching.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_UsesOrdinalCaseSensitiveKeyMatchingAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><key>lower</key><KEY>upper</KEY></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("lower");
            (await provider.GetMessageTemplateAsync("KEY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("upper");
            (await provider.GetMessageTemplateAsync("Key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider Utf8Bom declaration non ascii load correctly.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_Utf8BomDeclarationNonAscii_LoadCorrectlyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string value = "مرحبا — ünïcödé ✓";
            const string xml = """<?xml version="1.0" encoding="utf-8"?><messages><WELCOME>مرحبا — ünïcödé ✓</WELCOME></messages>""";
            await File.WriteAllTextAsync(tempFile, xml, new System.Text.UTF8Encoding(true), TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("WELCOME", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be(value);
            provider.Resource!.Entries.Should().ContainSingle()
                .Which.Template.Should().Be(value);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider loads once and ignores file changes after first lookup.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_LoadsOnceAndIgnoresFileChangesAfterFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><A>first</A></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");

            await File.WriteAllTextAsync(tempFile, "<messages><A>second</A><B>added</B></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("first");
            (await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider does not resolve external entities on first lookup.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_DoesNotResolveExternalEntities_OnFirstLookupAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            // If the external entity '&ext;' were resolved against the missing
            // file:///nonexistent target, loading would throw FileNotFoundException.
            // Completion with a non-message value proves external resolution is disabled.
            await File.WriteAllTextAsync(tempFile,
                """<!DOCTYPE messages [<!ENTITY ext SYSTEM "file:///nonexistent">]><messages><A>&ext;</A></messages>""",
                TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new XmlMessageProvider(tempFile);

            var result = await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);

            string.IsNullOrEmpty(result).Should().BeTrue();
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
