//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Translation.Tms;
using Jeninnet.Localization.Translation.Tms.Crowdin;
using Jeninnet.Localization.Translation.Tms.Crowdin.Extensions;
using Jeninnet.Localization.Translation.Tms.Crowdin.Options;
using static Jeninnet.Localization.Tests.TmsCrowdinConnectorPullTests;

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Crowdin push tests: dry-run previews, live PO/XLIFF uploads, conflict reporting,
/// and validation, all through a fake <see cref="HttpMessageHandler"/>. No test
/// requires a Crowdin token or network access.
/// </summary>
[TestClass]
public class TmsCrowdinConnectorPushTests {
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

            if(path.EndsWith("/strings", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(StringsPage(
                    (1, "Same", "Same source", null),
                    (2, "Changed", "Old source", null),
                    (3, "Untouched", "Untouched source", null),
                    (4, "Fresh", "Fresh source", null))));
            }

            return Task.FromResult(JsonResponse(TranslationsPage(
                (1, "نفس", null),
                (2, "قديم", null),
                (3, "ثابت", null),
                (4, "قديم", null))));
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
        string? storageFileName = null;
        string? uploadedBody = null;
        string? uploadTarget = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var path = request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/strings", StringComparison.Ordinal)) {
                return JsonResponse(StringsPage(
                    (1, "App.Title", "Title", "Shown in the header")));
            }

            if(path.Contains("/languages/", StringComparison.Ordinal)) {
                return JsonResponse(TranslationsPage(
                    (1, "عنوان قديم", null)));
            }

            if(path.EndsWith("/storages", StringComparison.Ordinal)) {
                request.Method.Should().Be(HttpMethod.Post);
                request.Headers.Authorization!.Scheme.Should().Be("Bearer");
                if(request.Headers.TryGetValues("Crowdin-API-FileName", out var names)) {
                    storageFileName = names.First();
                }

                uploadedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                uploadedBody.Should().Contain("App.Title");
                uploadedBody.Should().Contain("عنوان جديد");
                uploadedBody.Should().Contain("Shown in the header");
                return JsonResponse("""{"data":{"id":99}}""");
            }

            request.Method.Should().Be(HttpMethod.Post);
            uploadTarget = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""{"data":{"projectId":42,"storageId":99,"fileId":7,"languageId":"ar"}}""");
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
        storageFileName.Should().Be("messages.ar.po");
        uploadTarget.Should().Contain("99");
        uploadTarget.Should().Contain("7");
        StatusOf(report, "App.Title").Should().Be(CatalogKeyStatus.Updated);
    }

    /// <summary>
    /// Verifies that push live xliff format uploads xliff.
    /// </summary>
    [TestMethod]
    public async Task Push_Live_XliffFormat_UploadsXliffAsync() {
        string? storageFileName = null;
        string? uploadedBody = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var path = request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/strings", StringComparison.Ordinal)) {
                return JsonResponse(StringsPage((1, "App.Title", "Title", null)));
            }

            if(path.Contains("/languages/", StringComparison.Ordinal)) {
                return JsonResponse(TranslationsPage());
            }

            if(path.EndsWith("/storages", StringComparison.Ordinal)) {
                if(request.Headers.TryGetValues("Crowdin-API-FileName", out var names)) {
                    storageFileName = names.First();
                }

                uploadedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return JsonResponse("""{"data":{"id":100}}""");
            }

            return JsonResponse("""{"data":{"projectId":42,"storageId":100,"fileId":7,"languageId":"ar"}}""");
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
        storageFileName.Should().Be("messages.ar.xliff");
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

            var path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(JsonResponse(path.EndsWith("/strings", StringComparison.Ordinal)
                ? StringsPage((1, "Same", "Same source", null))
                : TranslationsPage((1, "نفس", null))));
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
    /// Verifies that push missing file id throws invalid operation exception.
    /// </summary>
    [TestMethod]
    public async Task Push_MissingFileId_ThrowsInvalidOperationExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var options = new CrowdinConnectorOptions {
            ApiToken = "test-token",
            ProjectId = 42,
        };
        var connector = new CrowdinConnector(Options.Create(options), client);
        Func<Task> act = async () => await connector.PushAsync(new TmsPushRequest {
            Culture = _arabic,
            Source = new Dictionary<string, string?>(),
            Target = new Dictionary<string, string?> { ["K"] = "V" },
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that push server error throws tms connector exception.
    /// </summary>
    [TestMethod]
    public async Task Push_ServerError_ThrowsTmsConnectorExceptionAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            if(request.RequestUri!.AbsolutePath.EndsWith("/strings", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(StringsPage((1, "K", "S", null))));
            }

            if(request.RequestUri.AbsolutePath.Contains("/languages/", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(TranslationsPage()));
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
            var path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(JsonResponse(path.EndsWith("/strings", StringComparison.Ordinal)
                ? StringsPage((1, "K", "S", null))
                : TranslationsPage()));
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
        services.AddCrowdinTmsConnector(options => {
            options.ApiToken = "test-token";
            options.ProjectId = 42;
            options.FileId = 7;
        });

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITmsConnector>();
        var second = provider.GetRequiredService<ITmsConnector>();

        first.Should().BeSameAs(second, "the connector is a singleton like the translation providers");
        first.Should().BeOfType<CrowdinConnector>();
    }

    /// <summary>
    /// Verifies that dependency injection invalid options throw at registration.
    /// </summary>
    [TestMethod]
    public void DependencyInjection_InvalidOptions_ThrowAtRegistration() {
        var blankToken = new ServiceCollection();
        Action blank = () => blankToken.AddCrowdinTmsConnector(options => {
            options.ApiToken = string.Empty;
            options.ProjectId = 42;
        });

        var badProject = new ServiceCollection();
        Action project = () => badProject.AddCrowdinTmsConnector(options => {
            options.ApiToken = "test-token";
            options.ProjectId = 0;
        });

        blank.Should().Throw<ArgumentException>("the setter rejects blank tokens eagerly");
        project.Should().Throw<InvalidOperationException>("registration validates the project id eagerly");
    }

    /// <summary>
    /// Creates connector.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <returns>The connector result.</returns>
    private static CrowdinConnector CreateConnector(HttpClient client) {
        var options = new CrowdinConnectorOptions {
            ApiToken = "test-token",
            ProjectId = 42,
            FileId = 7,
        };

        return new CrowdinConnector(Options.Create(options), client);
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
