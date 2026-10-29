//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the file provider tests.
/// </summary>
[TestClass]
public class FileProviderTests {
    /// <summary>
    /// Verifies that memory provider returns template when key exists.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_ReturnsTemplate_WhenKeyExistsAsync() {
        var options = new MemoryLocalizationOptions {
            Messages = new Dictionary<string, string> {
                ["REQUIRED"] = "The field is required."
            }
        };
        var provider = new MemoryMessageProvider(options);

        var result = await provider.GetMessageTemplateAsync("REQUIRED", CultureInfo.CurrentCulture, CancellationToken.None);

        result.Should().Be("The field is required.");
    }

    /// <summary>
    /// Verifies that memory provider returns null when key does not exist.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_ReturnsNull_WhenKeyDoesNotExistAsync() {
        var options = new MemoryLocalizationOptions {
            Messages = new Dictionary<string, string>()
        };
        var provider = new MemoryMessageProvider(options);

        var result = await provider.GetMessageTemplateAsync("UNKNOWN", CultureInfo.CurrentCulture, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that memory provider ignores culture parameter.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_IgnoresCultureParameterAsync() {
        var options = new MemoryLocalizationOptions {
            Messages = new Dictionary<string, string> { ["REQUIRED"] = "Always returned" }
        };
        var provider = new MemoryMessageProvider(options);

        var result1 = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None);
        var result2 = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("ar-SY"), CancellationToken.None);
        var result3 = await provider.GetMessageTemplateAsync("REQUIRED", CultureInfo.InvariantCulture, CancellationToken.None);

        result1.Should().Be("Always returned");
        result2.Should().Be("Always returned");
        result3.Should().Be("Always returned");
    }

    /// <summary>
    /// Verifies that memory provider throws on null options.
    /// </summary>
    [TestMethod]
    public void MemoryProvider_ThrowsOnNullOptions() {
        var act = static () => new MemoryMessageProvider(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that memory provider throws on null messages.
    /// </summary>
    [TestMethod]
    public void MemoryProvider_ThrowsOnNullMessages() {
        var options = new MemoryLocalizationOptions {
            Messages = null!
        };

        var act = () => new MemoryMessageProvider(options);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that json provider returns template when key exists.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_ReturnsTemplate_WhenKeyExistsAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"REQUIRED":"The field is required.","EMAIL":"Email is invalid."}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var options = new JsonLocalizationOptions { FilePath = tempFile };
            var provider = new JsonMessageProvider(options);

            var result = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None);

            result.Should().Be("The field is required.");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider returns null when key does not exist.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_ReturnsNull_WhenKeyDoesNotExistAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"ONLY_KEY":"value"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var options = new JsonLocalizationOptions { FilePath = tempFile };
            var provider = new JsonMessageProvider(options);

            var result = await provider.GetMessageTemplateAsync("MISSING", new CultureInfo("en"), CancellationToken.None);

            result.Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider ignores culture parameter.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_IgnoresCultureParameterAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"REQUIRED":"Always returned"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var options = new JsonLocalizationOptions { FilePath = tempFile };
            var provider = new JsonMessageProvider(options);

            var result1 = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None);
            var result2 = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("ar-SY"), CancellationToken.None);
            var result3 = await provider.GetMessageTemplateAsync("REQUIRED", CultureInfo.InvariantCulture, CancellationToken.None);

            result1.Should().Be("Always returned");
            result2.Should().Be("Always returned");
            result3.Should().Be("Always returned");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider throws file not found exception when file does not exist.
    /// </summary>
    [TestMethod]
    public void JsonProvider_ThrowsFileNotFoundException_WhenFileDoesNotExist() {
        var options = new JsonLocalizationOptions { FilePath = "nonexistent.json" };

        var act = () => new JsonMessageProvider(options);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that xml provider returns template when key exists.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_ReturnsTemplate_WhenKeyExistsAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            var doc = new XDocument(
                new XElement("messages",
                    new XElement("REQUIRED", "The field is required."),
                    new XElement("EMAIL", "Email is invalid.")
                )
            );
            doc.Save(tempFile);

            var options = new XmlLocalizationOptions { FilePath = tempFile };
            var provider = new XmlMessageProvider(options);

            var result = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None);

            result.Should().Be("The field is required.");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider returns null when key does not exist.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_ReturnsNull_WhenKeyDoesNotExistAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            var doc = new XDocument(new XElement("messages"));
            doc.Save(tempFile);

            var options = new XmlLocalizationOptions { FilePath = tempFile };
            var provider = new XmlMessageProvider(options);

            var result = await provider.GetMessageTemplateAsync("MISSING", new CultureInfo("en"), CancellationToken.None);

            result.Should().BeNull();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider ignores culture parameter.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_IgnoresCultureParameterAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            var doc = new XDocument(
                new XElement("messages",
                    new XElement("REQUIRED", "Culture independent result")
                )
            );
            doc.Save(tempFile);

            var options = new XmlLocalizationOptions { FilePath = tempFile };
            var provider = new XmlMessageProvider(options);

            var result1 = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None);
            var result2 = await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("ar-SY"), CancellationToken.None);

            result1.Should().Be("Culture independent result");
            result2.Should().Be("Culture independent result");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider throws file not found exception when file does not exist.
    /// </summary>
    [TestMethod]
    public void XmlProvider_ThrowsFileNotFoundException_WhenFileDoesNotExist() {
        var options = new XmlLocalizationOptions { FilePath = "nonexistent.xml" };

        var act = () => new XmlMessageProvider(options);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that res XProvider throws on null options.
    /// </summary>
    [TestMethod]
    public void ResXProvider_ThrowsOnNullOptions() {
        var act = static () => new ResXMessageProvider(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that res XProvider throws on empty base name.
    /// </summary>
    [TestMethod]
    public void ResXProvider_ThrowsOnEmptyBaseName() {
        var options = new ResXLocalizationOptions {
            BaseName = "",
            ResourceAssembly = typeof(FileProviderTests).Assembly
        };

        var act = () => new ResXMessageProvider(options);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that res XProvider throws on null assembly.
    /// </summary>
    [TestMethod]
    public void ResXProvider_ThrowsOnNullAssembly() {
        var options = new ResXLocalizationOptions {
            BaseName = "TestResources",
            ResourceAssembly = null!
        };

        var act = () => new ResXMessageProvider(options);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
