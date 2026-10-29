//
// Copyright (c) Jeninnet. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// R8 cancellation behavioral tests: canonical async entry semantics, token propagation through
/// the canonical call chain, fallback cancellation, and cancellation-versus-missing semantics.
/// </summary>
[TestClass]
public class CancellationBehaviorTests {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo _frenchCanadian = CultureInfo.GetCultureInfo("fr-CA");

    /// <summary>
    /// Verifies that localize async pre cancelled token argument validation takes precedence.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_PreCancelledToken_ArgumentValidationTakesPrecedenceAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var localizer = CreateLocalizer(new RecordingProvider(static (_, _) => "template"));

        async Task NullKeyAsync() => await localizer.LocalizeAsync(null!, _noParameters, _french, cancellation.Token);
        async Task NullParametersAsync() => await localizer.LocalizeAsync("key", null!, _french, cancellation.Token);
        async Task NullCultureAsync() => await localizer.LocalizeAsync("key", _noParameters, null!, cancellation.Token);
        Func<Task> nullKey = NullKeyAsync;
        Func<Task> nullParameters = NullParametersAsync;
        Func<Task> nullCulture = NullCultureAsync;

        await nullKey.Should().ThrowAsync<ArgumentNullException>("argument validation retains precedence over cancellation at entry");
        await nullParameters.Should().ThrowAsync<ArgumentNullException>();
        await nullCulture.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = new RecordingProvider(static (_, _) => "template");
        var localizer = CreateLocalizer(provider);

        async Task ActAsync() => await localizer.LocalizeAsync("key", _noParameters, _french, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is never converted into a missing-resource result");
        provider.CallCount.Should().Be(0, "a pre-cancelled token must be observed before provider work");
    }

    /// <summary>
    /// Verifies that localize async convenience overload forwards token to culture aware overload.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_ConvenienceOverload_ForwardsTokenToCultureAwareOverloadAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = new RecordingProvider(static (_, _) => "template");
        var localizer = CreateLocalizer(provider);

        async Task ActAsync() => await localizer.LocalizeAsync("key", _noParameters, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        provider.CallCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that localize async token reaches provider implementation.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_TokenReachesProviderImplementationAsync() {
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingProvider(static (_, _) => "Bonjour");
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizeAsync("key", _noParameters, _french, cancellation.Token);

        result.Should().Be("Bonjour");
        provider.CallCount.Should().Be(1);
        provider.ReceivedTokens[0].Should().Be(cancellation.Token, "the same caller token must reach the provider");
    }

    /// <summary>
    /// Verifies that localize async cancellation during provider lookup stops fallback walk.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_CancellationDuringProviderLookup_StopsFallbackWalkAsync() {
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingProvider((_, _) => {
            cancellation.Cancel();
            return null;
        });
        var localizer = CreateLocalizer(provider);

        async Task ActAsync() => await localizer.LocalizeAsync("key", _noParameters, _frenchCanadian, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        provider.CallCount.Should().Be(1, "no parent-culture or default-culture lookup may follow observed cancellation");
        provider.RequestedCultures.Should().ContainSingle().Which.Should().Be("fr-CA");
    }

    /// <summary>
    /// Verifies that localize async provider throws operation canceled propagates without fallback.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_ProviderThrowsOperationCanceled_PropagatesWithoutFallbackAsync() {
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingProvider((_, token) => throw new OperationCanceledException(token));
        var localizer = CreateLocalizer(provider);

        async Task ActAsync() => await localizer.LocalizeAsync("key", _noParameters, _frenchCanadian, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        provider.CallCount.Should().Be(1, "a provider-observed cancellation must not trigger remaining fallback cultures");
    }

    /// <summary>
    /// Verifies that localize async default token preserves existing behavior.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_DefaultToken_PreservesExistingBehaviorAsync() {
        var provider = new RecordingProvider(static (_, _) => "Hallo");
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizeAsync("key", _noParameters, _french, CancellationToken.None);

        result.Should().Be("Hallo");
        provider.CallCount.Should().Be(1);
        provider.ReceivedTokens[0].CanBeCanceled.Should().BeFalse("the default token preserves the pre-cancellation behavior");
    }

    /// <summary>
    /// Verifies that culture aware dispatcher forwards token to selected provider.
    /// </summary>
    [TestMethod]
    public async Task CultureAwareDispatcher_ForwardsTokenToSelectedProviderAsync() {
        using var cancellation = new CancellationTokenSource();
        var inner = new RecordingProvider(static (_, _) => "Hallo");
        var dispatcher = new CultureAwareMessageProvider(new Dictionary<string, IMessageProvider> { ["nl"] = inner });

        var result = await dispatcher.GetMessageTemplateAsync("key", new CultureInfo("nl-NL"), cancellation.Token);

        result.Should().Be("Hallo");
        inner.ReceivedTokens[0].Should().Be(cancellation.Token, "the dispatcher must forward the caller token to the selected provider");
    }

    /// <summary>
    /// Verifies that database provider pre cancelled token throws before opening connection.
    /// </summary>
    [TestMethod]
    public async Task DatabaseProvider_PreCancelledToken_ThrowsBeforeOpeningConnectionAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = new SqliteMessageProvider(new SqliteLocalizationOptions {
            ConnectionString = "Data Source=:memory:",
            TableName = "LocalizationMessages",
        });

        async Task ActAsync() => await provider.GetMessageTemplateAsync("key", _french, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("the database provider observes cancellation at the canonical boundary before any connection work");
    }

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(IMessageProvider provider) =>
        new(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="handler">The request handler.</param>
    private sealed class RecordingProvider(Func<string, CancellationToken, string?> handler) : IMessageProvider {
        /// <summary>
        /// Gets or sets the call count.
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Gets the received tokens.
        /// </summary>
        public List<CancellationToken> ReceivedTokens { get; } = [];

        /// <summary>
        /// Gets the requested cultures.
        /// </summary>
        public List<string> RequestedCultures { get; } = [];

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            CallCount++;
            ReceivedTokens.Add(cancellationToken);
            RequestedCultures.Add(culture.Name);
            return ValueTask.FromResult(handler(culture.Name, cancellationToken));
        }
    }
}
