//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// One extracted key reference: a constant member access resolved later, or a raw
/// string literal in a key position.
/// </summary>
internal sealed class KeyReference {
    /// <summary>
    /// Gets the constant member name, or <see langword="null"/> for literals.
    /// </summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// Gets the literal key value, or <see langword="null"/> for member accesses.
    /// </summary>
    public string? LiteralValue { get; set; }

    /// <summary>
    /// Gets the referencing file relative to the working directory (forward slashes).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    public int Line { get; set; } = 1;

    /// <summary>
    /// Gets the 1-based column number.
    /// </summary>
    public int Column { get; set; } = 1;
}

/// <summary>
/// One constant definition (<c>const string NAME = "value"</c>).
/// </summary>
internal sealed class KeyDefinition {
    /// <summary>
    /// Gets the constant name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the key value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets the defining file relative to the working directory (forward slashes).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the 1-based defining line.
    /// </summary>
    public int Line { get; set; } = 1;
}

/// <summary>
/// The extraction outcome: references, definitions, and scanned files.
/// </summary>
internal sealed class ExtractionResult {
    /// <summary>
    /// Gets the extracted references in scan order.
    /// </summary>
    public List<KeyReference> References { get; } = [];

    /// <summary>
    /// Gets the harvested constant definitions in scan order.
    /// </summary>
    public List<KeyDefinition> Definitions { get; } = [];

    /// <summary>
    /// Gets the scanned source files in ordinal path order.
    /// </summary>
    public List<string> ScannedFiles { get; } = [];
}

/// <summary>
/// Syntax-only key extraction: Roslyn walks for C# (constant member
/// accesses plus string literals in key positions) and textual member-access scans
/// for Razor views. No compilation is required, so extraction works on
/// non-compiling trees — a deliberate CI property, not a limitation.
/// </summary>
internal static partial class KeyExtractor {
    private static readonly string[] _keyPositionMethods = [
        "GetMessageTemplateAsync",
        "GetMessageTemplatesAsync",
        "GetPluralTemplateAsync",
        "GetPluralTemplateWithContextAsync",
        "LocalizeAsync",
        "Localize",
        "LocalizePluralAsync",
        "LocalizeWithContextAsync",
        "GetString",
    ];

    /// <summary>
    /// Scans source roots plus explicit keys files.
    /// </summary>
    /// <param name="sourceRoots">The source roots scanned recursively.</param>
    /// <param name="keysFiles">Explicit C# files parsed for constant definitions.</param>
    /// <returns>The extraction outcome.</returns>
    public static ExtractionResult Extract(IReadOnlyList<string> sourceRoots, IReadOnlyList<string> keysFiles) {
        ArgumentNullException.ThrowIfNull(sourceRoots);
        ArgumentNullException.ThrowIfNull(keysFiles);

        var result = new ExtractionResult();
        var files = new SortedSet<string>(StringComparer.Ordinal);
        var harvested = new HashSet<string>(StringComparer.Ordinal);

        foreach(var root in sourceRoots) {
            foreach(var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
                if(IsSkippedDirectory(file) || !IsSourceFile(file)) {
                    continue;
                }

                files.Add(Path.GetFullPath(file));
            }
        }

        foreach(var file in files) {
            ExtractFile(file, result, harvested);
        }

        foreach(var keysFile in keysFiles) {
            var full = Path.GetFullPath(keysFile);
            if(files.Add(full)) {
                ExtractFile(full, result, harvested);
            } else {
                HarvestDefinitions(full, result, harvested, force: true);
            }
        }

        result.ScannedFiles.AddRange(files.Select(RelativePath));
        return result;
    }

