//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Fixture consumer project tests: the generator runs inside a real
/// <c>dotnet build</c> (happy path green), and renaming a catalog key breaks the
/// consuming compilation (typos/drift become build errors). Each run copies the
/// fixture to an isolated temp directory; the solution build never sees red.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class LocalizationKeysGeneratorFixtureTests {
    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that fixture builds with generated keys.
    /// </summary>
    [TestMethod]
    [Timeout(600000, CooperativeCancellation = true)]
    public async Task Fixture_Builds_WithGeneratedKeysAsync() {
        var fixture = CopyFixture();
        try {
            var (ExitCode, Output) = await BuildAsync(fixture, TestContext?.CancellationToken ?? CancellationToken.None);

            ExitCode.Should().Be(0, $"the fixture consumes generated keys and must build:{Environment.NewLine}{Output}");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Verifies that fixture breaks on rename.
    /// </summary>
    [TestMethod]
    [Timeout(600000, CooperativeCancellation = true)]
    public async Task Fixture_Breaks_OnRenameAsync() {
        var fixture = CopyFixture();
        try {
            var catalogPath = Path.Combine(fixture, "Messages.loc.json");
            var catalog = await File.ReadAllTextAsync(catalogPath, TestContext?.CancellationToken ?? CancellationToken.None);
            catalog.Should().Contain("Catalog.Heading");
            await File.WriteAllTextAsync(
                catalogPath,
                catalog.Replace("Catalog.Heading", "Catalog.Headline", StringComparison.Ordinal),
                TestContext?.CancellationToken ?? CancellationToken.None);

            var (ExitCode, Output) = await BuildAsync(fixture, TestContext?.CancellationToken ?? CancellationToken.None);

            (ExitCode == 0).Should().BeFalse($"renaming a key must break the consumer:{Environment.NewLine}{Output}");
            Output.Should().Contain("CATALOG_HEADING", "the error names the stale constant");
            Output.Should().Contain("error CS", "drift surfaces as a compiler error, not a silent skip");
        }
        finally {
            DeleteQuietly(fixture);
        }
    }

    /// <summary>
    /// Performs the copy fixture operation.
    /// </summary>
    /// <returns>The copy fixture text.</returns>
    private static string CopyFixture() {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "tests", "Fixtures", "LocalizationKeys");
        var target = Path.Combine(Path.GetTempPath(), "jlnkey-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(target);
        foreach(var file in Directory.GetFiles(source)) {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        // The fixture build runs `dotnet build` from the temp directory, where the repository's
        // global.json does not apply. Copy it so the child build uses the same SDK as the test run;
        // otherwise the generator is compiled by a newer SDK and the test process rejects the
        // analyzer with CS9057 ("references a newer compiler version").
        var globalJson = Path.Combine(root, "global.json");
        if(File.Exists(globalJson)) {
            File.Copy(globalJson, Path.Combine(target, "global.json"));
        }

        return target;
    }

    /// <summary>
    /// Performs the repository root operation.
    /// </summary>
    /// <returns>The repository root text.</returns>
    private static string RepositoryRoot() {
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
    /// Performs the build operation.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is build.</returns>
    private static async Task<(int ExitCode, string Output)> BuildAsync(string directory, CancellationToken cancellationToken) {
        var generatorProject = Path.Combine(
            RepositoryRoot(),
            "src", "generators", "Jeninnet.Localization.Generators.Keys", "Jeninnet.Localization.Generators.Keys.csproj");
        var generatorConfiguration = "Fixture" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("LocalizationKeys.Fixture.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add("minimal");
        start.ArgumentList.Add($"/p:GeneratorProjectPath={generatorProject}");
        start.ArgumentList.Add($"/p:GeneratorConfiguration={generatorConfiguration}");

        using var process = Process.Start(start) ?? throw new InvalidOperationException("could not start the dotnet build process");
        var output = new StringBuilder();
        process.OutputDataReceived += (_, args) => {
            if(args.Data is not null) {
                output.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) => {
            if(args.Data is not null) {
                output.AppendLine(args.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (process.ExitCode, output.ToString());
    }

    /// <summary>
    /// Deletes quietly.
    /// </summary>
    /// <param name="directory">The directory.</param>
    private static void DeleteQuietly(string directory) {
        try {
            Directory.Delete(directory, recursive: true);
        }
        catch(IOException) {
            // Best effort: temp copies must never fail the test run.
        }
        catch(UnauthorizedAccessException) {
            // Best effort: temp copies must never fail the test run.
        }
    }
}
