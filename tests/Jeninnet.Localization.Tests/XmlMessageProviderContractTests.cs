//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused runtime-contract evidence for <see cref="XmlMessageProvider"/>: R8 cancellation
/// observation at the canonical lookup boundary and deterministic repeated lookups over a
/// lazy, one-shot snapshot load (file changes after first lookup are never observed).
/// </summary>
[TestClass]
public class XmlMessageProviderContractTests {
    /// <summary>
    /// Verifies that get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><REQUIRED>The field is required.</REQUIRED></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new XmlMessageProvider(tempFile);

            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            async Task ActAsync() => await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), cancellation.Token);

            await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed at the canonical lookup boundary and never becomes a missing-resource result");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that get message template async repeated lookups resolve deterministically.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_RepeatedLookups_ResolveDeterministicallyAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><REQUIRED>The field is required.</REQUIRED></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new XmlMessageProvider(tempFile);

            for(var i = 0; i < 16; i++) {
                (await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None)).Should().Be("The field is required.",
                    "a present key must resolve to the same template on every lookup");
                (await provider.GetMessageTemplateAsync("MISSING", new CultureInfo("en"), CancellationToken.None)).Should().BeNull(
                    "a missing key must remain missing across repeated lookups");
            }
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that get message template async null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_ThrowsArgumentNullExceptionAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><REQUIRED>The field is required.</REQUIRED></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new XmlMessageProvider(tempFile);

            Func<Task> act = async () => await provider.GetMessageTemplateAsync("REQUIRED", null!, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null culture");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that get message template async null culture with pre cancelled token still throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_WithPreCancelledToken_StillThrowsArgumentNullExceptionAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><REQUIRED>The field is required.</REQUIRED></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new XmlMessageProvider(tempFile);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            Func<Task> act = async () => await provider.GetMessageTemplateAsync("REQUIRED", null!, cancellation.Token);

            await act.Should().ThrowAsync<ArgumentNullException>("argument validation retains precedence over cancellation at the provider boundary");
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
