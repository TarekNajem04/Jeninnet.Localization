//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Globalization;
global using System.Runtime.CompilerServices;
global using System.Text;
global using Jeninnet.Localization.Abstractions;
global using Jeninnet.Localization.Providers.Caching.Options;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("Jeninnet.Localization.Tests")]
