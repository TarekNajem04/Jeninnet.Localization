//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Hosts.Desktop.Tests;

/// <summary>
/// Verifies the headless BookStore interaction paths the desktop hosts exercise: the shared order
/// service, localized DataAnnotations validation through a DI-backed <see cref="ValidationContext"/>,
/// and culture-switchable message localization through every desktop composition.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class DesktopHostOrderBehaviourTests {
    /// <summary>
    /// Verifies that placing an order through the shared service returns the canonical confirmation.
    /// </summary>
    [TestMethod]
    public void PlacingAnOrder_ThroughTheSharedService_ReturnsTheCanonicalConfirmation() {
        var result = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-001", Quantity = 2 });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Order);
        Assert.HasCount(1, result.Order.Items);
        Assert.AreEqual("book-001", result.Order.Items[0].BookId);
        Assert.AreEqual(2, result.Order.Items[0].Quantity);
        Assert.AreEqual(39.98m, result.Order.Total);
    }

    /// <summary>
    /// Verifies that shared validation model localizes through the host validation context.
    /// </summary>
    [TestMethod]
    public void SharedValidationModel_LocalizesThroughTheHostValidationContext() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
        using var services = Compose(new ServiceCollection(), "WinForms", ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentCulture = new CultureInfo("ar");
            CultureInfo.CurrentUICulture = new CultureInfo("ar");

            var request = new CreateOrderRequest { BookId = "book-001", Quantity = 200 };
            var results = new List<ValidationResult>();
            var validationContext = new ValidationContext(request, services, null);

            var isValid = Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true);

            Assert.IsFalse(isValid);
            Assert.HasCount(1, results);
            Assert.IsNotNull(results[0].ErrorMessage);
            var expected = string.Format(
                CultureInfo.CurrentCulture,
                LocalizationContent.GetTemplates("ar")[ReferenceKeys.QUANTITY_RANGE_VALIDATION],
                OrderLimits.MIN_QUANTITY,
                OrderLimits.MAX_QUANTITY);
            Assert.AreEqual(expected, results[0].ErrorMessage);
        }
        finally {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>
    /// Verifies that message localization is culture aware through every desktop host.
    /// </summary>
    [TestMethod]
    public async Task MessageLocalization_IsCultureAwareThroughEveryDesktopHostAsync() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            foreach(var host in _hosts) {
                await using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();
                var messageLocalizer = services.GetRequiredService<IMessageLocalizer>();

                var message = await messageLocalizer.LocalizeAsync(
                    ReferenceKeys.CATALOG_HEADING,
                    new Dictionary<string, object?>(), TestContext.CancellationToken);
                Assert.AreEqual(
                    LocalizationContent.GetTemplates("ar")[ReferenceKeys.CATALOG_HEADING],
                    message,
                    host);
            }
        }
        finally {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        Assert.AreEqual(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Verifies that order flow provides the localized confirmation through every desktop host.
    /// </summary>
    [TestMethod]
    public async Task OrderFlow_ProvidesTheLocalizedConfirmationThroughEveryDesktopHostAsync() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try {
            foreach(var host in _hosts) {
                CultureInfo.CurrentUICulture = new CultureInfo("ar");
                await using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();
                var messageLocalizer = services.GetRequiredService<IMessageLocalizer>();

                var result = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-001", Quantity = 2 });
                Assert.IsTrue(result.Succeeded, host);
                Assert.IsNotNull(result.Order, host);
                var order = result.Order;

                var localizedMessage = await messageLocalizer.LocalizeAsync(
                    ReferenceKeys.ORDER_PLACED_MESSAGE,
                    new Dictionary<string, object?> {
                        ["0"] = order.Id,
                        ["1"] = order.Items.Sum(static item => item.Quantity),
                        ["2"] = order.Total
                    }, TestContext.CancellationToken);
                Assert.IsNotNull(localizedMessage, host);
                var message = localizedMessage;

                CultureInfo.CurrentUICulture = new CultureInfo("en");
                var localizedEnglishMessage = await messageLocalizer.LocalizeAsync(
                    ReferenceKeys.ORDER_PLACED_MESSAGE,
                    new Dictionary<string, object?> {
                        ["0"] = order.Id,
                        ["1"] = order.Items.Sum(static item => item.Quantity),
                        ["2"] = order.Total
                    }, TestContext.CancellationToken);
                Assert.IsNotNull(localizedEnglishMessage, host);
                var englishMessage = localizedEnglishMessage;

                Assert.Contains(order.Id, message, host);
                Assert.AreNotEqual(englishMessage, message, host);
            }
        }
        finally {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        Assert.AreEqual(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Verifies that wpf view model culture change preserves and relocalizes the last order.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task WpfViewModel_CultureChange_PreservesAndRelocalizesTheLastOrderAsync() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
        await using var services = Compose(new ServiceCollection(), "Wpf", ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();
        var viewModel = services.GetRequiredService<BookStoreViewModel>();

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try {
            viewModel.CurrentCulture = "en";
            Assert.AreEqual(System.Windows.FlowDirection.LeftToRight, viewModel.FlowDirection);
            viewModel.SelectedBook = viewModel.Books[0];
            viewModel.Quantity = 2;
            await viewModel.PlaceOrderAsync();

            var englishMessage = viewModel.Message;
            Assert.IsNotNull(englishMessage);
            Assert.Contains("ORD-", englishMessage);

            viewModel.CurrentCulture = "ar";
            Assert.AreEqual(System.Windows.FlowDirection.RightToLeft, viewModel.FlowDirection);

            var arabicMessage = viewModel.Message;
            Assert.IsNotNull(arabicMessage);
            Assert.Contains("ORD-", arabicMessage);
            Assert.AreNotEqual(englishMessage, arabicMessage);
        }
        finally {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static readonly string[] _hosts = ["WinForms", "Wpf", "BlazorHybrid"];
    /// <summary>
    /// Performs the compose operation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="host">The host.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="contentRootPath">The content root path.</param>
    /// <returns>The compose result.</returns>
    private static IServiceCollection Compose(IServiceCollection services, string host, string provider, string contentRootPath) => host switch {
        "WinForms" => services.AddReferenceWinForms(Configure(provider, contentRootPath)),
        "Wpf" => services.AddReferenceWpf(Configure(provider, contentRootPath)),
        "BlazorHybrid" => services.AddReferenceBlazorHybrid(Configure(provider, contentRootPath)),
        _ => throw new InvalidOperationException($"Unknown reference desktop host '{host}'."),
    };

    /// <summary>
    /// Performs the configure operation.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="contentRootPath">The content root path.</param>
    /// <returns>The configure result.</returns>
    private static Action<ReferenceLocalizationOptions> Configure(string provider, string contentRootPath) {
        void ConfigureOptions(ReferenceLocalizationOptions options) {
            options.Provider = provider;
            if(!string.IsNullOrEmpty(contentRootPath)) {
                options.ContentRootPath = contentRootPath;
            }
        }

        return ConfigureOptions;
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
