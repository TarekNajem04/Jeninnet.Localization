//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#pragma warning disable CA1001 // No disposal lifecycle exists by design: providers are process-lifetime singletons and never implement IDisposable; the watcher and timer below live for the process lifetime and are intentionally never disposed.
#pragma warning disable CA2213 // Same as CA1001: disposable watcher/timer fields are process-lifetime by design and are never disposed.
#pragma warning disable S1450 // The watcher field is a lifetime root by design: FileSystemWatcher stops raising events once garbage-collected, so it must stay referenced for the process lifetime even though it is never read back.
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Watches a single file and invokes a debounced reload callback on content changes.
/// Shared provider infrastructure for file hot-reload.
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>One shared engine, used by the JSON/XML/PO (Portable Object) file providers: a
/// <see cref="FileSystemWatcher"/> on the file's directory (filter = file name, so
/// temp-file + rename atomic writes are observed) plus a debounce
/// <see cref="Timer"/>. Rapid change bursts coalesce into one reload per quiet window.
/// </item>
/// <item>The callback contract is <c>Func&lt;ValueTask&lt;bool&gt;&gt;</c>: <see langword="true"/>
/// when the table was swapped, <see langword="false"/> when the reload failed and the
/// caller kept serving its last-good table. A failed reload re-arms the timer once
/// (torn writes from slow writers usually complete by then); a second consecutive
/// failure waits for the next filesystem event. Deleted files flow through the same
/// path (the reload fails, the last-good table keeps serving, and a later re-create
/// heals automatically).
/// </item>
/// <item>Best-effort watching: if the watcher cannot be established, <see cref="Start"/>
/// silently leaves watching off — lookups are never affected. Callback exceptions can
/// never escape onto a lookup path: the timer loop contains them as a last resort
/// (provider reloads additionally contain their own failures per the reliability
/// contract, the cancellation rule).
/// </item>
/// <item> boundary: distributed (multi-node) invalidation is a different,
/// complementary mechanism with no shared code; this engine is single-node file
/// watching only.
/// </item>
/// </list>
/// </remarks>
public sealed class DebouncedFileReloader {
    private readonly string _directory;
    private readonly string _filter;
    private readonly TimeSpan _debounce;
    private readonly Func<ValueTask<bool>> _tryReloadAsync;
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private bool _started;
    private bool _reattempt;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncedFileReloader"/> class.
    /// </summary>
    /// <param name="filePath">The file to watch (snapshotted as a full path).</param>
    /// <param name="debounce">The quiet window that must pass before a reload fires. Must be positive.</param>
    /// <param name="tryReloadAsync">The reload callback: <see langword="true"/> when swapped, <see langword="false"/> on failure. Must never throw.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> or <paramref name="tryReloadAsync"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="debounce"/> is not positive.</exception>
    public DebouncedFileReloader(string filePath, TimeSpan debounce, Func<ValueTask<bool>> tryReloadAsync) {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(tryReloadAsync);

        if(debounce <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(debounce), "The reload debounce must be positive.");
        }

        var fullPath = Path.GetFullPath(filePath);
        _directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The file path must include a directory.", nameof(filePath));
        _filter = Path.GetFileName(fullPath);
        _debounce = debounce;
        _tryReloadAsync = tryReloadAsync;
    }

    /// <summary>
    /// Begins watching. Idempotent: only the first call starts the watcher. Best-effort:
    /// when watching cannot be established, this method returns with watching off and
    /// lookups are unaffected.
    /// </summary>
    public void Start() {
        lock(_sync) {
            if(_started) {
                return;
            }

            _started = true;
            try {
                _timer = new Timer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _watcher = new FileSystemWatcher(_directory, _filter) {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                _watcher.Changed += OnTouched;
                _watcher.Created += OnTouched;
                _watcher.Renamed += OnTouched;
                _watcher.Deleted += OnTouched;
                _watcher.Error += OnError;
            }
            catch(IOException) {
                _started = false;
            }
            catch(UnauthorizedAccessException) {
                _started = false;
            }
        }
    }

    /// <summary>
    /// Handles the touched notification.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnTouched(object sender, FileSystemEventArgs e) {
        _ = sender;
        _ = e;
        lock(_sync) {
            _reattempt = true;
            _timer?.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Handles the error notification.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnError(object sender, ErrorEventArgs e) {
        _ = sender;
        _ = e;
        lock(_sync) {
            _reattempt = true;
            _timer?.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Handles the timer notification.
    /// </summary>
    /// <param name="state">The state.</param>
    private void OnTimer(object? state) {
        _ = state;
        lock(_sync) {
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        _ = ReloadAsync();
    }

    /// <summary>
    /// Performs the reload operation.
    /// </summary>
    private async Task ReloadAsync() {
        bool swapped;
        try {
            swapped = await _tryReloadAsync().ConfigureAwait(false);
        }
        catch(Exception) {
            return;
        }

        if(swapped) {
            return;
        }

        lock(_sync) {
            if(_reattempt) {
                _reattempt = false;
                _timer?.Change(_debounce, Timeout.InfiniteTimeSpan);
            }
        }
    }
}
