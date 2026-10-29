//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the  health checks: provider/store
/// states and reasons (Healthy/Degraded/Unhealthy), description/data PII safety
/// (template text and exception messages never surface), and registration rules.
/// </summary>
[TestClass]
public class LocalizationHealthCheckTests {
    private const string TEMPLATE_MARKER = "SECRET-template-value";

    /// <summary>
    /// Verifies that provider probe resolved key reports healthy.
    /// </summary>
    [TestMethod]
    public async Task ProviderProbe_ResolvedKey_ReportsHealthyAsync() {
        var options = new LocalizationHealthChecksOptions { ProviderProbeKey = "probe", ProviderProbeCulture = "en" };
        var check = new LocalizationProviderHealthCheck(new CultureSensitiveProvider(value: TEMPLATE_MARKER), options);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("probe");
        result.Description.Should().NotContain(TEMPLATE_MARKER, "template text never appears in health descriptions");
        result.Data["probeKey"].Should().Be("probe");
        result.Data["culture"].Should().Be("en");
        result.Data.ContainsKey("durationMs").Should().BeTrue();
    }

    /// <summary>
    /// Verifies that provider probe unserved key reports degraded with documented reason.
    /// </summary>
    [TestMethod]
    public async Task ProviderProbe_UnservedKey_ReportsDegradedWithDocumentedReasonAsync() {
        var options = new LocalizationHealthChecksOptions { ProviderProbeKey = "probe", ProviderProbeCulture = "en" };
        var check = new LocalizationProviderHealthCheck(new CultureSensitiveProvider(value: null), options);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("reachable but does not serve probe key 'probe' for culture 'en'");
    }

    /// <summary>
    /// Verifies that provider probe throwing provider reports unhealthy with exception type and no leak.
    /// </summary>
    [TestMethod]
    public async Task ProviderProbe_ThrowingProvider_ReportsUnhealthyWithExceptionTypeAndNoLeakAsync() {
        var options = new LocalizationHealthChecksOptions { ProviderProbeKey = "probe", ProviderProbeCulture = "en" };
        var check = new LocalizationProviderHealthCheck(new ThrowingProvider(), options);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().NotContain(TEMPLATE_MARKER, "exception messages never surface in descriptions");
        result.Exception.Should().BeOfType<InvalidOperationException>();
        result.Data["exceptionType"].Should().Be(typeof(InvalidOperationException).FullName);
    }

    /// <summary>
    /// Verifies that provider probe no probe configured reports is degraded.
    /// </summary>
    [TestMethod]
    public async Task ProviderProbe_NoProbeConfigured_ReportsDegradedAsync() {
        var check = new LocalizationProviderHealthCheck(new CultureSensitiveProvider(value: TEMPLATE_MARKER), new LocalizationHealthChecksOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("No provider probe key is configured");
    }

    /// <summary>
    /// Verifies that store probe reachable store reports healthy.
    /// </summary>
    [TestMethod]
    public async Task StoreProbe_ReachableStore_ReportsHealthyAsync() {
        var check = new LocalizationStoreHealthCheck(new FakeStore(["en", "ar"]));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["supportedCultureCount"].Should().Be(2);
    }

    /// <summary>
    /// Verifies that store probe empty store reports degraded with documented reason.
    /// </summary>
    [TestMethod]
    public async Task StoreProbe_EmptyStore_ReportsDegradedWithDocumentedReasonAsync() {
        var check = new LocalizationStoreHealthCheck(new FakeStore([]));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("reachable but reports no supported cultures");
    }

    /// <summary>
    /// Verifies that store probe throwing store reports unhealthy with exception type.
    /// </summary>
    [TestMethod]
    public async Task StoreProbe_ThrowingStore_ReportsUnhealthyWithExceptionTypeAsync() {
        var check = new LocalizationStoreHealthCheck(new FakeStore([]) { ThrowOnProbe = true });

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["exceptionType"].Should().Be(typeof(InvalidOperationException).FullName);
    }

    /// <summary>
    /// Verifies that registration adds the configured checks.
    /// </summary>
    [TestMethod]
    public void Registration_AddsTheConfiguredChecks() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationStore>(new FakeStore(["en"]));
        services.AddLocalizationHealthChecks(static options => {
            options.ProviderProbeKey = "probe";
            options.ProviderProbeCulture = "en";
        });

        using var provider = services.BuildServiceProvider();
        var names = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Select(static registration => registration.Name)
            .ToList();

        names.Should().Contain("jeninnet-localization-provider");
        names.Should().Contain("jeninnet-localization-store");
    }

