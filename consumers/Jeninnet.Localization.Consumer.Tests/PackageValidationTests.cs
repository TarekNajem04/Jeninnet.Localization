//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Consumer.Tests;

/// <summary>
/// Validates the packed solution from the consumer's side: the local feed carries every
/// packable package at the solution version, every package carries its repository metadata
/// and payload, the analyzer, generator, and tool packages ship their assets, and the
/// dependency closure stays inside the feed. The packable inventory is single-owned by the
/// committed public-API baselines (one baseline per packable assembly).
/// </summary>
[TestClass]
public class PackageValidationTests {
    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that feed contains every packable package at the solution version.
    /// </summary>
    [TestMethod]
    public void Feed_ContainsEveryPackablePackage_AtTheSolutionVersion() {
        var missing = ExpectedPackageIds()
            .Where(packageId => !File.Exists(PackagePath(packageId)))
            .Select(packageId => Path.GetFileName(PackagePath(packageId)))
            .ToList();

        missing.Should().BeEmpty(
            $"[package validation] every packable package must be in the local feed; run `dotnet pack Jeninnet.Localization.slnx -c Release` first (missing: {string.Join(", ", missing)})");
    }

    /// <summary>
    /// Verifies that every package carries the repository metadata and its payload.
    /// </summary>
    [TestMethod]
    public void EveryPackage_CarriesTheRepositoryMetadataAndItsPayload() {
        foreach(var packageId in ExpectedPackageIds()) {
            using var archive = OpenPackage(packageId);
            var entries = EntryNames(archive);

            entries.Should().Contain("LICENSE", $"{packageId} must ship the repository license");
            entries.Should().Contain("README.md", $"{packageId} must ship its package README");
            entries.Should().Contain("THIRD-PARTY-NOTICES.md", $"{packageId} must ship the third-party notices");

            var hasPayload = entries.Any(static name =>
                name.StartsWith("lib/", StringComparison.Ordinal)
                || name.StartsWith("analyzers/", StringComparison.Ordinal)
                || name.StartsWith("tools/", StringComparison.Ordinal));
            hasPayload.Should().BeTrue($"{packageId} must ship a lib/, analyzers/, or tools/ payload");

            var nuspec = ReadEntry(archive, $"{packageId}.nuspec");
            nuspec.Should().Contain("<license type=\"expression\">MIT</license>", $"{packageId} must declare the MIT license expression");
            nuspec.Should().Contain("<repository type=\"git\"", $"{packageId} must carry the source-repository metadata");
            nuspec.Should().Contain($"<version>{SolutionVersion}</version>", $"{packageId} must carry the solution version");
        }
    }

    /// <summary>
    /// Verifies that analyzer package ships the analyzer and its code fix.
    /// </summary>
    [TestMethod]
    public void AnalyzerPackage_ShipsTheAnalyzerAndItsCodeFix() {
        using var archive = OpenPackage("Jeninnet.Localization.Analyzers.Localizability");
        var entries = EntryNames(archive);

        entries.Should().Contain("analyzers/dotnet/cs/Jeninnet.Localization.Analyzers.Localizability.dll",
            "the analyzer package must ship its analyzer under analyzers/dotnet/cs");
        entries.Should().Contain("analyzers/dotnet/cs/Jeninnet.Localization.Analyzers.Localizability.CodeFixes.dll",
            "the code-fix assembly ships inside the analyzer package (it is not a package of its own)");
    }

    /// <summary>
    /// Verifies that generator package ships the incremental generator assembly.
    /// </summary>
    [TestMethod]
    public void GeneratorPackage_ShipsTheIncrementalGeneratorAssembly() {
        using var archive = OpenPackage("Jeninnet.Localization.Generators.Keys");

        EntryNames(archive).Should().Contain("analyzers/dotnet/cs/Jeninnet.Localization.Generators.Keys.dll",
            "the generator package must ship its generator under analyzers/dotnet/cs");
    }

    /// <summary>
    /// Verifies that tool package ships the dotnet tool assets and command.
    /// </summary>
    [TestMethod]
    public void ToolPackage_ShipsTheDotnetToolAssetsAndCommand() {
        using var archive = OpenPackage("Jeninnet.Localization.Tools.Keys");
        var entries = EntryNames(archive);

        entries.Should().Contain("tools/net10.0/any/DotnetToolSettings.xml", "a .NET tool package must carry the tool settings");
        entries.Should().Contain("tools/net10.0/any/Jeninnet.Localization.Tools.Keys.dll");
        ReadEntry(archive, "tools/net10.0/any/DotnetToolSettings.xml").Should().Contain("jeninnet-localization-keys",
            "the tool command name is the documented contract");
    }

