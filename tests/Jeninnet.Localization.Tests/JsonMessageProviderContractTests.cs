//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused runtime-contract evidence for <see cref="JsonMessageProvider"/>: R8 cancellation
/// observation, JSON-source representation (top-level null document), and contract-defined
/// invalid configuration. Parsing, malformed-representation, duplicate-key, encoding, missing-source,
/// and lifecycle behavior are covered by <see cref="JsonProviderFileBehaviorTests"/>,
/// <see cref="FileProviderTests"/>, and <see cref="ProviderLifecycleTests"/>.
/// </summary>
[TestClass]
public class JsonMessageProviderContractTests {
    /// <summary>
    /// Verifies that get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"A":"value"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var provider = new JsonMessageProvider(tempFile);

            async Task ActAsync() => await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, cancellation.Token);

            await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed before any source access and never becomes a missing-resource result");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that get message template async null document is treated as empty source.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullDocument_IsTreatedAsEmptySourceAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "null", TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            (await provider.GetMessageTemplateAsync("ANY_KEY", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull(
                "a top-level JSON null deserializes to an empty message set, not a failure");
            provider.Resource.Should().NotBeNull("the load completes and publishes a canonical resource");
            provider.Resource!.Entries.Should().BeEmpty();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that ctor null options throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Ctor_NullOptions_ThrowsArgumentNullException() {
        var act = static () => new JsonMessageProvider((JsonLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that get message template async null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_ThrowsArgumentNullExceptionAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"A":"value"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new JsonMessageProvider(tempFile);

            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", null!, CancellationToken.None);

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
            await File.WriteAllTextAsync(tempFile, """{"A":"value"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new JsonMessageProvider(tempFile);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", null!, cancellation.Token);

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
