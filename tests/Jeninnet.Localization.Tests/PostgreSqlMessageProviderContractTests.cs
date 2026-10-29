//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused contract-boundary evidence for <see cref="PostgreSqlMessageProvider"/>: R8 cancellation
/// observation and contract-defined invalid input, verified on the concrete provider before any
/// database access is attempted (the shared base validates key, culture, and cancellation before
/// creating a connection).
/// </summary>
[TestClass]
public class PostgreSqlMessageProviderContractTests {
    /// <summary>
    /// Verifies that get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = CreateProvider();

        async Task ActAsync() => await provider.GetMessageTemplateAsync("REQUIRED", new CultureInfo("en"), cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed at the canonical lookup boundary and never becomes a missing-resource result");
    }

    /// <summary>
    /// Verifies that get message template async null key throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullKey_ThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync(null!, new CultureInfo("en"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null key");
    }

    /// <summary>
    /// Verifies that get message template async null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_ThrowsArgumentNullExceptionAsync() {
        var provider = CreateProvider();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("REQUIRED", null!, CancellationToken.None);

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

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("REQUIRED", null!, cancellation.Token);

        await act.Should().ThrowAsync<ArgumentNullException>("argument validation retains precedence over cancellation at the provider boundary");
    }

    /// <summary>
    /// Verifies that constructor null options throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Constructor_NullOptions_ThrowsArgumentNullException() {
        Action act = () => _ = new PostgreSqlMessageProvider(null!);

        act.Should().Throw<ArgumentNullException>("the canonical provider rejects a null options instance at construction");
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <returns>The provider result.</returns>
    private static PostgreSqlMessageProvider CreateProvider() =>
        new(new PostgreSqlLocalizationOptions {
            ConnectionString = "Host=localhost;Port=5432;Database=localization;",
            TableName = "LocalizationMessages"
        });
}
