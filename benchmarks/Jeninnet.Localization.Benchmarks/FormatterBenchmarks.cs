//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jeninnet.Localization.Core;

namespace Jeninnet.Localization.Benchmarks;

/// <summary>
/// Formatter hot paths: named/positional substitution and ICU plural/select.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FormatterBenchmarks {
    private readonly DefaultMessageFormatter _defaultFormatter = new();
    private readonly IcuMessageFormatter _icuFormatter = new();
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");
    private IReadOnlyDictionary<string, object?> _parameters = null!;

    /// <summary>
    /// Builds the shared parameter table once per process.
    /// </summary>
    [GlobalSetup]
    public void Setup() =>
        _parameters = new Dictionary<string, object?> {
            ["name"] = "Ada",
            ["count"] = 5,
            ["total"] = 42.5m,
        };

    /// <summary>Named-placeholder substitution.</summary>
    [Benchmark]
    public string Named() =>
        _defaultFormatter.Format("Hello {name}, you have {count} items.", _parameters);

    /// <summary>Positional-placeholder substitution.</summary>
    [Benchmark]
    public string Positional() =>
        _defaultFormatter.Format("Hello {0}, you have {1} items.", _parameters);

    /// <summary>ICU plural selection with an offset-free table.</summary>
    [Benchmark]
    public string IcuPlural() =>
        _icuFormatter.Format("{count, plural, one {# item} other {# items}}", _parameters, _culture);

    /// <summary>ICU select on a string discriminator.</summary>
    [Benchmark]
    public string IcuSelect() =>
        _icuFormatter.Format("{name, select, Ada {hello} other {hi}}", _parameters, _culture);
}
