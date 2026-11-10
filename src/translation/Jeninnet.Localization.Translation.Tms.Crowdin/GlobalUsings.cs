//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//


global using System.Globalization;
global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Runtime.CompilerServices;
global using System.Text;
global using System.Text.Json.Serialization;
global using Jeninnet.Localization.Abstractions;
global using Jeninnet.Localization.Interchange.Abstractions;
global using Jeninnet.Localization.Interchange.Po;
global using Jeninnet.Localization.Interchange.Xliff;
global using Jeninnet.Localization.Translation.Tms;
global using Jeninnet.Localization.Translation.Tms.Crowdin.Options;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("Jeninnet.Localization.Tests")]
