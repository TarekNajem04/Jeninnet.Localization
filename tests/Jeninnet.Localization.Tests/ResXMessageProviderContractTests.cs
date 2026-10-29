//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused runtime-contract evidence for <see cref="ResXMessageProvider"/>: R8 cancellation
/// observation at the canonical lookup boundary and deterministic repeated lookups against
/// the <see cref="System.Resources.ResourceManager"/>-backed embedded resource sets.
/// </summary>
[TestClass]
public class ResXMessageProviderContractTests {
    /// <summary>
    /// Verifies that get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var provider = CreateProvider();

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        async Task ActAsync() => await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed at the canonical lookup boundary and never becomes a missing-resource result");
    }

    /// <summary>
    /// Verifies that get message template async repeated lookups resolve deterministically.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_RepeatedLookups_ResolveDeterministicallyAsync() {
        var provider = CreateProvider();

        for(var i = 0; i < 16; i++) {
            (await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("Hello from ResX",
                "a present key must resolve to the same template on every lookup");
            (await provider.GetMessageTemplateAsync("Missing_Key_ZZZ", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull(
                "a missing key must remain missing across repeated lookups");
        }
    }

    /// <summary>
    /// Verifies that get message template async null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_ThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null culture");
    }

    /// <summary>
    /// Verifies that get message template async null culture with pre cancelled token still throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_WithPreCancelledToken_StillThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", null!, cancellation.Token);

        await act.Should().ThrowAsync<ArgumentNullException>("argument validation retains precedence over cancellation at the provider boundary");
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <returns>The provider result.</returns>
    private static ResXMessageProvider CreateProvider() =>
        new(new ResXLocalizationOptions {
            BaseName = "Jeninnet.Localization.Tests.TestResources",
            ResourceAssembly = typeof(ResXMessageProviderContractTests).Assembly
        });
}
