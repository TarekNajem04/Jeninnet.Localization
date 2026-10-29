//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The well-known, stable localization keys used throughout the reference application.
/// </summary>
public static class ReferenceKeys {
    /// <summary>Localized application title.</summary>
    public const string APPLICATION_TITLE = "App.Title";
    /// <summary>Catalog page heading.</summary>
    public const string CATALOG_HEADING = "Catalog.Headings.Catalog";
    /// <summary>Book detail page heading.</summary>
    public const string DETAILS_HEADING = "Catalog.Headings.Details";
    /// <summary>Book identifier field label.</summary>
    public const string FIELD_ID = "Catalog.Fields.Id";
    /// <summary>Book title field label.</summary>
    public const string FIELD_TITLE = "Catalog.Fields.Title";
    /// <summary>Book author field label.</summary>
    public const string FIELD_AUTHOR = "Catalog.Fields.Author";
    /// <summary>Book price field label.</summary>
    public const string FIELD_PRICE = "Catalog.Fields.Price";
    /// <summary>Book description field label.</summary>
    public const string FIELD_DESCRIPTION = "Catalog.Fields.Description";
    /// <summary>Back navigation action.</summary>
    public const string BACK_ACTION = "Actions.Back";
    /// <summary>View-details action.</summary>
    public const string VIEW_DETAILS_ACTION = "Actions.ViewDetails";
    /// <summary>Add-to-order action.</summary>
    public const string ADD_TO_ORDER_ACTION = "Actions.AddToOrder";
    /// <summary>Language / culture-switch action.</summary>
    public const string SWITCH_CULTURE_ACTION = "Actions.SwitchCulture";
    /// <summary>Order-placed confirmation message ('{0}': order id, '{1}': quantity, '{2}': total).</summary>
    public const string ORDER_PLACED_MESSAGE = "Messages.OrderPlaced";
    /// <summary>Order identifier field label.</summary>
    public const string ORDER_ID_FIELD = "Orders.Fields.OrderId";
    /// <summary>Order book field label.</summary>
    public const string BOOK_ID_FIELD = "Orders.Fields.BookId";
    /// <summary>Order book field description.</summary>
    public const string BOOK_ID_DESCRIPTION = "Orders.Fields.BookIdDescription";
    /// <summary>Order quantity field label.</summary>
    public const string QUANTITY_FIELD = "Orders.Fields.Quantity";
    /// <summary>Order quantity field placeholder.</summary>
    public const string QUANTITY_PROMPT = "Orders.Fields.QuantityPrompt";
    /// <summary>Order total field label.</summary>
    public const string TOTAL_FIELD = "Orders.Fields.Total";
    /// <summary>Book selection validation message.</summary>
    public const string BOOK_REQUIRED_VALIDATION = "Orders.Validation.BookRequired";
    /// <summary>Quantity range validation message ('{0}': minimum, '{1}': maximum).</summary>
    public const string QUANTITY_RANGE_VALIDATION = "Orders.Validation.QuantityRange";
    /// <summary>Unknown-book validation message ('{0}': book identifier).</summary>
    public const string BOOK_NOT_FOUND_VALIDATION = "Orders.Validation.BookNotFound";
    /// <summary>Localized title of 'The Prophet'.</summary>
    public const string BOOK_PROPHET_TITLE = "Books.Prophet.Title";
    /// <summary>Localized description of 'The Prophet'.</summary>
    public const string BOOK_PROPHET_DESCRIPTION = "Books.Prophet.Description";
    /// <summary>Localized title of 'The Cairo Trilogy'.</summary>
    public const string BOOK_CAIRO_TRILOGY_TITLE = "Books.CairoTrilogy.Title";
    /// <summary>Localized description of 'The Cairo Trilogy'.</summary>
    public const string BOOK_CAIRO_TRILOGY_DESCRIPTION = "Books.CairoTrilogy.Description";
    /// <summary>Localized title of 'Season of Migration to the North'.</summary>
    public const string BOOK_SEASON_OF_MIGRATION_TITLE = "Books.SeasonOfMigration.Title";
    /// <summary>Localized description of 'Season of Migration to the North'.</summary>
    public const string BOOK_SEASON_OF_MIGRATION_DESCRIPTION = "Books.SeasonOfMigration.Description";
    /// <summary>Localized title of 'Arabian Love Poems'.</summary>
    public const string BOOK_ARABIAN_LOVE_POEMS_TITLE = "Books.ArabianLovePoems.Title";
    /// <summary>Localized description of 'Arabian Love Poems'.</summary>
    public const string BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION = "Books.ArabianLovePoems.Description";
    /// <summary>Present only in the default culture; demonstrates default-culture fallback.</summary>
    public const string DEMO_SHOWCASE = "Demo.Showcase";
    /// <summary>Absent from 'ar-SY' but present in 'ar' and 'en'; demonstrates parent-culture fallback.</summary>
    public const string DEMO_REGIONAL_NOTE = "Demo.RegionalNote";
    /// <summary>Present in no culture; demonstrates deterministic missing-key behavior.</summary>
    public const string DEMO_MISSING_EVERYWHERE = "Demo.MissingEverywhere";
}
