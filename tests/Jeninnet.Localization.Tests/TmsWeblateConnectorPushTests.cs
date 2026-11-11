//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Translation.Tms;
using Jeninnet.Localization.Translation.Tms.Weblate;
using Jeninnet.Localization.Translation.Tms.Weblate.Extensions;
using Jeninnet.Localization.Translation.Tms.Weblate.Options;
using static Jeninnet.Localization.Tests.TmsWeblateConnectorPullTests;

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Weblate push tests: dry-run previews, live PO/XLIFF uploads, conflict reporting,
/// and validation, all through a fake <see cref="HttpMessageHandler"/>. No test
/// requires a Weblate token or network access.
/// </summary>
[TestClass]
public class TmsWeblateConnectorPushTests {
    private static readonly CultureInfo _arabic = CultureInfo.GetCultureInfo("ar");

    /// <summary>
    /// Verifies that push dry run reports without uploading.
    /// </summary>
    [TestMethod]
    public async Task Push_DryRun_ReportsWithoutUploadingAsync() {
        var uploads = 0;
        var handler = new MockHttpMessageHandler((request, _) => {
            var path = request.RequestUri!.AbsolutePath;
            if(request.Method == HttpMethod.Post) {
                uploads++;
                throw new InvalidOperationException("a dry run must not upload anything");
            }

            path.Should().Contain("/api/translations/demo/messages/ar/units/");
            return Task.FromResult(JsonResponse(UnitsPage(null,
                (1, ["Same source"], ["نفس"], "Same", null, 20),
                (2, ["Old source"], ["قديم"], "Changed", null, 20),
                (3, ["Untouched source"], ["ثابت"], "Untouched", null, 20),
                (4, ["Fresh source"], [], "Fresh", null, 0))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        var request = new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?> {
                ["Same"] = "Same source",
                ["Changed"] = "New source",
                ["Untouched"] = "Untouched source",
                ["Fresh"] = "Fresh source",
                ["New"] = "New key source",
            },
            Target = new Dictionary<string, string?> {
                ["Same"] = "نفس",
                ["Changed"] = "جديد",
                ["Untouched"] = "ثابت",
                ["Fresh"] = "جديد",
                ["New"] = "جديد",
                ["Empty"] = string.Empty,
            },
            DryRun = true,
        };

        var report = await connector.PushAsync(request, CancellationToken.None);

        report.Culture.Should().Be(_arabic);
        report.DryRun.Should().BeTrue();
        report.UploadedFileName.Should().BeNull();
        uploads.Should().Be(0);
        report.UpdatedCount.Should().Be(1);
        report.ConflictCount.Should().Be(2);
        StatusOf(report, "Same").Should().Be(CatalogKeyStatus.Unchanged);
        StatusOf(report, "Changed").Should().Be(CatalogKeyStatus.StaleSource);
        StatusOf(report, "Untouched").Should().Be(CatalogKeyStatus.Unchanged);
        StatusOf(report, "Fresh").Should().Be(CatalogKeyStatus.Updated);
        StatusOf(report, "New").Should().Be(CatalogKeyStatus.UnknownKey);
        StatusOf(report, "Empty").Should().Be(CatalogKeyStatus.Skipped);
    }

    /// <summary>
    /// Verifies that push live uploads po file.
    /// </summary>
    [TestMethod]
    public async Task Push_Live_UploadsPoFileAsync() {
        string? uploadedBody = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var path = request.RequestUri!.AbsolutePath;
            if(request.Method == HttpMethod.Get) {
                return JsonResponse(UnitsPage(null,
                    (1, ["Title"], ["عنوان قديم"], "App.Title", "Shown in the header", 20)));
            }

            request.Method.Should().Be(HttpMethod.Post);
            path.Should().EndWith("/api/translations/demo/messages/ar/file/");
            request.Headers.Authorization!.Scheme.Should().Be("Token");

            uploadedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            uploadedBody.Should().Contain("App.Title");
            uploadedBody.Should().Contain("عنوان جديد");
            uploadedBody.Should().Contain("Shown in the header");
            uploadedBody.Should().Contain("method");
            uploadedBody.Should().Contain("translate");
            return JsonResponse("""{"accepted":1,"skipped":0,"not_found":0,"total":1}""");
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        var request = new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?> { ["App.Title"] = "Title" },
            Target = new Dictionary<string, string?> { ["App.Title"] = "عنوان جديد" },
            Notes = new Dictionary<string, string> { ["App.Title"] = "Shown in the header" },
        };

        var report = await connector.PushAsync(request, CancellationToken.None);

        report.DryRun.Should().BeFalse();
        report.UpdatedCount.Should().Be(1);
        report.ConflictCount.Should().Be(0);
        report.UploadedFileName.Should().Be("messages.ar.po");
        StatusOf(report, "App.Title").Should().Be(CatalogKeyStatus.Updated);
    }

    /// <summary>
    /// Verifies that push live xliff format uploads xliff.
    /// </summary>
    [TestMethod]
    public async Task Push_Live_XliffFormat_UploadsXliffAsync() {
        string? uploadedBody = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var path = request.RequestUri!.AbsolutePath;
            if(request.Method == HttpMethod.Get) {
                return JsonResponse(UnitsPage(null,
                    (1, ["Title"], [], "App.Title", null, 0)));
            }

            path.Should().EndWith("/api/translations/demo/messages/ar/file/");
            uploadedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""{"accepted":1,"skipped":0,"not_found":0,"total":1}""");
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        var request = new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?> { ["App.Title"] = "Title" },
            Target = new Dictionary<string, string?> { ["App.Title"] = "عنوان جديد" },
            Format = TmsCatalogFormat.Xliff,
        };

        var report = await connector.PushAsync(request, CancellationToken.None);

        report.UploadedFileName.Should().Be("messages.ar.xliff");
        uploadedBody.Should().Contain("App.Title");
        uploadedBody.Should().Contain("عنوان جديد");
    }

    /// <summary>
    /// Verifies that push no changes uploads nothing.
    /// </summary>
    [TestMethod]
    public async Task Push_NoChanges_UploadsNothingAsync() {
        var uploads = 0;
        var handler = new MockHttpMessageHandler((request, _) => {
            if(request.Method == HttpMethod.Post) {
                uploads++;
            }

            return Task.FromResult(JsonResponse(UnitsPage(null,
                (1, ["Same source"], ["نفس"], "Same", null, 20))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        var report = await connector.PushAsync(new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?> { ["Same"] = "Same source" },
            Target = new Dictionary<string, string?> { ["Same"] = "نفس" },
        }, CancellationToken.None);

        report.UpdatedCount.Should().Be(0);
        report.UploadedFileName.Should().BeNull();
        uploads.Should().Be(0);
    }

    /// <summary>
    /// Verifies that push plural units report unsupported key.
    /// </summary>
    [TestMethod]
    public async Task Push_PluralUnits_ReportUnsupportedKeyAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            return Task.FromResult(JsonResponse(UnitsPage(null,
                (1, ["Apple", "Apples"], ["Pomme", "Pommes"], "Apple", null, 20))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        var report = await connector.PushAsync(new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?> { ["Apple"] = "Apple" },
            Target = new Dictionary<string, string?> { ["Apple"] = "تفاحة" },
        }, CancellationToken.None);

        report.UpdatedCount.Should().Be(0);
        report.UploadedFileName.Should().BeNull();
        StatusOf(report, "Apple").Should().Be(CatalogKeyStatus.UnsupportedKey);
    }

    /// <summary>
    /// Verifies that push server error throws tms connector exception.
    /// </summary>
    [TestMethod]
    public async Task Push_ServerError_ThrowsTmsConnectorExceptionAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            if(request.RequestUri!.AbsolutePath.Contains("/units/", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(UnitsPage(null,
                    (1, ["S"], [], "K", null, 0))));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) {
                Content = new StringContent("bad token scope"),
            });
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        Func<Task> act = async () => await connector.PushAsync(new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?> { ["K"] = "S" },
            Target = new Dictionary<string, string?> { ["K"] = "V" },
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<TmsConnectorException>();
        exception.Which.Message.Should().Contain("403");
        exception.Which.Message.Should().NotContain("test-token");
    }

    /// <summary>
    /// Verifies that push null request throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task Push_NullRequest_ThrowsArgumentNullExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var connector = CreateConnector(client);
        Func<Task> act = async () => await connector.PushAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that push unsupported format throws argument out of range exception.
    /// </summary>
    [TestMethod]
    public async Task Push_UnsupportedFormat_ThrowsArgumentOutOfRangeExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var connector = CreateConnector(client);
        Func<Task> act = async () => await connector.PushAsync(new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?>(),
            Target = new Dictionary<string, string?>(),
            Format = (TmsCatalogFormat)99,
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that push xliff missing source reports unsupported key.
    /// </summary>
    [TestMethod]
    public async Task Push_XliffMissingSource_ReportsUnsupportedKeyAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            return Task.FromResult(JsonResponse(UnitsPage(null,
                (1, ["S"], [], "K", null, 0))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        var report = await connector.PushAsync(new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?>(),
            Target = new Dictionary<string, string?> { ["K"] = "V" },
            Format = TmsCatalogFormat.Xliff,
        }, CancellationToken.None);

        report.UpdatedCount.Should().Be(0);
        report.UploadedFileName.Should().BeNull();
        StatusOf(report, "K").Should().Be(CatalogKeyStatus.UnsupportedKey);
    }

    /// <summary>
    /// Verifies that dependency injection registers single connector.
    /// </summary>
    [TestMethod]
    public void DependencyInjection_RegistersSingleConnector() {
        var services = new ServiceCollection();
        services.AddWeblateTmsConnector(options => {
            options.ApiToken = "test-token";
            options.Endpoint = "https://weblate.example";
            options.Project = "demo";
            options.Component = "messages";
        });

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITmsConnector>();
        var second = provider.GetRequiredService<ITmsConnector>();

        first.Should().BeSameAs(second, "the connector is a singleton like the translation providers");
        first.Should().BeOfType<WeblateConnector>();
    }

    /// <summary>
    /// Verifies that dependency injection invalid options throw at registration.
    /// </summary>
    [TestMethod]
    public void DependencyInjection_InvalidOptions_ThrowAtRegistration() {
        var blankToken = new ServiceCollection();
        Action blank = () => blankToken.AddWeblateTmsConnector(options => {
            options.ApiToken = string.Empty;
            options.Endpoint = "https://weblate.example";
            options.Project = "demo";
            options.Component = "messages";
        });

        var blankComponent = new ServiceCollection();
        Action component = () => blankComponent.AddWeblateTmsConnector(options => {
            options.ApiToken = "test-token";
            options.Endpoint = "https://weblate.example";
            options.Project = "demo";
            options.Component = string.Empty;
        });

        blank.Should().Throw<ArgumentException>("the setter rejects blank tokens eagerly");
        component.Should().Throw<InvalidOperationException>("registration validates the component slug eagerly");
    }

    /// <summary>
    /// Creates connector.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <returns>The connector result.</returns>
    private static WeblateConnector CreateConnector(HttpClient client) {
        var options = new WeblateConnectorOptions {
            ApiToken = "test-token",
            Endpoint = "https://weblate.example",
            Project = "demo",
            Component = "messages",
        };

        return new WeblateConnector(Options.Create(options), client);
    }

    /// <summary>
    /// Gets the status of one key in the report.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="key">The key.</param>
    /// <returns>The status.</returns>
    private static CatalogKeyStatus StatusOf(TmsPushReport report, string key) =>
        report.Entries.First(entry => entry.Key == key).Status;

    /// <summary>
    /// Handles stub.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(new InvalidOperationException("must not be called"));
    }

    /// <summary>
    /// Handles mock http message.
    /// </summary>
    /// <param name="sendAsync">The send async.</param>
    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _sendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
