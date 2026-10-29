//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Docs;

/// <summary>
/// Compile-only copies of documentation examples that cannot run in a hermetic check (file
/// registration, database registration, the store bridge, and a DataAnnotations model).
/// Each member names the documentation file it mirrors; keep both in sync when the API changes.
/// </summary>
public static class CompileOnlySnippets {
    /// <summary>Mirrors docs/en/01-getting-started/02-installation.md (Memory registration).</summary>
    public static void RegisterMemoryProvider() {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddMemoryLocalization(new Dictionary<string, string> {
            ["Greeting"] = "Hello, {name}!"
        });
    }

    /// <summary>Mirrors docs/en/02-guides/01-choosing-a-provider.md (JSON file registration).</summary>
    public static void RegisterJsonProvider() {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddJsonLocalization(Path.Combine(AppContext.BaseDirectory, "Localization", "en.json"));
    }

    /// <summary>Mirrors docs/en/02-guides/01-choosing-a-provider.md (XML file registration).</summary>
    public static void RegisterXmlProvider() {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddXmlLocalization(Path.Combine(AppContext.BaseDirectory, "Localization", "en.xml"));
    }

    /// <summary>Mirrors docs/en/02-guides/01-choosing-a-provider.md (ResX registration).</summary>
    public static void RegisterResXProvider() {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddResXLocalization("Docs.Resources", typeof(CompileOnlySnippets).Assembly);
    }

    /// <summary>Mirrors docs/en/02-guides/01-choosing-a-provider.md (PO (Portable Object) registration).</summary>
    public static void RegisterPoProvider() {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddPoLocalization(Path.Combine(AppContext.BaseDirectory, "Localization", "en.po"));
    }

    /// <summary>Mirrors docs/en/02-guides/01-choosing-a-provider.md (SQLite registration).</summary>
    public static void RegisterSqliteProvider() {
        var services = new ServiceCollection();
        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddSqliteLocalization("Data Source=docs-snippets.db");
    }

    /// <summary>Mirrors docs/en/02-guides/06-resource-sets-and-store.md (EF store registration).</summary>
    public static void RegisterEntityFrameworkStore() {
        var services = new ServiceCollection();
        JeninnetLocalizationExtensions.AddLocalization(services);
        services.AddEntityFrameworkLocalizationStore(
            "Server=localhost;Database=Docs;Trusted_Connection=True;TrustServerCertificate=True;");
    }

    /// <summary>Mirrors docs/en/03-reference/01-core-api.md (store-backed IStringLocalizer bridge).</summary>
    public static void ResolveStringLocalizerBridge() {
        var services = new ServiceCollection();
        JeninnetLocalizationExtensions.AddLocalization(services);
        services.AddEntityFrameworkLocalizationStore(
            "Server=localhost;Database=Docs;Trusted_Connection=True;TrustServerCertificate=True;");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();
        var localizer = factory.Create("Bookstore", "Docs");
        _ = localizer["App.Title"];
    }

    /// <summary>Mirrors the DataAnnotations integration documentation.</summary>
    public sealed class CheckoutModel {
        /// <summary>Gets or sets the customer name; the localized required attribute enforces a non-empty value.</summary>
        [LocalizedRequired("Validation.Required")]
        public string Name { get; set; } = string.Empty;
    }
}
