//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

global using System.Collections.ObjectModel;
global using System.ComponentModel.DataAnnotations;
global using System.Data.Common;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Reflection;
global using System.Security.Cryptography;
global using System.Text;
global using System.Text.Json;
global using Jeninnet.Localization.Abstractions;
global using Jeninnet.Localization.Abstractions.Store;
global using Jeninnet.Localization.Core.Extensions;
global using Jeninnet.Localization.Integrations.DataAnnotations;
global using Jeninnet.Localization.Providers.Database.MySql.Extensions;
global using Jeninnet.Localization.Providers.Database.PostgreSQL.Extensions;
global using Jeninnet.Localization.Providers.Database.SQLite.Extensions;
global using Jeninnet.Localization.Providers.Database.SqlServer.Extensions;
global using Jeninnet.Localization.Providers.Json.Extensions;
global using Jeninnet.Localization.Providers.Memory.Extensions;
global using Jeninnet.Localization.Providers.ResX.Extensions;
global using Jeninnet.Localization.Providers.ResX.Options;
global using Jeninnet.Localization.Providers.Xml.Extensions;
global using Jeninnet.Localization.Reference.Shared.Domain;
global using Jeninnet.Localization.Reference.Shared.Localization;
global using Jeninnet.Localization.Translation.Azure;
global using Jeninnet.Localization.Translation.Azure.Extensions;
global using Jeninnet.Localization.Translation.Google;
global using Jeninnet.Localization.Translation.Google.Extensions;
global using Microsoft.Data.SqlClient;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.DependencyInjection;
global using MySql.Data.MySqlClient;
global using Npgsql;
