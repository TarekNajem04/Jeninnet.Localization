//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Public API surface verification for every packable project (R26).
/// The committed baselines under <c>ApiBaselines</c> are compared, per packable assembly,
/// against the built assembly's exported public surface. Any accidental public API
/// addition, removal, or signature change fails the test with an actionable diff.
/// The mechanism is deterministic, offline, and CI-safe (runs inside the existing
/// <c>dotnet test</c> verification model) and uses no third-party tooling.
/// </summary>
[TestClass]
public class PublicApiSurfaceTests {
    /// <summary>
    /// The 33 cross-platform packable library assemblies (IsPackable=true, Architecture package inventory
    /// plus 's Blazor, 's JavaScript, 's Diagnostics integrations,
    /// 's key generator, 's parity tool, 's pseudo provider, 's
    /// localizability analyzer, 's request-localization bridge, and the /06/07
    /// interchange family contract home, CSV, ResX, and PO (Portable Object) interchanges). The MAUI\n    /// and WinUI assemblies are validated by the Windows host test project.
    /// </summary>
    private static readonly string[] _packableAssemblies = [
        "Jeninnet.Localization.Abstractions",
        "Jeninnet.Localization.Core",
        "Jeninnet.Localization.Storage",
        "Jeninnet.Localization.Integrations.Avalonia",
        "Jeninnet.Localization.Integrations.Blazor",
        "Jeninnet.Localization.Integrations.DataAnnotations",
        "Jeninnet.Localization.Integrations.Diagnostics",
        "Jeninnet.Localization.Integrations.JavaScript",
        "Jeninnet.Localization.Integrations.Mvc.Validation",
        "Jeninnet.Localization.Integrations.RequestLocalization",
        "Jeninnet.Localization.Interchange.Abstractions",
        "Jeninnet.Localization.Interchange.Csv",
        "Jeninnet.Localization.Interchange.Po",
        "Jeninnet.Localization.Interchange.ResX",
        "Jeninnet.Localization.Interchange.Xliff",
        "Jeninnet.Localization.Providers.Database.Abstractions",
        "Jeninnet.Localization.Providers.Database.EntityFrameworkCore",
        "Jeninnet.Localization.Providers.Database.MySql",
        "Jeninnet.Localization.Providers.Database.PostgreSQL",
        "Jeninnet.Localization.Providers.Database.SQLite",
        "Jeninnet.Localization.Providers.Database.SqlServer",
        "Jeninnet.Localization.Providers.Json",
        "Jeninnet.Localization.Providers.Memory",
        "Jeninnet.Localization.Providers.Po",
        "Jeninnet.Localization.Providers.ResX",
        "Jeninnet.Localization.Providers.Satellite",
        "Jeninnet.Localization.Providers.Xml",
        "Jeninnet.Localization.Translation.Azure",
        "Jeninnet.Localization.Translation.Google",
        "Jeninnet.Localization.Generators.Keys",
        "Jeninnet.Localization.Tools.Keys",
        "Jeninnet.Localization.Providers.Pseudo",
        "Jeninnet.Localization.Analyzers.Localizability",
    ];

    /// <summary>
    /// Verifies that public api surface matches committed baselines.
    /// </summary>
    [TestMethod]
    public void PublicApiSurface_MatchesCommittedBaselines() {
        var failures = new List<string>();

        foreach(var assemblyName in _packableAssemblies) {
            var baselinePath = Path.Combine(BaselineDirectory, assemblyName + ".Api.txt");
            if(!File.Exists(baselinePath)) {
                failures.Add($"{assemblyName}: missing committed baseline file (a packable project must participate in API validation)");
                continue;
            }

            var expected = File.ReadAllLines(baselinePath)
                .Where(static line => !line.StartsWith("//", StringComparison.Ordinal))
                .ToArray();
            var actual = DumpAssembly(assemblyName);

            var differences = DiffLines(expected, actual);
            if(differences.Count > 0) {
                failures.Add($"{assemblyName}: {differences.Count} public API difference(s)" + Environment.NewLine + string.Join(Environment.NewLine, differences.Take(40)));
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine + Environment.NewLine, failures));
    }

