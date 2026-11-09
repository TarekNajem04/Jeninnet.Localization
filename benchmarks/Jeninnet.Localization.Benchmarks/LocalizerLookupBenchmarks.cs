//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jeninnet.Localization.Abstractions.Options;
using Jeninnet.Localization.Core;
using Jeninnet.Localization.Providers.Memory;
using Jeninnet.Localization.Providers.Memory.Options;

namespace Jeninnet.Localization.Benchmarks;

/// <summary>
/// Localizer hot paths: exact-culture hit, missing key, and plural-pair selection
/// over an in-memory provider. Short-run job keeps local iterations fast.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class LocalizerLookupBenchmarks {
    private DefaultMessageLocalizer _localizer = null!;
    private IReadOnlyDictionary<string, object?> _parameters = null!;
    private CultureInfo _culture = null!;

    /// <summary>
    /// Builds a 100-key localizer once per process.
    /// </summary>
    [GlobalSetup]
    public void Setup() {
        var messages = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["Cart.Item.One"] = "{count} item",
            ["Cart.Item.Other"] = "{count} items",
        };
        for(var i = 0; i < 98; i++) {
            messages[$"Key.{i:000}"] = $"Value {i} for {{name}}";
        }

        var provider = new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = messages });
        _localizer = new DefaultMessageLocalizer(
            provider,
            new DefaultMessageFormatter(),
            new LocalizationOptions { DefaultCulture = "en" });
        _parameters = new Dictionary<string, object?> { ["count"] = 5, ["name"] = "Ada" };
        _culture = CultureInfo.GetCultureInfo("en");
    }

    /// <summary>Exact-culture template hit with parameters.</summary>
    [Benchmark]
    public async ValueTask<string?> HitAsync() =>
        await _localizer.LocalizeAsync("Key.050", _parameters, _culture);

    /// <summary>Missing key (sink path).</summary>
    [Benchmark]
    public async ValueTask<string?> MissAsync() =>
        await _localizer.LocalizeAsync("Key.Nope", _parameters, _culture);

    /// <summary>Plural-pair selection plus formatting.</summary>
    [Benchmark]
    public async ValueTask<string?> PluralAsync() =>
        await _localizer.LocalizePluralAsync("Cart.Item.One", "Cart.Item.Other", 5, _parameters, _culture);
}
