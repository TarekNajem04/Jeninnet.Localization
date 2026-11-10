//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jeninnet.Localization.Providers.Ini;
using Jeninnet.Localization.Providers.Toml;

namespace Jeninnet.Localization.Benchmarks;

/// <summary>
/// File-provider hot paths: INI and TOML steady-state lookups (hit and miss) over a
/// 100-key catalog, mirroring the JSON lookup shape. The files are written once per
/// process and warmed before measurement, so the benchmarks time dictionary lookups,
/// never the filesystem. Short-run job keeps local iterations fast.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FileProviderBenchmarks : IDisposable {
    private const int KeyCount = 100;

    private string _scratchRoot = null!;
    private IniMessageProvider _ini = null!;
    private TomlMessageProvider _toml = null!;
    private CultureInfo _culture = null!;
    private bool _disposed;

    /// <summary>
    /// Writes one 100-key INI and TOML fixture per process and warms both providers.
    /// </summary>
    [GlobalSetup]
    public void Setup() {
        _scratchRoot = Path.Combine(Path.GetTempPath(), "jlnbench-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_scratchRoot);

        var iniLines = new List<string>(KeyCount);
        var tomlLines = new List<string>(KeyCount);
        for(var i = 0; i < KeyCount; i++) {
            iniLines.Add($"Key.{i:000} = Value {i}");
            tomlLines.Add($"Key{i:000} = \"Value {i}\"");
        }

        var iniPath = Path.Combine(_scratchRoot, "messages.ini");
        var tomlPath = Path.Combine(_scratchRoot, "messages.toml");
        File.WriteAllText(iniPath, string.Join("\n", iniLines));
        File.WriteAllText(tomlPath, string.Join("\n", tomlLines));

        _ini = new IniMessageProvider(iniPath);
        _toml = new TomlMessageProvider(tomlPath);
        _culture = CultureInfo.GetCultureInfo("en");

        _ini.GetMessageTemplateAsync("Key.050", _culture).AsTask().GetAwaiter().GetResult();
        _toml.GetMessageTemplateAsync("Key050", _culture).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Disposes the providers and deletes the per-process fixture directory.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <inheritdoc/>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources (providers and the fixture directory).
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing) {
        if(_disposed) {
            return;
        }

        _disposed = true;
        if(disposing) {
            _ini.Dispose();
            _toml.Dispose();
            Directory.Delete(_scratchRoot, recursive: true);
        }
    }

    /// <summary>INI exact hit, steady state.</summary>
    [Benchmark]
    public async ValueTask<string?> IniHitAsync() =>
        await _ini.GetMessageTemplateAsync("Key.050", _culture);

    /// <summary>INI miss, steady state.</summary>
    [Benchmark]
    public async ValueTask<string?> IniMissAsync() =>
        await _ini.GetMessageTemplateAsync("Key.Nope", _culture);

    /// <summary>TOML exact hit, steady state.</summary>
    [Benchmark]
    public async ValueTask<string?> TomlHitAsync() =>
        await _toml.GetMessageTemplateAsync("Key050", _culture);

    /// <summary>TOML miss, steady state.</summary>
    [Benchmark]
    public async ValueTask<string?> TomlMissAsync() =>
        await _toml.GetMessageTemplateAsync("KeyNope", _culture);
}
