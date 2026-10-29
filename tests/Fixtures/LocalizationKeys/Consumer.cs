//  fixture consumer: compiles against the generated LocalizationKeys.
// Renaming Catalog.Heading in Messages.loc.json must break this compilation.

/// <summary>
/// Represents a consumer.
/// </summary>
public static class Consumer {
    /// <summary>
    /// Gets the title key.
    /// </summary>
    public static string TitleKey => LocalizationKeys.APP_TITLE;

    /// <summary>
    /// Gets the heading key.
    /// </summary>
    public static string HeadingKey => LocalizationKeys.CATALOG_HEADING;

    /// <summary>
    /// Gets the key count.
    /// </summary>
    public static int KeyCount => LocalizationKeys.All.Length;
}
