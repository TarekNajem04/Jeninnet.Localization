//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReferenceBlazor(options => {
    var section = builder.Configuration.GetSection("ReferenceLocalization");
    if(section.Exists()) {
        section.Bind(options);
    }
});

var app = builder.Build();

var supportedCultures = ReferenceCultures.SupportedCultures
    .Select(static code => new CultureInfo(code))
    .ToArray();

app.UseRequestLocalization(new RequestLocalizationOptions {
    DefaultRequestCulture = new RequestCulture(ReferenceCultures.DEFAULT_CULTURE),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = [new QueryStringRequestCultureProvider(), new CookieRequestCultureProvider()]
});

app.MapStaticAssets();

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