    /// <summary>
    /// Verifies that every jeninnet dependency resolves inside the is feed.
    /// </summary>
    [TestMethod]
    public void EveryJeninnetDependency_ResolvesInsideTheFeed() {
        var available = ExpectedPackageIds().ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach(var packageId in ExpectedPackageIds()) {
            foreach(var dependency in JeninnetDependencies(packageId)) {
                if(!available.Contains(dependency)) {
                    missing.Add($"{packageId} -> {dependency}");
                }
            }
        }

        missing.Should().BeEmpty(
            $"[package validation] every Jeninnet dependency must itself be a package in the feed (missing: {string.Join(", ", missing)})");
    }

    /// <summary>
    /// Verifies that canonical dependency edges are is declared.
    /// </summary>
    [TestMethod]
    public void CanonicalDependencyEdges_AreDeclared() {
        JeninnetDependencies("Jeninnet.Localization.Core").Should().Contain("Jeninnet.Localization.Abstractions");
        JeninnetDependencies("Jeninnet.Localization.Providers.Memory").Should().Contain("Jeninnet.Localization.Abstractions");
        JeninnetDependencies("Jeninnet.Localization.Providers.Po").Should().Contain("Jeninnet.Localization.Storage");
        JeninnetDependencies("Jeninnet.Localization.Translation.Azure").Should().Contain("Jeninnet.Localization.Abstractions");
        JeninnetDependencies("Jeninnet.Localization.Interchange.Csv").Should().Contain("Jeninnet.Localization.Interchange.Abstractions");
        JeninnetDependencies("Jeninnet.Localization.Integrations.Mvc.Validation").Should().Contain("Jeninnet.Localization.Core",
            "the MVC validation integration is the documented integration -> Core exception (Rule07)");
    }

    /// <summary>
    /// Gets the repository root.
    /// </summary>
    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>
    /// Gets the feed directory.
    /// </summary>
    private static string FeedDirectory { get; } = Path.Combine(RepositoryRoot, "artifacts");

    /// <summary>
    /// Gets the solution version.
    /// </summary>
    private static string SolutionVersion { get; } = ReadSolutionVersion();

    /// <summary>
    /// Performs the expected package ids operation.
    /// </summary>
    /// <returns>The expected package ids sequence.</returns>
    private static string[] ExpectedPackageIds() =>
        [.. Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot, "tests", "Jeninnet.Localization.Tests", "ApiBaselines"),
                "*.Api.txt")
            .Select(static path => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)))
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Performs the package path operation.
    /// </summary>
    /// <param name="packageId">The package id.</param>
    /// <returns>The package path text.</returns>
    private static string PackagePath(string packageId) =>
        Path.Combine(FeedDirectory, $"{packageId}.{SolutionVersion}.nupkg");

    /// <summary>
    /// Opens package.
    /// </summary>
    /// <param name="packageId">The package id.</param>
    /// <returns>The open package result.</returns>
    private static ZipArchive OpenPackage(string packageId) => ZipFile.OpenRead(PackagePath(packageId));

    /// <summary>
    /// Performs the entry names operation.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <returns>The entry names sequence.</returns>
    private static string[] EntryNames(ZipArchive archive) =>
        [.. archive.Entries.Select(static entry => entry.FullName)];

    /// <summary>
    /// Reads entry.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="name">The name.</param>
    /// <returns>The entry text.</returns>
    private static string ReadEntry(ZipArchive archive, string name) {
        var entry = archive.Entries.FirstOrDefault(candidate => candidate.FullName == name)
            ?? throw new InvalidOperationException($"'{name}' is missing from the package");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Performs the jeninnet dependencies operation.
    /// </summary>
    /// <param name="packageId">The package id.</param>
    /// <returns>The jeninnet dependencies sequence.</returns>
    private static string[] JeninnetDependencies(string packageId) {
        using var archive = OpenPackage(packageId);
        var nuspec = XDocument.Parse(ReadEntry(archive, $"{packageId}.nuspec"));

        // The nuspec root declares the packaging namespace, so match on local names.
        return [.. nuspec.Descendants()
            .Where(static element => element.Name.LocalName == "dependency")
            .Select(static dependency => (string?)dependency.Attribute("id"))
            .Where(static id => id is not null && id.StartsWith("Jeninnet.Localization.", StringComparison.Ordinal))
            .Select(static id => id!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
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

        return directory?.FullName
            ?? throw new InvalidOperationException("the repository root could not be located from the test output directory");
    }

    /// <summary>
    /// Reads solution version.
    /// </summary>
    /// <returns>The solution version text.</returns>
    private static string ReadSolutionVersion() {
        var buildProps = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        var version = buildProps.Descendants()
            .Where(static element => element.Name.LocalName == "Version")
            .Select(static element => element.Value.Trim())
            .First(static value => value.Length > 0);

        // Keep the prerelease label intact; only redundant trailing zero segments are removed.
        var parts = version.Split('.');
        var keep = parts.Length;
        while(keep > 3 && parts[keep - 1] == "0") {
            keep--;
        }

        return string.Join('.', parts.Take(keep));
    }
}