    /// <summary>
    /// Verifies that public api baselines regenerate when is requested.
    /// </summary>
    [TestMethod]
    public void PublicApiBaselines_RegenerateWhenRequested() {
        if(Environment.GetEnvironmentVariable("JENINNET_API_REGEN") != "1") {
            Assert.Inconclusive("set JENINNET_API_REGEN=1 to regenerate the API baseline files");
            return;
        }

        foreach(var assemblyName in _packableAssemblies) {
            var lines = new[] { $"// Public API baseline for {assemblyName} (net10.0)." }
                .Concat(DumpAssembly(assemblyName));
            Directory.CreateDirectory(BaselineDirectory);
            File.WriteAllLines(Path.Combine(BaselineDirectory, assemblyName + ".Api.txt"), lines);
        }
    }

    /// <summary>
    /// Gets the baseline directory.
    /// </summary>
    private static string BaselineDirectory {
        get {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))) {
                directory = directory.Parent;
            }

            if(directory is null) {
                throw new InvalidOperationException("repository root could not be located from the test output directory");
            }

            return Path.Combine(directory.FullName, "tests", "Jeninnet.Localization.Tests", "ApiBaselines");
        }
    }

    /// <summary>
    /// Performs the dump assembly operation.
    /// </summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The dump assembly sequence.</returns>
    private static string[] DumpAssembly(string assemblyName) {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));
        var lines = new List<string>();

        foreach(var type in assembly.GetExportedTypes()
                     .OrderBy(static type => type.FullName, StringComparer.Ordinal)) {
            lines.AddRange(DumpType(type));
        }

        return [.. lines];
    }

    /// <summary>
    /// Performs the dump type operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The dump type sequence.</returns>
    private static IEnumerable<string> DumpType(Type type) {
        var header = "type " + FormatType(type, includeGenericParameters: true);
        var members = DumpConstructors(type)
            .Concat(DumpMethods(type))
            .Concat(DumpProperties(type))
            .Concat(DumpEvents(type))
            .Concat(DumpFields(type))
            .OrderBy(static line => line, StringComparer.Ordinal);

        return [header, .. members];
    }

    /// <summary>
    /// Performs the dump constructors operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The dump constructors sequence.</returns>
    private static IEnumerable<string> DumpConstructors(Type type) {
        foreach(var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            yield return "  ctor(" + FormatParameters(constructor.GetParameters()) + ")";
        }
    }

    /// <summary>
    /// Performs the dump methods operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The dump methods sequence.</returns>
    private static IEnumerable<string> DumpMethods(Type type) {
        foreach(var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName)) {
            var modifiers = string.Join(' ', MethodModifiers(method));
            var name = FormatTypeName(method.Name, method.GetGenericArguments());
            yield return $"  method {modifiers}{(modifiers.Length > 0 ? " " : "")}{FormatType(method.ReturnType)} {name}({FormatParameters(method.GetParameters())})";
        }
    }

    /// <summary>
    /// Performs the dump properties operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The dump properties sequence.</returns>
    private static IEnumerable<string> DumpProperties(Type type) {
        foreach(var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            var accessors = string.Join("; ", new[] {
                property.GetMethod is { IsPublic: true } ? "get" : null,
                property.SetMethod is { IsPublic: true } ? "set" : null,
            }.Where(static accessor => accessor is not null));
            var modifiers = property.GetMethod is { IsStatic: true } || property.SetMethod is { IsStatic: true } ? "static " : string.Empty;
            var indexer = property.GetIndexParameters() is { Length: > 0 } indexParameters
                ? "[" + FormatParameters(indexParameters) + "]"
                : string.Empty;
            yield return $"  property {modifiers}{FormatType(property.PropertyType)} {property.Name}{indexer} {{ {accessors}; }}";
        }
    }

    /// <summary>
    /// Performs the dump events operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The dump events sequence.</returns>
    private static IEnumerable<string> DumpEvents(Type type) {
        foreach(var @event in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            yield return $"  event {FormatType(@event.EventHandlerType!)} {@event.Name}";
        }
    }

    /// <summary>
    /// Performs the dump fields operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The dump fields sequence.</returns>
    private static IEnumerable<string> DumpFields(Type type) {
        foreach(var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            var modifiers = field.IsStatic ? "static " : string.Empty;
            if(field.IsLiteral) {
                modifiers = "const ";
            } else if(field.IsInitOnly) {
                modifiers += "readonly ";
            }

            yield return $"  field {modifiers}{FormatType(field.FieldType)} {field.Name}";
        }
    }

    /// <summary>
    /// Performs the method modifiers operation.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>The method modifiers sequence.</returns>
    private static IEnumerable<string> MethodModifiers(MethodInfo method) {
        if(method.IsStatic) {
            yield return "static";
        }

        if(method.IsAbstract) {
            yield return "abstract";
        } else if(method.IsVirtual && !method.IsFinal) {
            yield return "virtual";
        } else if(method.IsVirtual && method.IsFinal) {
            yield return "override";
        }
    }

    /// <summary>
    /// Formats parameters.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The parameters text.</returns>
    private static string FormatParameters(IReadOnlyList<ParameterInfo> parameters) =>
        string.Join(", ", parameters.Select(static parameter => FormatType(parameter.ParameterType)));

    /// <summary>
    /// Formats type name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="genericArguments">The generic arguments.</param>
    /// <returns>The type name text.</returns>
    private static string FormatTypeName(string name, Type[] genericArguments) {
        if(genericArguments.Length == 0) {
            return name;
        }

        return name + "<" + string.Join(",", genericArguments.Select(static argument => argument.Name)) + ">";
    }

    /// <summary>
    /// Formats type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="includeGenericParameters">The include generic parameters.</param>
    /// <returns>The type text.</returns>
    private static string FormatType(Type type, bool includeGenericParameters = false) {
        if(type.IsGenericParameter) {
            return type.Name;
        }

        if(type.IsArray) {
            return FormatType(type.GetElementType()!) + "[]";
        }

        if(type.IsByRef) {
            return FormatType(type.GetElementType()!) + "&";
        }

        if(type.IsGenericType) {
            var fullName = type.GetGenericTypeDefinition().FullName ?? type.GetGenericTypeDefinition().Name;
            var backtick = fullName.IndexOf('`');
            var baseName = backtick >= 0 ? fullName[..backtick] : fullName;
            var arguments = includeGenericParameters
                ? type.GetGenericArguments().Select(static argument => argument.Name)
                : type.GetGenericArguments().Select(static argument => FormatType(argument));
            return baseName + "<" + string.Join(",", arguments) + ">";
        }

        return type.FullName ?? type.Name;
    }

    /// <summary>
    /// Performs the diff lines operation.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns>The diff lines sequence.</returns>
    private static List<string> DiffLines(string[] expected, string[] actual) {
        var expectedCounts = CountByLine(expected);
        var actualCounts = CountByLine(actual);
        var added = new List<string>();
        var removed = new List<string>();

        foreach(var (line, count) in actualCounts) {
            var expectedCount = expectedCounts.TryGetValue(line, out var existing) ? existing : 0;
            for(var index = 0; index < Math.Max(0, count - expectedCount); index++) {
                added.Add(line);
            }
        }

        foreach(var (line, count) in expectedCounts) {
            var actualCount = actualCounts.TryGetValue(line, out var existing) ? existing : 0;
            for(var index = 0; index < Math.Max(0, count - actualCount); index++) {
                removed.Add(line);
            }
        }

        var differences = added.Select(static line => "+ " + line)
            .Concat(removed.Select(static line => "- " + line))
            .OrderBy(static line => line, StringComparer.Ordinal)
            .ToList();
        if(differences.Count > 0) {
            differences.Insert(0, "(+ = in built assembly, not in baseline; - = in baseline, missing from built assembly)");
        }

        return differences;
    }

    /// <summary>
    /// Performs the count by line operation.
    /// </summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The count by line result.</returns>
    private static Dictionary<string, int> CountByLine(IEnumerable<string> lines) {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach(var line in lines) {
            counts.TryGetValue(line, out var count);
            counts[line] = count + 1;
        }

        return counts;
    }
}
