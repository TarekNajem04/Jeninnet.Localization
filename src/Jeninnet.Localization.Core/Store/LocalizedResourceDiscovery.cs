//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// Discovery of <see cref="LocalizedResourceAttribute"/> declarations:
/// reflects over a type or an assembly and converts every attribute (type-level and on
/// properties, fields, and methods) into a <see cref="ResourceDeclaration"/> in
/// deterministic (resource set, key, template, note) order.
/// </summary>
/// <remarks>
/// Every discovery method is <see cref="RequiresUnreferencedCodeAttribute"/>-annotated:
/// attribute-based discovery reflects over metadata that trimming may remove, so the
/// hazard propagates to callers honestly.
/// Trimmed/AOT (Ahead-of-Time) deployments use explicit <see cref="ResourceDeclaration"/> values
/// instead — the synchronizer itself carries no reflection. Discovery scans type-level
/// attributes and public declared members (properties, fields, methods).
/// </remarks>
public static class LocalizedResourceDiscovery {
    private const BindingFlags MEMBERS =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Discovers declarations across every type of one assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The declarations in deterministic order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Attribute-based declaration discovery reflects over assembly metadata that trimming may remove; pass explicit ResourceDeclaration values in trimmed/AOT deployments.")]
    public static IReadOnlyList<ResourceDeclaration> FromAssembly(Assembly assembly) {
        ArgumentNullException.ThrowIfNull(assembly);

        var declarations = new List<ResourceDeclaration>();
        foreach(var type in assembly.GetTypes()) {
            Collect(type, declarations);
        }

        return Order(declarations);
    }

    /// <summary>
    /// Discovers declarations on one type (type-level and on its declared members).
    /// </summary>
    /// <param name="type">The type to scan.</param>
    /// <returns>The declarations in deterministic order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Attribute-based declaration discovery reflects over type metadata that trimming may remove; pass explicit ResourceDeclaration values in trimmed/AOT deployments.")]
    public static IReadOnlyList<ResourceDeclaration> FromType(Type type) {
        ArgumentNullException.ThrowIfNull(type);

        var declarations = new List<ResourceDeclaration>();
        Collect(type, declarations);
        return Order(declarations);
    }

    /// <summary>
    /// Performs the collect operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="declarations">The declarations.</param>
    [RequiresUnreferencedCode("Attribute-based declaration discovery reflects over type metadata that trimming may remove; pass explicit ResourceDeclaration values in trimmed/AOT deployments.")]
    private static void Collect(Type type, List<ResourceDeclaration> declarations) {
        Add(type.GetCustomAttributes<LocalizedResourceAttribute>(inherit: false), declarations);
        foreach(var property in type.GetProperties(MEMBERS)) {
            Add(property.GetCustomAttributes<LocalizedResourceAttribute>(inherit: false), declarations);
        }

        foreach(var field in type.GetFields(MEMBERS)) {
            Add(field.GetCustomAttributes<LocalizedResourceAttribute>(inherit: false), declarations);
        }

        foreach(var method in type.GetMethods(MEMBERS)) {
            Add(method.GetCustomAttributes<LocalizedResourceAttribute>(inherit: false), declarations);
        }
    }

    /// <summary>
    /// Performs the add operation.
    /// </summary>
    /// <param name="attributes">The attributes.</param>
    /// <param name="declarations">The declarations.</param>
    private static void Add(IEnumerable<LocalizedResourceAttribute> attributes, List<ResourceDeclaration> declarations) {
        foreach(var attribute in attributes) {
            declarations.Add(attribute.ToDeclaration());
        }
    }

    /// <summary>
    /// Performs the order operation.
    /// </summary>
    /// <param name="declarations">The declarations.</param>
    /// <returns>The order sequence.</returns>
    private static List<ResourceDeclaration> Order(List<ResourceDeclaration> declarations) =>
        [.. declarations
            .OrderBy(static declaration => declaration.ResourceSet, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.Key, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.DefaultTemplate, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.Note, StringComparer.Ordinal)];
}
