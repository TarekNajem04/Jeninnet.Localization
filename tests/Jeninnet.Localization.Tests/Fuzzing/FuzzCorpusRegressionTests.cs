//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests.Fuzzing;

/// <summary>
/// Regression tests over the committed hostile-input corpora in
/// <c>FuzzCorpora/</c>: each malformed sample asserts the documented exception type
/// plus file/line diagnostics. The corpora seed real-world hostile shapes (overlong
/// lines, bad escapes, truncated MO binaries, bad UTF-8, version drift).
/// </summary>
[TestClass]
public sealed class FuzzCorpusRegressionTests {
    /// <summary>
    /// Verifies that json bad escape corpus reports syntax error.
    /// </summary>
    [TestMethod]
    public void Json_BadEscape_Corpus_ReportsSyntaxError() {
        var path = CorpusPath("json.bad-escape.json");
        var result = LocJsonParser.Parse(File.ReadAllText(path));
        result.SyntaxError.Should().NotBeNull("a bad escape is structurally invalid");
        result.SyntaxError!.Message.Should().Contain("escape");
        (result.SyntaxError.Offset >= 0).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that ini overlong line corpus throws invalid data exception.
    /// </summary>
    [TestMethod]
    public void Ini_OverlongLine_Corpus_ThrowsInvalidDataException() {
        var path = CorpusPath("ini.overlong-line.ini");
        Action act = () => IniParser.Parse(File.ReadAllText(path), path);
        var exception = act.Should().Throw<InvalidDataException>().Exception;
        exception.Message.Should().Contain("line 1");
        exception.Message.Should().Contain(path);
    }

    /// <summary>
    /// Verifies that toml bad escape corpus throws invalid data exception.
    /// </summary>
    [TestMethod]
    public void Toml_BadEscape_Corpus_ThrowsInvalidDataException() {
        var path = CorpusPath("toml.bad-escape.toml");
        Action act = () => TomlParser.Parse(File.ReadAllText(path), path);
        var exception = act.Should().Throw<InvalidDataException>().Exception;
        exception.Message.Should().Contain("line 1");
        exception.Message.Should().Contain(path);
    }

    /// <summary>
    /// Verifies that po unterminated string corpus throws po parse exception.
    /// </summary>
    [TestMethod]
    public void Po_UnterminatedString_Corpus_ThrowsPoParseException() {
        var path = CorpusPath("po.unterminated-string.po");
        Action act = () => PoParser.Parse(File.ReadAllText(path), path);
        var exception = act.Should().Throw<PoParseException>().Exception;
        exception.FilePath.Should().Be(path);
        (exception.LineNumber > 0).Should().BeTrue("text diagnostics carry a 1-based line");
        exception.Message.Should().Contain(path);
    }

    /// <summary>
    /// Verifies that mo truncated corpus throws po parse exception.
    /// </summary>
    [TestMethod]
    public void Mo_Truncated_Corpus_ThrowsPoParseException() {
        var path = CorpusPath("mo.truncated.mo");
        Action act = () => MoParser.Parse(File.ReadAllBytes(path), path);
        var exception = act.Should().Throw<PoParseException>().Exception;
        exception.FilePath.Should().Be(path);
        exception.LineNumber.Should().Be(0, "binary diagnostics carry no line numbers");
        exception.Message.Should().Contain("too small");
    }

    /// <summary>
    /// Verifies that mo bad magic corpus throws po parse exception.
    /// </summary>
    [TestMethod]
    public void Mo_BadMagic_Corpus_ThrowsPoParseException() {
        var path = CorpusPath("mo.bad-magic.mo");
        Action act = () => MoParser.Parse(File.ReadAllBytes(path), path);
        var exception = act.Should().Throw<PoParseException>().Exception;
        exception.FilePath.Should().Be(path);
        exception.Message.Should().Contain("magic");
    }

    /// <summary>
    /// Verifies that mo bad utf8 corpus throws po parse exception.
    /// </summary>
    [TestMethod]
    public void Mo_BadUtf8_Corpus_ThrowsPoParseException() {
        var path = CorpusPath("mo.bad-utf8.mo");
        Action act = () => MoParser.Parse(File.ReadAllBytes(path), path);
        var exception = act.Should().Throw<PoParseException>().Exception;
        exception.FilePath.Should().Be(path);
        exception.LineNumber.Should().Be(0, "binary diagnostics carry no line numbers");
        exception.Message.Should().Contain("UTF-8");
    }

    /// <summary>
    /// Verifies that res x malformed corpus throws res x interchange exception.
    /// </summary>
    [TestMethod]
    public void ResX_Malformed_Corpus_ThrowsResXInterchangeException() {
        var path = CorpusPath("resx.malformed.resx");
        Action act = () => ResXCatalogInterchange.ParseDocument(File.ReadAllText(path), path);
        var exception = act.Should().Throw<ResXInterchangeException>().Exception;
        exception.FilePath.Should().Be(path);
        exception.Message.Should().Contain(path);
    }

    /// <summary>
    /// Verifies that xliff missing version corpus throws xliff interchange exception.
    /// </summary>
    [TestMethod]
    public void Xliff_MissingVersion_Corpus_ThrowsXliffInterchangeException() {
        var path = CorpusPath("xliff.missing-version.xliff");
        Action act = () => XliffCatalogInterchange.ParseDocument(File.ReadAllText(path), path);
        var exception = act.Should().Throw<XliffInterchangeException>().Exception;
        exception.FilePath.Should().Be(path);
        exception.Message.Should().Contain("2.0");
    }

    /// <summary>
    /// Resolves a corpus file path under the test project source tree.
    /// </summary>
    /// <param name="fileName">The corpus file name.</param>
    /// <returns>The absolute corpus path.</returns>
    private static string CorpusPath(string fileName) {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))) {
            directory = directory.Parent;
        }

        if(directory is null) {
            throw new InvalidOperationException("repository root could not be located from the test output directory");
        }

        var path = Path.Combine(directory.FullName, "tests", "Jeninnet.Localization.Tests", "Fuzzing", "FuzzCorpora", fileName);
        File.Exists(path).Should().BeTrue($"the committed corpus file '{fileName}' must exist");
        return path;
    }
}
