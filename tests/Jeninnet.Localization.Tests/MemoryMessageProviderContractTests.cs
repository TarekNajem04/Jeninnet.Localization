//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused runtime-contract evidence for <see cref="MemoryMessageProvider"/>: R8 cancellation
/// observation, deterministic repeated lookups, exact-key ordinal matching (no fuzzy or
/// case-insensitive resolution, no provider-level fallback), and contract-defined invalid input.
/// </summary>
[TestClass]
public class MemoryMessageProviderContractTests {
    /// <summary>
    /// Verifies that get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = CreateProvider("REQUIRED", "The field is required.");

        async Task ActAsync() => await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed at the canonical lookup boundary and never becomes a missing-resource result");
    }

    /// <summary>
    /// Verifies that get message template async repeated lookups resolve deterministically.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_RepeatedLookups_ResolveDeterministicallyAsync() {
        var provider = CreateProvider("REQUIRED", "The field is required.");

        for(var i = 0; i < 16; i++) {
            (await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), CancellationToken.None)).Should().Be("The field is required.",
                "a present key must resolve to the same template on every lookup");
            (await provider.GetMessageTemplateAsync("MISSING", new CultureInfo("en"), CancellationToken.None)).Should().BeNull(
                "a missing key must remain missing across repeated lookups");
        }
    }

    /// <summary>
    /// Verifies that get message template async exact key match is case sensitive.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_ExactKeyMatch_IsCaseSensitiveAsync() {
        var provider = CreateProvider("Key", "value");

        var exact = await provider.GetMessageTemplateAsync("Key", new CultureInfo("en"), CancellationToken.None);
        var otherCase = await provider.GetMessageTemplateAsync("key", new CultureInfo("en"), CancellationToken.None);

        exact.Should().Be("value");
        otherCase.Should().BeNull("lookup resolves only the exact requested key with the ordinal comparer; no case-insensitive matching");
    }

    /// <summary>
    /// Verifies that get message template async null key throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullKey_ThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider("REQUIRED", "The field is required.");

        Func<Task> act = async () => await provider.GetMessageTemplateAsync(null!, new CultureInfo("en"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null key");
    }

    /// <summary>
    /// Verifies that get message template async null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_ThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider("REQUIRED", "The field is required.");

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("REQUIRED", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null culture");
    }

    /// <summary>
    /// Verifies that get message template async null culture with pre cancelled token still throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_WithPreCancelledToken_StillThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider("REQUIRED", "The field is required.");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("REQUIRED", null!, cancellation.Token);

        await act.Should().ThrowAsync<ArgumentNullException>("argument validation retains precedence over cancellation at the provider boundary");
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="template">The message template.</param>
    /// <returns>The provider result.</returns>
    private static MemoryMessageProvider CreateProvider(string key, string template) =>
        new(new MemoryLocalizationOptions {
            Messages = new Dictionary<string, string> { [key] = template }
        });
}
