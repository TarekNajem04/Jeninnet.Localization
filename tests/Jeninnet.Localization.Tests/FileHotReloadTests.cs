//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
///  hot-reload tests: opt-in file watching with debounced reload for the JSON, XML,
/// and PO (Portable Object) providers (PO text and MO (Machine Object) binary carriers,). Real filesystem events
/// throughout: positive cases poll with timeouts (no fixed sleeps), negative cases use a
/// single bounded wait.
/// </summary>
[TestClass]
public class FileHotReloadTests {
    private static readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Performs the poll for value operation.
    /// </summary>
    /// <param name="lookup">The lookup.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="timeout">The timeout.</param>
    private async Task PollForValueAsync(Func<ValueTask<string?>> lookup, string? expected, TimeSpan timeout) {
        var token = TestContext?.CancellationToken ?? CancellationToken.None;
        var deadline = DateTime.UtcNow + timeout;
        while(true) {
            if(string.Equals(await lookup(), expected, StringComparison.Ordinal)) {
                return;
            }

            if(DateTime.UtcNow >= deadline) {
                break;
            }

            await Task.Delay(50, token);
        }

        (await lookup()).Should().Be(expected, "the watched edit did not propagate within the timeout");
    }

    /// <summary>
    /// Performs the poll for value operation.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="key">The message key.</param>
    /// <param name="expected">The expected value.</param>
    private Task PollForValueAsync(IMessageProvider provider, string key, string? expected) =>
        PollForValueAsync(() => provider.GetMessageTemplateAsync(key, CultureInfo.InvariantCulture, CancellationToken.None), expected, _timeout);

    /// <summary>
    /// Performs the watched json operation.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The watched json result.</returns>
    private static JsonMessageProvider WatchedJson(string filePath) =>
        new(new JsonLocalizationOptions { FilePath = filePath, WatchForChanges = true, ReloadDebounce = _debounce });

    /// <summary>
    /// Performs the watched xml operation.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The watched xml result.</returns>
    private static XmlMessageProvider WatchedXml(string filePath) =>
        new(new XmlLocalizationOptions { FilePath = filePath, WatchForChanges = true, ReloadDebounce = _debounce });

    /// <summary>
    /// Performs the watched po operation.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The watched po result.</returns>
    private static PoMessageProvider WatchedPo(string filePath) =>
        new(new PoLocalizationOptions { FilePath = filePath, WatchForChanges = true, ReloadDebounce = _debounce });

