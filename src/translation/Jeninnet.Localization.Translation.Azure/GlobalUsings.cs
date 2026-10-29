//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Globalization;
global using System.Net;
global using System.Net.Http.Json;
global using System.Runtime.CompilerServices;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using Jeninnet.Localization.Abstractions;
global using Jeninnet.Localization.Abstractions.Diagnostics;
global using Jeninnet.Localization.Translation.Azure.Options;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("Jeninnet.Localization.Tests")]
