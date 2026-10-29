/// <summary>
/// Fixture consumer that resolves generated sample keys through a dynamic localizer.
/// </summary>
public static class Consumer {
    /// <summary>
    /// Gets the application title key resolved through the generated sample keys.
    /// </summary>
    public static string Title => SampleKeys.APP_TITLE;

    /// <summary>
    /// Resolves the cart-add label through the supplied localizer.
    /// </summary>
    /// <param name="localizer">The localizer used to resolve the cart-add key.</param>
    /// <returns>The localized cart-add label.</returns>
    public static string Add(dynamic localizer) => localizer["Cart.Add"];
}