    /// <summary>
    /// Verifies that json edited file propagates within debounce.
    /// </summary>
    [TestMethod]
    public async Task Json_EditedFile_PropagatesWithinDebounceAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedJson(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            await File.WriteAllTextAsync(tempFile, """{"key":"v2"}""", TestContext?.CancellationToken ?? CancellationToken.None);

            await PollForValueAsync(provider, "key", "v2");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml edited file propagates within debounce.
    /// </summary>
    [TestMethod]
    public async Task Xml_EditedFile_PropagatesWithinDebounceAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><key>v1</key></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedXml(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            await File.WriteAllTextAsync(tempFile, "<messages><key>v2</key></messages>", TestContext?.CancellationToken ?? CancellationToken.None);

            await PollForValueAsync(provider, "key", "v2");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po edited file propagates within debounce.
    /// </summary>
    [TestMethod]
    public async Task Po_EditedFile_PropagatesWithinDebounceAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "msgid \"key\"\nmsgstr \"v1\"\n", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedPo(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            await File.WriteAllTextAsync(tempFile, "msgid \"key\"\nmsgstr \"v2\"\n", TestContext?.CancellationToken ?? CancellationToken.None);

            await PollForValueAsync(provider, "key", "v2");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that po plural table reloads.
    /// </summary>
    [TestMethod]
    public async Task Po_PluralTable_ReloadsAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string first = """
                msgid ""
                msgstr ""
                "Plural-Forms: nplurals=2; plural=(n != 1);\n"

                msgid "apple"
                msgid_plural "apples"
                msgstr[0] "pomme"
                msgstr[1] "pommes"
                """;
            await File.WriteAllTextAsync(tempFile, first, TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedPo(tempFile);
            var culture = new CultureInfo("fr");
            (await provider.GetPluralTemplateAsync("apple", "apples", 5, culture, CancellationToken.None)).Should().Be("pommes");

            await File.WriteAllTextAsync(tempFile, first.Replace("pommes", "pommes-vertes", StringComparison.Ordinal), TestContext?.CancellationToken ?? CancellationToken.None);

            var deadline = DateTime.UtcNow + _timeout;
            while(!string.Equals(await provider.GetPluralTemplateAsync("apple", "apples", 5, culture, CancellationToken.None), "pommes-vertes", StringComparison.Ordinal)) {
                if(DateTime.UtcNow >= deadline) {
                    break;
                }

                await Task.Delay(50, TestContext?.CancellationToken ?? CancellationToken.None);
            }

            (await provider.GetPluralTemplateAsync("apple", "apples", 5, culture, CancellationToken.None)).Should().Be("pommes-vertes");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that mo edited file propagates within debounce.
    /// </summary>
    [TestMethod]
    public async Task Mo_EditedFile_PropagatesWithinDebounceAsync() {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mo");
        try {
            await File.WriteAllBytesAsync(tempFile, MoFileWriter.Write([("key", "v1")]), TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new PoMessageProvider(new PoLocalizationOptions { FilePath = tempFile, WatchForChanges = true, ReloadDebounce = _debounce });
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            await File.WriteAllBytesAsync(tempFile, MoFileWriter.Write([("key", "v2")]), TestContext?.CancellationToken ?? CancellationToken.None);

            await PollForValueAsync(provider, "key", "v2");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json torn write never serves partial table.
    /// </summary>
    [TestMethod]
    public async Task Json_TornWrite_NeverServesPartialTableAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedJson(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            var observed = new HashSet<string>();
            var token = TestContext?.CancellationToken ?? CancellationToken.None;
            var sampling = Task.Run(async () => {
                var stop = DateTime.UtcNow + TimeSpan.FromSeconds(3);
                while(DateTime.UtcNow < stop) {
                    observed.Add(await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None) ?? "<missing>");
                    await Task.Delay(25, token);
                }
            }, token);

            await File.WriteAllTextAsync(tempFile, """{"key":""", TestContext?.CancellationToken ?? CancellationToken.None);
            await Task.Delay(100, token);
            await File.WriteAllTextAsync(tempFile, """{"key":"half""", TestContext?.CancellationToken ?? CancellationToken.None);
            await Task.Delay(100, token);
            await File.WriteAllTextAsync(tempFile, """{"key":"final"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            await sampling;

            observed.Should().OnlyContain(static value => value is "v1" or "final",
                "half-written files must never be served: failed reloads keep the last-good table");
            await PollForValueAsync(provider, "key", "final");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml atomic rename serves complete table.
    /// </summary>
    [TestMethod]
    public async Task Xml_AtomicRename_ServesCompleteTableAsync() {
        var tempFile = Path.GetTempFileName();
        var staging = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><key>v1</key></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedXml(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            await File.WriteAllTextAsync(staging, "<messages><key>v2</key><added>yes</added></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            File.Move(staging, tempFile, true);

            await PollForValueAsync(provider, "key", "v2");
            await PollForValueAsync(provider, "added", "yes");
        }
        finally {
            File.Delete(tempFile);
            File.Delete(staging);
        }
    }

    /// <summary>
    /// Verifies that json deleted file keeps serving last good and signals.
    /// </summary>
    [TestMethod]
    public async Task Json_DeletedFile_KeepsServingLastGoodAndSignalsAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = WatchedJson(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            var failed = new TaskCompletionSource<ProviderAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.AcquisitionFailed += (_, e) => failed.TrySetResult(e);

            File.Delete(tempFile);

            for(var i = 0; i < 10; i++) {
                (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1",
                    "lookups never throw for watcher failures: the last-good table keeps serving");
                await Task.Delay(100, TestContext?.CancellationToken ?? CancellationToken.None);
            }

            var signal = await failed.Task.WaitAsync(_timeout, TestContext?.CancellationToken ?? CancellationToken.None);
            signal.Exception.Should().BeOfType<FileNotFoundException>();

            await File.WriteAllTextAsync(tempFile, """{"key":"v2"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            await PollForValueAsync(provider, "key", "v2");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json watching disabled preserves static behavior.
    /// </summary>
    [TestMethod]
    public async Task Json_WatchingDisabled_PreservesStaticBehaviorAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new JsonMessageProvider(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            await File.WriteAllTextAsync(tempFile, """{"key":"v2"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1",
                "watching is opt-in only: default behavior is identical to the static snapshot");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json refresh async reloads without watcher.
    /// </summary>
    [TestMethod]
    public async Task Json_RefreshAsync_ReloadsWithoutWatcherAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new JsonMessageProvider(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            var acquired = new TaskCompletionSource<ProviderAcquisitionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.TranslationAcquired += (_, e) => acquired.TrySetResult(e);

            await File.WriteAllTextAsync(tempFile, """{"key":"v2"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            await provider.RefreshAsync(TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v2");
            var signal = await acquired.Task.WaitAsync(_timeout, TestContext?.CancellationToken ?? CancellationToken.None);
            signal.Templates["key"].Should().Be("v2");
            signal.Culture.Should().Be(CultureInfo.InvariantCulture);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json refresh async failure throws and keeps last good.
    /// </summary>
    [TestMethod]
    public async Task Json_RefreshAsync_Failure_ThrowsAndKeepsLastGoodAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new JsonMessageProvider(tempFile);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            var failed = new TaskCompletionSource<ProviderAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.AcquisitionFailed += (_, e) => failed.TrySetResult(e);

            File.Delete(tempFile);

            Func<Task> act = async () => await provider.RefreshAsync(CancellationToken.None);

            await act.Should().ThrowAsync<FileNotFoundException>("an explicit refresh surfaces its failure");
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1",
                "a failed refresh keeps serving the last-good table");
            var signal = await failed.Task.WaitAsync(_timeout, TestContext?.CancellationToken ?? CancellationToken.None);
            signal.Exception.Should().BeOfType<FileNotFoundException>();
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json refresh async pre cancelled token throws before work.
    /// </summary>
    [TestMethod]
    public async Task Json_RefreshAsync_PreCancelledToken_ThrowsBeforeWorkAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var provider = new JsonMessageProvider(tempFile);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            Func<Task> act = async () => await provider.RefreshAsync(cancellation.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            provider.Resource.Should().BeNull("a pre-cancelled refresh performs no load");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that watch options invalid debounce throws.
    /// </summary>
    [TestMethod]
    public void WatchOptions_InvalidDebounce_Throws() {
        Func<JsonMessageProvider> json = () => new(new JsonLocalizationOptions { FilePath = "x.json", WatchForChanges = true, ReloadDebounce = TimeSpan.Zero });
        Func<XmlMessageProvider> xml = () => new(new XmlLocalizationOptions { FilePath = "x.xml", WatchForChanges = true, ReloadDebounce = TimeSpan.FromMilliseconds(-1) });
        Func<PoMessageProvider> po = () => new(new PoLocalizationOptions { FilePath = "x.po", WatchForChanges = true, ReloadDebounce = TimeSpan.Zero });

        // File-existence validation runs first; use real temp files to reach the debounce check.
        var jsonFile = Path.GetTempFileName();
        var xmlFile = Path.GetTempFileName();
        var poFile = Path.GetTempFileName();
        try {
            Func<JsonMessageProvider> jsonReal = () => new(new JsonLocalizationOptions { FilePath = jsonFile, WatchForChanges = true, ReloadDebounce = TimeSpan.Zero });
            Func<XmlMessageProvider> xmlReal = () => new(new XmlLocalizationOptions { FilePath = xmlFile, WatchForChanges = true, ReloadDebounce = TimeSpan.FromMilliseconds(-1) });
            Func<PoMessageProvider> poReal = () => new(new PoLocalizationOptions { FilePath = poFile, WatchForChanges = true, ReloadDebounce = TimeSpan.Zero });

            jsonReal.Should().Throw<ArgumentOutOfRangeException>();
            xmlReal.Should().Throw<ArgumentOutOfRangeException>();
            poReal.Should().Throw<ArgumentOutOfRangeException>();
            json.Should().Throw<FileNotFoundException>();
            xml.Should().Throw<FileNotFoundException>();
            po.Should().Throw<FileNotFoundException>();
        }
        finally {
            File.Delete(jsonFile);
            File.Delete(xmlFile);
            File.Delete(poFile);
        }
    }

    /// <summary>
    /// Verifies that watch options mutation after construction is ignored.
    /// </summary>
    [TestMethod]
    public async Task WatchOptions_MutationAfterConstruction_IsIgnoredAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var options = new JsonLocalizationOptions { FilePath = tempFile };
            var provider = new JsonMessageProvider(options);
            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");

            options.WatchForChanges = true;
            options.ReloadDebounce = TimeSpan.FromMilliseconds(50);

            await File.WriteAllTextAsync(tempFile, """{"key":"v2"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext?.CancellationToken ?? CancellationToken.None);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1",
                "watch settings are snapshotted at construction");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json watcher success raises acquired with table.
    /// </summary>
    [TestMethod]
    public async Task Json_WatcherSuccess_RaisesAcquiredWithTableAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"key":"v1"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var acquired = new List<ProviderAcquisitionEventArgs>();
            var provider = WatchedJson(tempFile);
            provider.TranslationAcquired += (_, e) => acquired.Add(e);

            (await provider.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("v1");
            acquired.Should().HaveCount(1, "the initial load is an acquisition");
            acquired[0].Templates["key"].Should().Be("v1");

            await File.WriteAllTextAsync(tempFile, """{"key":"v2"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            await PollForValueAsync(provider, "key", "v2");

            acquired.Should().HaveCount(2, "the reload is a second acquisition");
            acquired[1].Templates["key"].Should().Be("v2");
            acquired[1].Culture.Should().Be(CultureInfo.InvariantCulture);
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
