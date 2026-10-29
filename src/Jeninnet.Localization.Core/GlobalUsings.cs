//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

global using System.Collections.Concurrent;
global using System.ComponentModel.DataAnnotations;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Reflection;
global using System.Resources;
global using System.Text;
global using System.Text.Encodings.Web;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using Jeninnet.Localization.Abstractions;
global using Jeninnet.Localization.Abstractions.Diagnostics;
global using Jeninnet.Localization.Abstractions.Options;
global using Jeninnet.Localization.Abstractions.Store;
global using Jeninnet.Localization.Core.Localization;
global using Jeninnet.Localization.Core.Store;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Localization;
global using Microsoft.Extensions.Logging;