    /// <summary>
    /// Verifies that registration skips checks that are not is configured.
    /// </summary>
    [TestMethod]
    public void Registration_SkipsChecksThatAreNotConfigured() {
        var withoutStore = new ServiceCollection();
        withoutStore.AddLocalizationHealthChecks(static options => options.ProviderProbeKey = "probe");
        using(var provider = withoutStore.BuildServiceProvider()) {
            var names = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Select(static registration => registration.Name)
                .ToList();
            names.Should().Contain("jeninnet-localization-provider");
            names.Should().NotContain("jeninnet-localization-store", "the store check registers only when a store is registered");
        }

        var withoutProbeKey = new ServiceCollection();
        withoutProbeKey.AddSingleton<ILocalizationStore>(new FakeStore(["en"]));
        withoutProbeKey.AddLocalizationHealthChecks();
        using(var provider = withoutProbeKey.BuildServiceProvider()) {
            var names = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Select(static registration => registration.Name)
                .ToList();
            names.Should().NotContain("jeninnet-localization-provider", "the provider probe requires an explicit probe key (never dumps)");
            names.Should().Contain("jeninnet-localization-store");
        }
    }

    /// <summary>
    /// Verifies that registration validates arguments and options.
    /// </summary>
    [TestMethod]
    public void Registration_ValidatesArgumentsAndOptions() {
        Action nullServices = () => ((IServiceCollection)null!).AddLocalizationHealthChecks();
        nullServices.Should().Throw<ArgumentNullException>();

        var badCulture = new ServiceCollection();
        Action culture = () => badCulture.AddLocalizationHealthChecks(static options => {
            options.ProviderProbeKey = "probe";
            options.ProviderProbeCulture = "en!x";
        });
        culture.Should().Throw<CultureNotFoundException>();

        var blankKey = new ServiceCollection();
        Action key = () => blankKey.AddLocalizationHealthChecks(static options => options.ProviderProbeKey = "  ");
        key.Should().Throw<ArgumentException>();

        var twice = new ServiceCollection();
        twice.AddLocalizationHealthChecks();
        var second = Assert.ThrowsExactly<InvalidOperationException>(() => twice.AddLocalizationHealthChecks());
        second.Message.Should().Contain("already registered");
    }

    /// <summary>
    /// Verifies that registration health check service reports healthy end to end.
    /// </summary>
    [TestMethod]
    public async Task Registration_HealthCheckService_ReportsHealthyEndToEndAsync() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryLocalization(new Dictionary<string, string> { ["probe"] = "value" });
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddSingleton<ILocalizationStore>(new FakeStore(["en"]));
        services.AddLocalizationHealthChecks(static options => {
            options.ProviderProbeKey = "probe";
            options.ProviderProbeCulture = "en";
        });

        await using var provider = services.BuildServiceProvider();
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.CancellationToken);

        report.Status.Should().Be(HealthStatus.Healthy);
        report.Entries["jeninnet-localization-provider"].Status.Should().Be(HealthStatus.Healthy);
        report.Entries["jeninnet-localization-store"].Status.Should().Be(HealthStatus.Healthy);
    }

    /// <summary>
    /// Provides culture sensitive.
    /// </summary>
    /// <param name="value">The value.</param>
    private sealed class CultureSensitiveProvider(string? value) : IMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = culture;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(value);
        }
    }

    /// <summary>
    /// Provides throwing.
    /// </summary>
    private sealed class ThrowingProvider : IMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = key;
            _ = culture;
            _ = cancellationToken;
            throw new InvalidOperationException("provider failure containing " + TEMPLATE_MARKER);
        }
    }

    /// <summary>
    /// Stores fake.
    /// </summary>
    /// <param name="cultures">The cultures.</param>
    private sealed class FakeStore(IReadOnlyList<string> cultures) : ILocalizationStore {
        /// <summary>
        /// Gets or sets the throw on probe.
        /// </summary>
        public bool ThrowOnProbe { get; set; }

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if(ThrowOnProbe) {
                throw new InvalidOperationException("store failure");
            }

            return Task.FromResult(cultures.ToList());
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult<ILocalizationResourceSet?>(null);

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ILocalizationResourceSet>());

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            localizationResourceSet = null;
            return false;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
