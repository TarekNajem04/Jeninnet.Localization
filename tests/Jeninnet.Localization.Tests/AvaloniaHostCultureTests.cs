//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Verifies the Avalonia host culture service, its file store, and its DI composition.
/// </summary>
[TestClass]
public class AvaloniaHostCultureTests {
    /// <summary>
    /// Verifies that setting a culture applies it, persists it, and raises the change event.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task SetCulture_AppliesPersistsAndRaisesEventAsync() {
        using var scope = new CultureScope();
        var store = new FakeCultureStore();
        var service = new HostCultureService(new HostCultureOptions(), store);
        HostCultureChangedEventArgs? change = null;
        service.CultureChanged += (_, args) => change = args;

        await service.SetCultureAsync("ar", TestContext.CancellationToken);

        service.Culture.Should().Be("ar");
        service.IsRightToLeft.Should().BeTrue();
        store.Value.Should().Be("ar");
        change.Should().NotBeNull();
        change!.NewCulture.Name.Should().Be("ar");
        change.OldCulture.Should().BeNull();
    }

    /// <summary>
    /// Verifies that initialization restores the persisted culture before the default.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Initialize_RestoresPersistedCultureAsync() {
        using var scope = new CultureScope();
        var store = new FakeCultureStore { Value = "ar" };
        var service = new HostCultureService(new HostCultureOptions { DefaultCulture = "en" }, store);

        var applied = await service.InitializeAsync(TestContext.CancellationToken);

        applied.Should().BeTrue();
        service.Culture.Should().Be("ar");
    }

    /// <summary>
    /// Verifies that initialization applies the configured default when nothing is persisted.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Initialize_AppliesDefaultCultureAsync() {
        using var scope = new CultureScope();
        var service = new HostCultureService(new HostCultureOptions { DefaultCulture = "en" }, new FakeCultureStore());

        var applied = await service.InitializeAsync(TestContext.CancellationToken);

        applied.Should().BeTrue();
        service.Culture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that an unsupported culture is rejected and nothing is persisted.
    /// </summary>
    [TestMethod]
    public async Task SetCulture_RejectsUnsupportedCultureAsync() {
        var store = new FakeCultureStore();
        var options = new HostCultureOptions();
        options.SupportedCultures.Add("en");
        options.SupportedCultures.Add("ar");
        var service = new HostCultureService(options, store);

        var act = async () => await service.SetCultureAsync("fr", TestContext.CancellationToken);

        (await act.Should().ThrowAsync<ArgumentException>()).WithMessage("*not in the configured*");
        store.Value.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the file store round-trips a culture name and clears it.
    /// </summary>
    [TestMethod]
    public async Task FileStore_RoundTripsAndClearsAsync() {
        var directory = Path.Combine(Path.GetTempPath(), "jeninnet-avalonia-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "culture.txt");
        try {
            var store = new FileHostCultureStore(path);
            (await store.ReadAsync(TestContext.CancellationToken)).Should().BeNull();

            await store.WriteAsync("ar", TestContext.CancellationToken);
            (await store.ReadAsync(TestContext.CancellationToken)).Should().Be("ar");

            await store.ClearAsync(TestContext.CancellationToken);
            (await store.ReadAsync(TestContext.CancellationToken)).Should().BeNull();
        }
        finally {
            if(Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that the Avalonia registration resolves the service and the file-backed store.
    /// </summary>
    [TestMethod]
    public void AddAvaloniaLocalization_RegistersServiceAndFileStore() {
        var path = Path.Combine(Path.GetTempPath(), "jeninnet-avalonia-" + Guid.NewGuid().ToString("N"), "culture.txt");
        try {
            using var provider = new ServiceCollection()
                .AddAvaloniaLocalization(options => options.CultureStoreFilePath = path)
                .BuildServiceProvider();

            provider.GetRequiredService<HostCultureService>().Should().NotBeNull();
            provider.GetRequiredService<IHostCultureStore>().Should().BeOfType<FileHostCultureStore>();
        }
        finally {
            var directory = Path.GetDirectoryName(path);
            if(directory is not null && Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that a right-to-left culture maps to Avalonia's right-to-left flow direction.
    /// </summary>
    [TestMethod]
    public void ToFlowDirection_MapsRightToLeftCulture() {
        var service = new HostCultureService(
            new HostCultureOptions { DefaultCulture = "ar" },
            new FakeCultureStore());

        service.ToFlowDirection().Should().Be(Avalonia.Media.FlowDirection.RightToLeft);
    }

    private sealed class FakeCultureStore : IHostCultureStore {
        public string? Value { get; set; }

        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Value);

        public ValueTask WriteAsync(string cultureName, CancellationToken cancellationToken = default) {
            Value = cultureName;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default) {
            Value = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CultureScope : IDisposable {
        private readonly CultureInfo _currentCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _currentUiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        public void Dispose() {
            CultureInfo.CurrentCulture = _currentCulture;
            CultureInfo.CurrentUICulture = _currentUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
        }
    }

    public required TestContext TestContext { get; set; }
}
