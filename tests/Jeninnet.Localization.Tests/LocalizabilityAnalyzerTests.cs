//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Analyzer harness tests: seeded violations across WinForms/WPF/MVC/API
/// shapes are flagged with actionable messages, while identifiers, logging calls,
/// and suppressed literals stay quiet. Severity configuration flows through the
/// documented MSBuild property.
/// </summary>
[TestClass]
public sealed class LocalizabilityAnalyzerTests {
    /// <summary>
    /// Verifies that win forms text assignment is flagged.
    /// </summary>
    [TestMethod]
    public async Task WinForms_TextAssignment_FlaggedAsync() {
        const string source = """
            namespace System.Windows.Forms {
                public class Control {
                    public string Text { get; set; } = "";
                }
            }
            public class Form : System.Windows.Forms.Control {
            }
            public static class Consumer {
                public static void Run(Form form) {
                    form.Text = "Hello";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNLOC001");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Hidden);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("UI text");
        diagnostics[0].Location.GetLineSpan().StartLinePosition.Line.Should().Be(9);
    }

    /// <summary>
    /// Verifies that wpf content assignment is flagged.
    /// </summary>
    [TestMethod]
    public async Task Wpf_ContentAssignment_FlaggedAsync() {
        const string source = """
            namespace System.Windows {
                public class DependencyObject {
                }
                public class FrameworkElement : DependencyObject {
                    public object Content { get; set; } = "";
                }
            }
            public static class Consumer {
                public static void Run(System.Windows.FrameworkElement element) {
                    element.Content = "Hello";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Content");
    }

    /// <summary>
    /// Verifies that exception message is flagged.
    /// </summary>
    [TestMethod]
    public async Task ExceptionMessage_FlaggedAsync() {
        const string source = """
            using System;
            public static class Consumer {
                public static void Run(bool fail) {
                    if(fail) {
                        throw new InvalidOperationException("Boom");
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("InvalidOperationException");
    }

    /// <summary>
    /// Verifies that validation result is flagged.
    /// </summary>
    [TestMethod]
    public async Task ValidationResult_FlaggedAsync() {
        const string source = """
            public sealed class ValidationResult {
                public ValidationResult(string message) {
                }
            }
            public static class Consumer {
                public static object Check(bool fail) {
                    return fail ? new ValidationResult("Bad value") : new object();
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("validation");
    }

    /// <summary>
    /// Verifies that add model error is flagged.
    /// </summary>
    [TestMethod]
    public async Task AddModelError_FlaggedAsync() {
        const string source = """
            public sealed class ModelStateDictionary {
                public void AddModelError(string key, string message) {
                }
            }
            public static class Consumer {
                public static void Run(ModelStateDictionary state) {
                    state.AddModelError("Name", "Name is required.");
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("AddModelError");
    }

    /// <summary>
    /// Verifies that message box show is flagged.
    /// </summary>
    [TestMethod]
    public async Task MessageBoxShow_FlaggedAsync() {
        const string source = """
            public static class MessageBox {
                public static void Show(string text) {
                }
            }
            public static class Consumer {
                public static void Run() {
                    MessageBox.Show("Saved");
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Show");
    }

    /// <summary>
    /// Verifies that problem call is flagged.
    /// </summary>
    [TestMethod]
    public async Task ProblemCall_FlaggedAsync() {
        const string source = """
            public static class Results {
                public static string Problem(string detail) => detail;
            }
            public static class Consumer {
                public static string Run() {
                    return Results.Problem("Something broke.");
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Problem");
    }

    /// <summary>
    /// Verifies that view data assignment is flagged.
    /// </summary>
    [TestMethod]
    public async Task ViewDataAssignment_FlaggedAsync() {
        const string source = """
            public sealed class ViewDataDictionary {
                public object this[string key] {
                    get { return new object(); }
                    set { }
                }
            }
            public class Page {
                public ViewDataDictionary ViewData { get; } = new ViewDataDictionary();
                public void Render() {
                    ViewData["Title"] = "Catalog";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("view-dictionary");
    }

    /// <summary>
    /// Verifies that error message attribute is flagged.
    /// </summary>
    [TestMethod]
    public async Task ErrorMessageAttribute_FlaggedAsync() {
        const string source = """
            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class RequiredAttribute : System.Attribute {
                public string? ErrorMessage { get; set; }
            }
            public class Model {
                [Required(ErrorMessage = "Name is required.")]
                public string Name { get; set; } = "";
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("RequiredAttribute");
    }

    /// <summary>
    /// Verifies that display name attribute flagged once.
    /// </summary>
    [TestMethod]
    public async Task DisplayNameAttribute_FlaggedOnceAsync() {
        const string source = """
            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class DisplayAttribute : System.Attribute {
                public string? Name { get; set; }
            }
            public class Model {
                [Display(Name = "Full name")]
                public string Name { get; set; } = "";
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1, "named display text reports exactly once");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("DisplayAttribute");
    }

    /// <summary>
    /// Verifies that non user facing shapes quiet.
    /// </summary>
    [TestMethod]
    public async Task NonUserFacingShapes_QuietAsync() {
        const string source = """
            using System.Collections.Generic;
            public static class Consumer {
                public const string Key = "App.Title";
                private static readonly Dictionary<string, string> Table = new();
                public static void Run(object logger, string name) {
                    var local = "temporary";
                    Table["key"] = local;
                }
                public static string Identify(string id) {
                    return id;
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("identifiers, locals, and dictionary writes are not user-facing shapes");
    }

    /// <summary>
    /// Verifies that non text assignments quiet.
    /// </summary>
    [TestMethod]
    public async Task NonTextAssignments_QuietAsync() {
        const string source = """
            namespace System.Windows.Forms {
                public class Control {
                    public string Text { get; set; } = "";
                    public object Tag { get; set; } = new object();
                }
            }
            public class Book {
                public string Title { get; set; } = "";
            }
            public static class Consumer {
                public static void Run(System.Windows.Forms.Control control, Book book) {
                    control.Tag = "internal-marker";
                    control.Text = "";
                    book.Title = "Seeded title";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("non-text properties, empty text, and non-UI receivers stay quiet");
    }

    /// <summary>
    /// Verifies that pragma suppression quiet.
    /// </summary>
    [TestMethod]
    public async Task PragmaSuppression_QuietAsync() {
        const string source = """
            public static class Consumer {
                public static void Run(bool fail) {
                    if(fail) {
                        #pragma warning disable JLNLOC001
                        throw new InvalidOperationException("Boom");
                        #pragma warning restore JLNLOC001
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("pragma suppression is honored");
    }

    /// <summary>
    /// Verifies that suppress message quiet.
    /// </summary>
    [TestMethod]
    public async Task SuppressMessage_QuietAsync() {
        const string source = """
            public static class Consumer {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Localizability", "JLNLOC001", Justification = "Internal-only throw.")]
                public static void Run(bool fail) {
                    if(fail) {
                        throw new InvalidOperationException("Boom");
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("SuppressMessage suppression is honored");
    }

    /// <summary>
    /// Verifies that severity property maps to diagnostic severity.
    /// </summary>
    [TestMethod]
    public void SeverityProperty_MapsToDiagnosticSeverity() {
        LocalizabilityAnalyzer.ResolveSeverity(TestOptions(null)).Should().Be(DiagnosticSeverity.Hidden);
        LocalizabilityAnalyzer.ResolveSeverity(TestOptions("suggestion")).Should().Be(DiagnosticSeverity.Hidden);
        LocalizabilityAnalyzer.ResolveSeverity(TestOptions("WARNING")).Should().Be(DiagnosticSeverity.Warning);
        LocalizabilityAnalyzer.ResolveSeverity(TestOptions("error")).Should().Be(DiagnosticSeverity.Error);
        LocalizabilityAnalyzer.ResolveSeverity(TestOptions("definitely-not-a-severity")).Should().Be(DiagnosticSeverity.Hidden);
    }

    /// <summary>
    /// Verifies that severity warning surfaces in diagnostics.
    /// </summary>
    [TestMethod]
    public async Task SeverityWarning_SurfacesInDiagnosticsAsync() {
        const string source = """
            using System;
            public static class Consumer {
                public static void Run(bool fail) {
                    if(fail) {
                        throw new InvalidOperationException("Boom");
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().HaveCount(1);
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Hidden);
    }

    private static readonly string[] _analyzerConfigurations = ["Debug", "Release"];

    /// <summary>
    /// Locates the built analyzer assembly for whichever configuration the repository was built
    /// in (the test run is configuration-agnostic, so a hard-coded Debug path would fail a
    /// Release-only build and vice versa).
    /// </summary>
    /// <param name="root">The root directory of the repository.</param>
    /// <exception cref="FileNotFoundException">Thrown when the analyzer assembly is not found.</exception>
    /// <returns>The built analyzer path text.</returns>
    private static string ResolveBuiltAnalyzerPath(string root) {
        const string assemblyName = "Jeninnet.Localization.Analyzers.Localizability.dll";
        var outputRoot = Path.Combine(root, "src", "analyzers", "Jeninnet.Localization.Analyzers.Localizability", "bin");
        var candidates = _analyzerConfigurations
            .Select(configuration => Path.Combine(outputRoot, configuration, assemblyName))
            .ToArray();

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"The analyzer assembly was not found; build the solution first. Probed: {string.Join(", ", candidates)}");
    }

    /// <summary>
    /// Verifies that built analyzer dll loads and runs.
    /// </summary>
    [TestMethod]
    public async Task BuiltAnalyzerDll_LoadsAndRunsAsync() {
        const string source = """
            using System;
            public static class Consumer {
                public static void Run(bool fail) {
                    if(fail) {
                        throw new InvalidOperationException("Boom");
                    }
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), path: "Test.cs", cancellationToken: TestContext.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "DllProbe",
            [tree],
            TestReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var root = FindRepositoryRoot();
        var analyzerPath = ResolveBuiltAnalyzerPath(root);
        var reference = new AnalyzerFileReference(analyzerPath, new TestAssemblyLoader());
        var analyzers = reference.GetAnalyzersForAllLanguages();
        (analyzers.Length > 0).Should().BeTrue("the built analyzer assembly must expose its analyzer");
        // S8949: the current (non-obsolete) DiagnosticAnalyzerExtensions.WithAnalyzers overload takes no
        // CancellationToken, so the analyzer-driver token cannot be forwarded here; the analyzer run is
        // bounded by the synchronous pump above instead.
#pragma warning disable MSTEST0049 // Flow TestContext.CancellationToken to async operations
#pragma warning disable S8949 // The overload accepting a 'CancellationToken' should be used
        var diagnostics = await compilation.WithAnalyzers(analyzers, options: null).GetAnalyzerDiagnosticsAsync(TestContext.CancellationToken);
#pragma warning restore S8949 // The overload accepting a 'CancellationToken' should be used
#pragma warning restore MSTEST0049 // Flow TestContext.CancellationToken to async operations
        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("JLNLOC001");
    }

    /// <summary>
    /// Finds repository root.
    /// </summary>
    /// <returns>The repository root text.</returns>
    private static string FindRepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))) {
            directory = directory.Parent;
        }

        if(directory is null) {
            throw new InvalidOperationException("repository root could not be located from the test output directory");
        }

        return directory.FullName;
    }

    /// <summary>
    /// Represents a test assembly loader.
    /// </summary>
    private sealed class TestAssemblyLoader : IAnalyzerAssemblyLoader {
        /// <summary>
        /// Adds dependency location.
        /// </summary>
        /// <param name="fullPath">The full path.</param>
        public void AddDependencyLocation(string fullPath) {
        }

        /// <summary>
        /// Loads from path.
        /// </summary>
        /// <param name="fullPath">The full path.</param>
        /// <returns>The from path result.</returns>
        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
    /// <summary>
    /// Performs the analyze operation.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A task whose result is analyze.</returns>
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "LocalizabilityReview",
            [tree],
            TestReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new LocalizabilityAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Performs the test references operation.
    /// </summary>
    /// <returns>The test references result.</returns>
    private static ImmutableArray<MetadataReference> TestReferences() {
        var assemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?.Split(Path.PathSeparator);
        if(assemblies is null) {
            return [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        }

        return [.. assemblies
            .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(static path => MetadataReference.CreateFromFile(path))];
    }

    /// <summary>
    /// Performs the test options operation.
    /// </summary>
    /// <param name="severity">The severity.</param>
    /// <returns>The test options result.</returns>
    private static SeverityTestOptions TestOptions(string? severity) => new(severity);

    /// <summary>
    /// Specifies the options for severity test.
    /// </summary>
    /// <param name="severity">The severity.</param>
    private sealed class SeverityTestOptions(string? severity) : AnalyzerConfigOptions {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) {
            if(key.Equals(LocalizabilityAnalyzer.SEVERITY_PROPERTY, StringComparison.Ordinal) && severity is not null) {
                value = severity;
                return true;
            }

            value = null;
            return false;
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
