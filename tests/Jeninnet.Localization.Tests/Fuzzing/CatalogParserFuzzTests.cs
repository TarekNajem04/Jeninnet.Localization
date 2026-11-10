//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests.Fuzzing;

/// <summary>
/// Property-style tests for every catalog parser: random valid documents round-trip,
/// random mutations throw only the documented exception type, and failed loads never
/// publish partial tables. Deterministic: fixed seeds (see the constants below), no
/// wall-clock, no new dependencies. Seed policy: one non-zero xorshift64* seed per
/// area; changing a seed requires reviewing every golden expectation it feeds.
/// </summary>
[TestClass]
public sealed class CatalogParserFuzzTests {
    private const ulong SEED_INI = 0x1A2B3C4D5E6F7081ul;
    private const ulong SEED_TOML = 0x2B3C4D5E6F708192ul;
    private const ulong SEED_JSON = 0x3C4D5E6F708192A3ul;
    private const ulong SEED_PO = 0x4D5E6F708192A3B4ul;
    private const ulong SEED_MO = 0x5E6F708192A3B4C5ul;
    private const ulong SEED_RESX = 0x6F708192A3B4C5D6ul;
    private const ulong SEED_XLIFF = 0x708192A3B4C5D6E7ul;
    private const ulong SEED_PO_INTERCHANGE = 0x8192A3B4C5D6E7F8ul;