    /// <summary>
    /// Extracts file.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="result">The result.</param>
    /// <param name="harvested">The harvested.</param>
    private static void ExtractFile(string fullPath, ExtractionResult result, HashSet<string> harvested) {
        var extension = Path.GetExtension(fullPath);
        if(extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)) {
            ExtractCSharp(fullPath, result, harvested);
        } else {
            ExtractMarkup(fullPath, result);
        }
    }

    /// <summary>
    /// Extracts CSharp.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="result">The result.</param>
    /// <param name="harvested">The harvested.</param>
    private static void ExtractCSharp(string fullPath, ExtractionResult result, HashSet<string> harvested) {
        // Generated code carries no references, but its constants are definitions
        //.
        if(IsGeneratedFile(fullPath)) {
            HarvestDefinitions(fullPath, result, harvested, force: true);
            return;
        }

        var text = File.ReadAllText(fullPath);
        var tree = CSharpSyntaxTree.ParseText(text, path: fullPath);
        var root = tree.GetRoot();
        var relative = RelativePath(fullPath);

        foreach(var node in root.DescendantNodes()) {
            if(node is MemberAccessExpressionSyntax access &&
               access.Expression is IdentifierNameSyntax owner &&
               owner.Identifier.Text.EndsWith("Keys", StringComparison.Ordinal) &&
               access.Name is IdentifierNameSyntax member &&
               !IsInvoked(access)) {
                var position = access.Name.GetLocation().GetLineSpan().StartLinePosition;
                result.References.Add(new KeyReference {
                    MemberName = member.Identifier.Text,
                    Path = relative,
                    Line = position.Line + 1,
                    Column = position.Character + 1,
                });
            } else if(node is InvocationExpressionSyntax invocation &&
                      GetMethodName(invocation) is string method &&
                      IsKeyPositionMethod(method, invocation.ArgumentList.Arguments.Count) &&
                      invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal &&
                      literal.IsKind(SyntaxKind.StringLiteralExpression)) {
                var position = literal.GetLocation().GetLineSpan().StartLinePosition;
                result.References.Add(new KeyReference {
                    LiteralValue = literal.Token.ValueText,
                    Path = relative,
                    Line = position.Line + 1,
                    Column = position.Character + 1,
                });
            } else if(node is ElementAccessExpressionSyntax indexer &&
                      indexer.ArgumentList.Arguments.Count == 1 &&
                      indexer.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax indexLiteral &&
                      indexLiteral.IsKind(SyntaxKind.StringLiteralExpression) &&
                      indexer.Expression is IdentifierNameSyntax receiver &&
                      receiver.Identifier.Text.EndsWith("Localizer", StringComparison.OrdinalIgnoreCase)) {
                var position = indexLiteral.GetLocation().GetLineSpan().StartLinePosition;
                result.References.Add(new KeyReference {
                    LiteralValue = indexLiteral.Token.ValueText,
                    Path = relative,
                    Line = position.Line + 1,
                    Column = position.Character + 1,
                });
            }
        }

        HarvestDefinitions(fullPath, result, harvested, force: false);
    }

    /// <summary>
    /// Performs the harvest definitions operation.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="result">The result.</param>
    /// <param name="harvested">The harvested.</param>
    /// <param name="force">The force.</param>
    private static void HarvestDefinitions(string fullPath, ExtractionResult result, HashSet<string> harvested, bool force) {
        if(!force && !IsKeysFile(fullPath)) {
            return;
        }

        if(!harvested.Add(fullPath)) {
            return;
        }

        var text = File.ReadAllText(fullPath);
        var root = CSharpSyntaxTree.ParseText(text, path: fullPath).GetRoot();
        var relative = RelativePath(fullPath);

        foreach(var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>()) {
            if(!field.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ConstKeyword))) {
                continue;
            }

            foreach(var variable in field.Declaration.Variables) {
                if(variable.Initializer?.Value is LiteralExpressionSyntax literal &&
                   literal.IsKind(SyntaxKind.StringLiteralExpression)) {
                    var position = variable.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                    result.Definitions.Add(new KeyDefinition {
                        Name = variable.Identifier.Text,
                        Value = literal.Token.ValueText,
                        Path = relative,
                        Line = position.Line + 1,
                    });
                }
            }
        }
    }

    /// <summary>
    /// Extracts markup.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <param name="result">The result.</param>
    private static void ExtractMarkup(string fullPath, ExtractionResult result) {
        var relative = RelativePath(fullPath);
        var lines = File.ReadAllLines(fullPath);
        for(var index = 0; index < lines.Length; index++) {
            var lineNumber = index + 1;
            foreach(var groups in MemberAccessPattern().Matches(lines[index]).Cast<Match>().Select(static match => match.Groups)) {
                result.References.Add(new KeyReference {
                    MemberName = groups[2].Value,
                    Path = relative,
                    Line = lineNumber,
                    Column = groups[2].Index + 1,
                });
            }

            foreach(var groups in LocalizerIndexPattern().Matches(lines[index]).Cast<Match>().Select(static match => match.Groups)) {
                result.References.Add(new KeyReference {
                    LiteralValue = groups[1].Value,
                    Path = relative,
                    Line = lineNumber,
                    Column = groups[1].Index + 1,
                });
            }
        }
    }

    /// <summary>
    /// Gets method name.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The method name result.</returns>
    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax access when access.Name is IdentifierNameSyntax name => name.Identifier.Text,
            _ => null,
        };

    /// <summary>
    /// Returns whether a member access is the direct target of an invocation
    /// (<c>requiredKeys.Where(...)</c>). Key constants are strings and are never
    /// invoked; invocation targets on <c>*Keys</c> receivers are LINQ-style
    /// collection queries, not key references.
    /// </summary>
    /// <param name="access">The member access.</param>
    /// <returns><see langword="true"/> when directly invoked.</returns>
    private static bool IsInvoked(MemberAccessExpressionSyntax access) =>
        access.Parent is InvocationExpressionSyntax invocation &&
        invocation.Expression == access;

    /// <summary>
    /// Determines whether the value is key position method.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="argumentCount">The argument count.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsKeyPositionMethod(string method, int argumentCount) {
        if(_keyPositionMethods.Contains(method, StringComparer.Ordinal)) {
            return true;
        }

        return method.Equals("GetStringAsync", StringComparison.Ordinal) && argumentCount <= 2;
    }

    /// <summary>
    /// Determines whether the value is skipped directory.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsSkippedDirectory(string fullPath) {
        var directory = Path.GetDirectoryName(fullPath);
        while(directory is not null) {
            var name = Path.GetFileName(directory);
            if(name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
               (name.Length > 1 && name[0] == '.')) {
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    /// <summary>
    /// Determines whether the value is source file.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsSourceFile(string fullPath) {
        var extension = Path.GetExtension(fullPath);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the value is keys file.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsKeysFile(string fullPath) =>
        Path.GetFileNameWithoutExtension(fullPath).Contains("keys", StringComparison.OrdinalIgnoreCase) &&
        Path.GetExtension(fullPath).Equals(".cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the value is generated file.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsGeneratedFile(string fullPath) {
        var name = Path.GetFileName(fullPath);
        return name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Performs the relative path operation.
    /// </summary>
    /// <param name="fullPath">The full path.</param>
    /// <returns>The relative path text.</returns>
    private static string RelativePath(string fullPath) {
        var current = Path.GetFullPath(Environment.CurrentDirectory);
        if(fullPath.StartsWith(current, StringComparison.OrdinalIgnoreCase)) {
            var relative = fullPath[current.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        return fullPath.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Performs the member access pattern operation.
    /// </summary>
    /// <returns>The member access pattern result.</returns>
    [GeneratedRegex(@"(?<![\w.])([A-Za-z_]\w*Keys)\.([A-Za-z_]\w*)(?![\w])")]
    private static partial Regex MemberAccessPattern();

    /// <summary>
    /// Performs the localizer index pattern operation.
    /// </summary>
    /// <returns>The localizer index pattern result.</returns>
    [GeneratedRegex(@"@[A-Za-z_]\w*Localizer\[""([^""]*)""\]")]
    private static partial Regex LocalizerIndexPattern();
}
