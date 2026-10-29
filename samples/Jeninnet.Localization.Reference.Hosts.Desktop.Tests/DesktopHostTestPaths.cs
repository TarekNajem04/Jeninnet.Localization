//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Hosts.Desktop.Tests;

/// <summary>
/// Builds a temporary directory replicating the reference content layout
/// (<c>Localization/&lt;culture&gt;.[json|xml]</c>) so file-based desktop host compositions can
/// resolve their inner message providers from content, and locates the repository root.
/// </summary>
internal static class DesktopHostTestPaths {
    /// <summary>
    /// Builds a temporary directory replicating the reference content layout.
    /// </summary>
    /// <returns>A disposable directory handle.</returns>
    public static TemporaryLocalizationDirectory CreateLocalizationDirectory() {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "JeninnetLocalizationReferenceDesktopHosts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        var sourceDirectory = Path.Combine(GetRepositoryRoot(), "samples", "Content", "Localization");
        var targetDirectory = Path.Combine(temporaryRoot, "Localization");
        Directory.CreateDirectory(targetDirectory);

        foreach(var extension in new[] { ".json", ".xml" }) {
            foreach(var file in Directory.EnumerateFiles(sourceDirectory, "*" + extension)) {
                File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }

        return new TemporaryLocalizationDirectory(temporaryRoot);
    }

    /// <summary>
    /// Resolves the repository root by walking upward until the central package manifest is found.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the repository root cannot be located.</exception>
    public static string GetRepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))) {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root (containing Directory.Packages.props) could not be located.");
    }

    /// <summary>
    /// A temporary directory that deletes itself on dispose.
    /// </summary>
    public sealed class TemporaryLocalizationDirectory : IDisposable {

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporaryLocalizationDirectory"/> class.
        /// </summary>
        /// <param name="path">The temporary directory path.</param>
        internal TemporaryLocalizationDirectory(string path) => Path = path;

        /// <summary>
        /// Gets the temporary directory path.
        /// </summary>
        public string Path { get; }

        /// <inheritdoc/>
        public void Dispose() {
            if(Directory.Exists(Path)) {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