    private const int ROUND_TRIP_DOCS = 100;
    private const int MUTATION_CASES = 300;
    private const int PARTIAL_TABLE_FILES = 20;
    private const int MAX_ENTRIES = 12;

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that ini valid documents round trip.
    /// </summary>
    [TestMethod]
    public void Ini_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_INI);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextIniValue(rng));
            var text = string.Join("\n", expected.Select(static kv => kv.Key + " = " + kv.Value));
            var parsed = IniParser.Parse(text, "fuzz.ini");
            parsed.Should().HaveCount(expected.Count);
            for(var index = 0; index < expected.Count; index++) {
                parsed[index].Should().Be(expected[index]);
            }
        }
    }

    /// <summary>
    /// Verifies that ini mutated documents only invalid data exception.
    /// </summary>
    [TestMethod]
    public void Ini_MutatedDocuments_OnlyInvalidDataException() {
        var rng = new FuzzRng(SEED_INI);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextIniValue(rng));
            var valid = string.Join("\n", expected.Select(static kv => kv.Key + " = " + kv.Value));
            var mutated = rng.MutateText(valid);
            try {
                var parsed = IniParser.Parse(mutated, "fuzz.ini");
                parsed.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as InvalidDataException;
                parsed.Should().NotBeNull($"INI mutations must throw only InvalidDataException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.Message.Should().Contain("fuzz.ini");
            }
        }
    }

    /// <summary>
    /// Verifies that ini mutated files never publish partial tables.
    /// </summary>
    [TestMethod]
    public async Task Ini_MutatedFiles_NeverPublishPartialTablesAsync() {
        var rng = new FuzzRng(SEED_INI);
        for(var iteration = 0; iteration < PARTIAL_TABLE_FILES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextIniValue(rng));
            var valid = string.Join("\n", expected.Select(static kv => kv.Key + " = " + kv.Value));
            var mutated = rng.MutateText(valid);
            var tempFile = Path.GetTempFileName();
            try {
                await File.WriteAllTextAsync(tempFile, mutated, TestContext?.CancellationToken ?? CancellationToken.None);
                var provider = new IniMessageProvider(tempFile);
                try {
                    await provider.GetMessageTemplateAsync("anything", CultureInfo.InvariantCulture, CancellationToken.None);
                }
                catch(InvalidDataException) {
                    provider.Resource.Should().BeNull("a failed load publishes nothing");
                }
            }
            finally {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Verifies that toml valid documents round trip.
    /// </summary>
    [TestMethod]
    public void Toml_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_TOML);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextBareKey(rng), () => NextSafeValue(rng));
            var text = string.Join("\n", expected.Select(static kv => kv.Key + " = \"" + kv.Value + "\""));
            var parsed = TomlParser.Parse(text, "fuzz.toml");
            parsed.Should().HaveCount(expected.Count);
            for(var index = 0; index < expected.Count; index++) {
                parsed[index].Should().Be(expected[index]);
            }
        }
    }

    /// <summary>
    /// Verifies that toml mutated documents only invalid data exception.
    /// </summary>
    [TestMethod]
    public void Toml_MutatedDocuments_OnlyInvalidDataException() {
        var rng = new FuzzRng(SEED_TOML);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextBareKey(rng), () => NextSafeValue(rng));
            var valid = string.Join("\n", expected.Select(static kv => kv.Key + " = \"" + kv.Value + "\""));
            var mutated = rng.MutateText(valid);
            try {
                var parsed = TomlParser.Parse(mutated, "fuzz.toml");
                parsed.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as InvalidDataException;
                parsed.Should().NotBeNull($"TOML mutations must throw only InvalidDataException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.Message.Should().Contain("fuzz.toml");
            }
        }
    }

    /// <summary>
    /// Verifies that toml mutated files never publish partial tables.
    /// </summary>
    [TestMethod]
    public async Task Toml_MutatedFiles_NeverPublishPartialTablesAsync() {
        var rng = new FuzzRng(SEED_TOML);
        for(var iteration = 0; iteration < PARTIAL_TABLE_FILES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextBareKey(rng), () => NextSafeValue(rng));
            var valid = string.Join("\n", expected.Select(static kv => kv.Key + " = \"" + kv.Value + "\""));
            var mutated = rng.MutateText(valid);
            var tempFile = Path.GetTempFileName();
            try {
                await File.WriteAllTextAsync(tempFile, mutated, TestContext?.CancellationToken ?? CancellationToken.None);
                var provider = new TomlMessageProvider(tempFile);
                try {
                    await provider.GetMessageTemplateAsync("anything", CultureInfo.InvariantCulture, CancellationToken.None);
                }
                catch(InvalidDataException) {
                    provider.Resource.Should().BeNull("a failed load publishes nothing");
                }
            }
            finally {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Verifies that loc json valid documents round trip.
    /// </summary>
    [TestMethod]
    public void LocJson_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_JSON);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => rng.NextTemplate());
            var text = JsonSerializer.Serialize(expected.ToDictionary(static kv => kv.Key, static kv => kv.Value));
            var result = LocJsonParser.Parse(text);
            result.SyntaxError.Should().BeNull();
            result.Entries.Should().HaveCount(expected.Count);
            for(var index = 0; index < expected.Count; index++) {
                result.Entries[index].Key.Should().Be(expected[index].Key);
                result.Entries[index].Value.Should().Be(expected[index].Value);
            }
        }
    }

    /// <summary>
    /// Verifies that loc json mutated documents never throw unexpected.
    /// </summary>
    [TestMethod]
    public void LocJson_MutatedDocuments_NeverThrowUnexpected() {
        var rng = new FuzzRng(SEED_JSON);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => rng.NextTemplate());
            var valid = JsonSerializer.Serialize(expected.ToDictionary(static kv => kv.Key, static kv => kv.Value));
            var mutated = rng.MutateText(valid);
            var result = LocJsonParser.Parse(mutated);
            result.Should().NotBeNull();
            if(result.SyntaxError is not null) {
                (result.SyntaxError.Offset >= 0 && result.SyntaxError.Offset <= mutated.Length)
                    .Should().BeTrue("the syntax error offset must point inside the document");
                LocJsonParser.GetLineAndColumn(mutated, result.SyntaxError.Offset, out var line, out var column);
                (line >= 1 && column >= 1).Should().BeTrue("line/column diagnostics are 1-based");
                LocJsonParser.GetLineAndColumn(mutated, result.SyntaxError.Offset, out var line2, out var column2);
                line2.Should().Be(line, "line mapping is deterministic");
                column2.Should().Be(column, "column mapping is deterministic");
            }
        }
    }

    /// <summary>
    /// Verifies that po valid documents round trip.
    /// </summary>
    [TestMethod]
    public void Po_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_PO);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextPoValue(rng));
            var text = string.Join("\n\n", expected.Select(static kv => "msgid \"" + EscapePo(kv.Key) + "\"\nmsgstr \"" + EscapePo(kv.Value) + "\"")) + "\n";
            var catalog = PoParser.Parse(text, "fuzz.po");
            catalog.Messages.Should().HaveCount(expected.Count);
            foreach(var kv in expected) {
                catalog.Messages[kv.Key].Should().Be(kv.Value);
            }
        }
    }

    /// <summary>
    /// Verifies that po mutated documents only po parse exception.
    /// </summary>
    [TestMethod]
    public void Po_MutatedDocuments_OnlyPoParseException() {
        var rng = new FuzzRng(SEED_PO);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextPoValue(rng));
            var valid = string.Join("\n\n", expected.Select(static kv => "msgid \"" + EscapePo(kv.Key) + "\"\nmsgstr \"" + EscapePo(kv.Value) + "\"")) + "\n";
            var mutated = rng.MutateText(valid);
            try {
                var catalog = PoParser.Parse(mutated, "fuzz.po");
                catalog.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as PoParseException;
                parsed.Should().NotBeNull($"PO mutations must throw only PoParseException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.FilePath.Should().Be("fuzz.po");
                (parsed.LineNumber >= 0).Should().BeTrue("binary/text diagnostics carry line 0 or a 1-based line");
                parsed.Message.Should().Contain("fuzz.po");
            }
        }
    }

    /// <summary>
    /// Verifies that po mutated files never publish partial tables.
    /// </summary>
    [TestMethod]
    public async Task Po_MutatedFiles_NeverPublishPartialTablesAsync() {
        var rng = new FuzzRng(SEED_PO);
        for(var iteration = 0; iteration < PARTIAL_TABLE_FILES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextPoValue(rng));
            var valid = string.Join("\n\n", expected.Select(static kv => "msgid \"" + EscapePo(kv.Key) + "\"\nmsgstr \"" + EscapePo(kv.Value) + "\"")) + "\n";
            var mutated = rng.MutateText(valid);
            var tempFile = Path.GetTempFileName();
            try {
                await File.WriteAllTextAsync(tempFile, mutated, TestContext?.CancellationToken ?? CancellationToken.None);
                var provider = new PoMessageProvider(tempFile);
                try {
                    await provider.GetMessageTemplateAsync("anything", CultureInfo.InvariantCulture, CancellationToken.None);
                }
                catch(PoParseException) {
                    provider.Catalog.Should().BeNull("a failed load publishes nothing");
                    provider.Resource.Should().BeNull("a failed load publishes nothing");
                }
            }
            finally {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Verifies that mo valid catalogs round trip.
    /// </summary>
    [TestMethod]
    public void Mo_ValidCatalogs_RoundTrip() {
        var rng = new FuzzRng(SEED_MO);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextSafeValue(rng));
            var entries = expected.Select(static kv => (kv.Key, kv.Value)).ToArray();
            var bytes = MoFileWriter.Write(entries, bigEndian: rng.NextBool());
            var catalog = MoParser.Parse(bytes, "fuzz.mo");
            catalog.Messages.Should().HaveCount(expected.Count);
            foreach(var kv in expected) {
                catalog.Messages[kv.Key].Should().Be(kv.Value);
            }
        }
    }

    /// <summary>
    /// Verifies that mo mutated binaries only po parse exception.
    /// </summary>
    [TestMethod]
    public void Mo_MutatedBinaries_OnlyPoParseException() {
        var rng = new FuzzRng(SEED_MO);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextSafeValue(rng));
            var valid = MoFileWriter.Write(expected.Select(static kv => (kv.Key, kv.Value)).ToArray(), bigEndian: rng.NextBool());
            var mutated = rng.MutateBytes(valid);
            try {
                var catalog = MoParser.Parse(mutated, "fuzz.mo");
                catalog.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as PoParseException;
                parsed.Should().NotBeNull($"MO mutations must throw only PoParseException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.FilePath.Should().Be("fuzz.mo");
                parsed.LineNumber.Should().Be(0, "binary diagnostics carry no line numbers");
            }
        }
    }

    /// <summary>
    /// Verifies that res x valid documents round trip.
    /// </summary>
    [TestMethod]
    public void ResX_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_RESX);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextXmlValue(rng))
                .ToDictionary(static kv => kv.Key, static kv => (string?)kv.Value);
            var text = ResXCatalogInterchange.ExportCatalog(expected);
            var document = ResXCatalogInterchange.ParseDocument(text, "fuzz.resx");
            document.Entries.Should().HaveCount(expected.Count);
            foreach(var entry in document.Entries) {
                expected[entry.Name].Should().Be(entry.Value);
            }
        }
    }

    /// <summary>
    /// Verifies that res x mutated documents only res x interchange exception.
    /// </summary>
    [TestMethod]
    public void ResX_MutatedDocuments_OnlyResXInterchangeException() {
        var rng = new FuzzRng(SEED_RESX);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextXmlValue(rng))
                .ToDictionary(static kv => kv.Key, static kv => (string?)kv.Value);
            var valid = ResXCatalogInterchange.ExportCatalog(expected);
            var mutated = rng.MutateText(valid);
            try {
                var document = ResXCatalogInterchange.ParseDocument(mutated, "fuzz.resx");
                document.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as ResXInterchangeException;
                parsed.Should().NotBeNull($"ResX mutations must throw only ResXInterchangeException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.FilePath.Should().Be("fuzz.resx");
            }
        }
    }

    /// <summary>
    /// Verifies that xliff valid documents round trip.
    /// </summary>
    [TestMethod]
    public void Xliff_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_XLIFF);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextXmlValue(rng))
                .ToDictionary(static kv => kv.Key, static kv => (string?)kv.Value);
            var target = expected.ToDictionary(static kv => kv.Key, static kv => kv.Value);
            var text = XliffCatalogInterchange.ExportCatalog(expected, target, "en", "ar", "fuzz");
            var document = XliffCatalogInterchange.ParseDocument(text, "fuzz.xliff");
            document.Units.Should().HaveCount(expected.Count);
            foreach(var unit in document.Units) {
                expected[unit.Id].Should().Be(unit.Source);
                unit.Target.Should().Be(expected[unit.Id]);
            }
        }
    }

    /// <summary>
    /// Verifies that xliff mutated documents only xliff interchange exception.
    /// </summary>
    [TestMethod]
    public void Xliff_MutatedDocuments_OnlyXliffInterchangeException() {
        var rng = new FuzzRng(SEED_XLIFF);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextXmlValue(rng))
                .ToDictionary(static kv => kv.Key, static kv => (string?)kv.Value);
            var target = expected.ToDictionary(static kv => kv.Key, static kv => kv.Value);
            var valid = XliffCatalogInterchange.ExportCatalog(expected, target, "en", "ar", "fuzz");
            var mutated = rng.MutateText(valid);
            try {
                var document = XliffCatalogInterchange.ParseDocument(mutated, "fuzz.xliff");
                document.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as XliffInterchangeException;
                parsed.Should().NotBeNull($"XLIFF mutations must throw only XliffInterchangeException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.FilePath.Should().Be("fuzz.xliff");
            }
        }
    }

    /// <summary>
    /// Verifies that po interchange valid documents round trip.
    /// </summary>
    [TestMethod]
    public void PoInterchange_ValidDocuments_RoundTrip() {
        var rng = new FuzzRng(SEED_PO_INTERCHANGE);
        for(var doc = 0; doc < ROUND_TRIP_DOCS; doc++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextPoValue(rng))
                .ToDictionary(static kv => kv.Key, static kv => (string?)kv.Value);
            var target = expected.ToDictionary(static kv => kv.Key, static kv => kv.Value);
            var text = PoCatalogInterchange.ExportCatalog(target, "ar");
            var document = PoCatalogInterchange.ParseDocument(text, "fuzz.po");
            document.Entries.Should().HaveCount(expected.Count);
            foreach(var entry in document.Entries) {
                expected[entry.Key].Should().Be(entry.Value);
            }
        }
    }

    /// <summary>
    /// Verifies that po interchange mutated documents only po interchange exception.
    /// </summary>
    [TestMethod]
    public void PoInterchange_MutatedDocuments_OnlyPoInterchangeException() {
        var rng = new FuzzRng(SEED_PO_INTERCHANGE);
        for(var iteration = 0; iteration < MUTATION_CASES; iteration++) {
            var expected = DistinctEntries(NextEntryCount(rng), () => NextSafeKey(rng), () => NextPoValue(rng))
                .ToDictionary(static kv => kv.Key, static kv => (string?)kv.Value);
            var target = expected.ToDictionary(static kv => kv.Key, static kv => kv.Value);
            var valid = PoCatalogInterchange.ExportCatalog(target, "ar");
            var mutated = rng.MutateText(valid);
            try {
                var document = PoCatalogInterchange.ParseDocument(mutated, "fuzz.po");
                document.Should().NotBeNull();
            }
            catch(Exception exception) {
                var parsed = exception as PoInterchangeException;
                parsed.Should().NotBeNull($"PO interchange mutations must throw only PoInterchangeException, not {exception.GetType().Name}: {exception.Message}");
                parsed!.FilePath.Should().Be("fuzz.po");
            }
        }
    }

    /// <summary>
    /// Draws distinct entries.
    /// </summary>
    /// <param name="count">The entry count.</param>
    /// <param name="nextKey">The next-key function.</param>
    /// <param name="nextValue">The next-value function.</param>
    /// <returns>The distinct entries in draw order.</returns>
    private static List<KeyValuePair<string, string>> DistinctEntries(int count, Func<string> nextKey, Func<string> nextValue) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<KeyValuePair<string, string>>(count);
        var guard = 0;
        while(entries.Count < count && guard++ < count * 50 + 50) {
            var key = nextKey();
            if(!seen.Add(key)) {
                continue;
            }

            entries.Add(new KeyValuePair<string, string>(key, nextValue()));
        }

        return entries;
    }

    /// <summary>
    /// Draws the next entry count.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>A count in <c>[1, MAX_ENTRIES]</c>.</returns>
    private static int NextEntryCount(FuzzRng rng) => rng.Next(1, MAX_ENTRIES + 1);

    /// <summary>
    /// Draws a safe key for INI/JSON/PO/MO/ResX/XLIFF documents.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>A key without separator, bracket, or quote characters.</returns>
    private static string NextSafeKey(FuzzRng rng) => "K" + rng.NextKey().Replace(".", "_", StringComparison.Ordinal).Replace("-", "_", StringComparison.Ordinal);

    /// <summary>
    /// Draws a bare TOML key (letters, digits, underscore, hyphen only).
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The bare key.</returns>
    private static string NextBareKey(FuzzRng rng) {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";
        var length = rng.Next(1, 20);
        var builder = new StringBuilder(length + 1);
        builder.Append((char)('A' + rng.Next(26)));
        for(var index = 1; index < length; index++) {
            builder.Append(alphabet[rng.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Draws a safe INI value (no newlines; trimmed of edge whitespace by construction).
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The value.</returns>
    private static string NextIniValue(FuzzRng rng) => "v" + rng.NextKey() + " " + rng.NextKey();

    /// <summary>
    /// Draws a safe value without quotes, backslashes, or newlines.
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The value.</returns>
    private static string NextSafeValue(FuzzRng rng) => "v-" + rng.NextKey() + "_" + rng.Next(1000000).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Draws a PO value (may carry whitespace and punctuation; never a raw newline).
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The value.</returns>
    private static string NextPoValue(FuzzRng rng) {
        const string alphabet = "abcXYZ019 \t.,:;#'\"\\/=<>()!?-_\u00e9\u4e2d\u263a";
        var length = rng.Next(0, 40);
        var builder = new StringBuilder(length + 1);
        builder.Append('v');
        for(var index = 0; index < length; index++) {
            builder.Append(alphabet[rng.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Draws an XML-representable value (no control characters).
    /// </summary>
    /// <param name="rng">The RNG.</param>
    /// <returns>The value.</returns>
    private static string NextXmlValue(FuzzRng rng) {
        const string alphabet = "abcXYZ019 .,;:!?=+-_\u00e9\u4e2d\u263a\u0645\u0631\u062d\u0628\u0627";
        var length = rng.Next(0, 40);
        var builder = new StringBuilder(length + 1);
        builder.Append('v');
        for(var index = 0; index < length; index++) {
            builder.Append(alphabet[rng.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Escapes a PO string body.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The escaped body.</returns>
    private static string EscapePo(string value) {
        var builder = new StringBuilder(value.Length);
        foreach(var current in value) {
            builder.Append(current switch {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => current.ToString(),
            });
        }

        return builder.ToString();
    }
}
