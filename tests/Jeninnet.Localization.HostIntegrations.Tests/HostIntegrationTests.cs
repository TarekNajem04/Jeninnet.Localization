//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.HostIntegrations.Tests;

/// <summary>
/// Composition tests for the platform host integrations.
/// </summary>
[TestClass]
public class HostIntegrationTests {
    /// <summary>
    /// Verifies that MAUI registration resolves the host culture service and maps Arabic to RTL.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task MauiRegistration_ResolvesServiceAndRtlDirectionAsync() {
        var store = new MauiFakeCultureStore();
        using var provider = new ServiceCollection()
            .AddSingleton<MauiHostCultureStore>(store)
            .AddMauiLocalization(options => options.SupportedCultures.Add("ar"))
            .BuildServiceProvider();

        var culture = provider.GetRequiredService<MauiHostCultureService>();
        await culture.SetCultureAsync("ar");

        culture.IsRightToLeft.Should().BeTrue();
        culture.ToFlowDirection().Should().Be(Microsoft.Maui.FlowDirection.RightToLeft);
        store.Value.Should().Be("ar");
    }

    /// <summary>
    /// Verifies that WinUI registration resolves the host culture service and maps Arabic to RTL.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task WinUiRegistration_ResolvesServiceAndRtlDirectionAsync() {
        var store = new WinUiFakeCultureStore();
        using var provider = new ServiceCollection()
            .AddSingleton<WinUiHostCultureStore>(store)
            .AddWinUiLocalization(options => options.SupportedCultures.Add("ar"))
            .BuildServiceProvider();

        var culture = provider.GetRequiredService<WinUiHostCultureService>();
        await culture.SetCultureAsync("ar");

        culture.IsRightToLeft.Should().BeTrue();
        culture.ToFlowDirection().Should().Be(Microsoft.UI.Xaml.FlowDirection.RightToLeft);
        store.Value.Should().Be("ar");
    }

    private sealed class MauiFakeCultureStore : MauiHostCultureStore {
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

    private sealed class WinUiFakeCultureStore : WinUiHostCultureStore {
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
}
