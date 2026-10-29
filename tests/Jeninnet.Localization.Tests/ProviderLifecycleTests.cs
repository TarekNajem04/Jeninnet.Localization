//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// / provider lifecycle behavioral tests: immutable construction snapshots for options
/// and synchronized one-shot initialization with retryable failures for file providers.
/// </summary>
[TestClass]
public class ProviderLifecycleTests {
    /// <summary>
    /// Verifies that memory provider options mutation after construction does not affect behavior.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_OptionsMutationAfterConstruction_DoesNotAffectBehaviorAsync() {
        var messages = new Dictionary<string, string> { ["key"] = "value" };
        var options = new MemoryLocalizationOptions { Messages = messages };
        var provider = new MemoryMessageProvider(options);

        messages["key"] = "mutated";
        messages["added"] = "surprise";

        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
            "the provider must snapshot the message table at construction");
        (await provider.GetMessageTemplateAsync("added", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull(
            "post-construction option mutations must not change provider behavior");
    }

    /// <summary>
    /// Verifies that json provider options mutation after construction does not affect behavior.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_OptionsMutationAfterConstruction_DoesNotAffectBehaviorAsync() {
        var firstFile = Path.Combine(Path.GetTempPath(), $"lifecycle-json-first-{Guid.NewGuid():N}.json");
        var secondFile = Path.Combine(Path.GetTempPath(), $"lifecycle-json-second-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(firstFile, """{"key":"first"}""", CancellationToken.None);
        await File.WriteAllTextAsync(secondFile, """{"key":"second"}""", CancellationToken.None);

        try {
            var options = new JsonLocalizationOptions { FilePath = firstFile };
            var provider = new JsonMessageProvider(options);

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
    /// Verifies that json provider concurrent first lookups all observe consistent snapshot.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_ConcurrentFirstLookups_AllObserveConsistentSnapshotAsync() {
        var file = Path.Combine(Path.GetTempPath(), $"lifecycle-json-concurrent-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, """{"key":"value"}""", CancellationToken.None);

        try {
            var provider = new JsonMessageProvider(file);

            var lookups = Enumerable.Range(0, 64)
                .Select(_ => provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None).AsTask())
                .ToArray();
            var results = await Task.WhenAll(lookups);

            results.All(static result => result == "value").Should().BeTrue("all concurrent first lookups observe the same published snapshot");
        }
        finally {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Verifies that json provider loaded snapshot survives file removal.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_LoadedSnapshotSurvivesFileRemovalAsync() {
        var file = Path.Combine(Path.GetTempPath(), $"lifecycle-json-once-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, """{"key":"value"}""", CancellationToken.None);

        var provider = new JsonMessageProvider(file);
        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");

        File.Delete(file);

        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
            "the provider loads the file exactly once and serves the immutable snapshot afterwards");
    }

    /// <summary>
    /// Verifies that json provider failed initialization is retryable.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_FailedInitialization_IsRetryableAsync() {
        var file = Path.Combine(Path.GetTempPath(), $"lifecycle-json-retry-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(file, """{"key":"value"}""", CancellationToken.None);

        var provider = new JsonMessageProvider(file);
        File.Delete(file);

        async Task FirstLookupAsync() => await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None);

        await ((Func<Task>)FirstLookupAsync).Should().ThrowAsync<IOException>("a failed initialization surfaces the failure and is not cached");

        await File.WriteAllTextAsync(file, """{"key":"recovered"}""", CancellationToken.None);
        try {
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("recovered",
                "a later lookup retries initialization after a failure");
        }
        finally {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Verifies that xml provider options mutation after construction does not affect behavior.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_OptionsMutationAfterConstruction_DoesNotAffectBehaviorAsync() {
        var firstFile = Path.Combine(Path.GetTempPath(), $"lifecycle-xml-first-{Guid.NewGuid():N}.xml");
        var secondFile = Path.Combine(Path.GetTempPath(), $"lifecycle-xml-second-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(firstFile, "<messages><key>first</key></messages>", CancellationToken.None);
        await File.WriteAllTextAsync(secondFile, "<messages><key>second</key></messages>", CancellationToken.None);

        try {
            var options = new XmlLocalizationOptions { FilePath = firstFile };
            var provider = new XmlMessageProvider(options);

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
    /// Verifies that xml provider concurrent first lookups all observe consistent snapshot.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_ConcurrentFirstLookups_AllObserveConsistentSnapshotAsync() {
        var file = Path.Combine(Path.GetTempPath(), $"lifecycle-xml-concurrent-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(file, "<messages><key>value</key></messages>", CancellationToken.None);

        try {
            var provider = new XmlMessageProvider(file);

            var lookups = Enumerable.Range(0, 64)
                .Select(_ => provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None).AsTask())
                .ToArray();
            var results = await Task.WhenAll(lookups);

            results.All(static result => result == "value").Should().BeTrue("all concurrent first lookups observe the same published snapshot");
        }
        finally {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Verifies that xml provider loaded snapshot survives file removal.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_LoadedSnapshotSurvivesFileRemovalAsync() {
        var file = Path.Combine(Path.GetTempPath(), $"lifecycle-xml-once-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(file, "<messages><key>value</key></messages>", CancellationToken.None);

        var provider = new XmlMessageProvider(file);
        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value");

        File.Delete(file);

        (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("value",
            "the provider loads the file exactly once and serves the immutable snapshot afterwards");
    }

    /// <summary>
    /// Verifies that xml provider failed initialization is retryable.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_FailedInitialization_IsRetryableAsync() {
        var file = Path.Combine(Path.GetTempPath(), $"lifecycle-xml-retry-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(file, "<messages><key>value</key></messages>", CancellationToken.None);

        var provider = new XmlMessageProvider(file);
        File.Delete(file);

        async Task FirstLookupAsync() => await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None);

        await ((Func<Task>)FirstLookupAsync).Should().ThrowAsync<IOException>("a failed initialization surfaces the failure and is not cached");

        await File.WriteAllTextAsync(file, "<messages><key>recovered</key></messages>", CancellationToken.None);
        try {
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("recovered",
                "a later lookup retries initialization after a failure");
        }
        finally {
            File.Delete(file);
        }
    }

    /// <summary>
    /// Verifies that res XProvider options mutation after construction does not affect behavior.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_OptionsMutationAfterConstruction_DoesNotAffectBehaviorAsync() {
        var options = new ResXLocalizationOptions {
            BaseName = "Jeninnet.Localization.Tests.TestResources",
            ResourceAssembly = typeof(ProviderLifecycleTests).Assembly,
        };
        var provider = new ResXMessageProvider(options);

        options.BaseName = "Jeninnet.Localization.Tests.DoesNotExistAfterConstruction";

        (await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("Hello from ResX",
            "the resource manager is built from the construction snapshot");
    }
}
