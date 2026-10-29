//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

global using System.ComponentModel.DataAnnotations;
global using System.Data.Common;
global using System.Globalization;
global using System.Reflection;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using System.Xml.Linq;
global using Jeninnet.Localization.Abstractions;
global using Jeninnet.Localization.Abstractions.Store;
global using Jeninnet.Localization.Integrations.DataAnnotations;
global using Jeninnet.Localization.Providers.Database.MySql;
global using Jeninnet.Localization.Providers.Database.MySql.Options;
global using Jeninnet.Localization.Providers.Database.PostgreSQL;
global using Jeninnet.Localization.Providers.Database.PostgreSQL.Options;
global using Jeninnet.Localization.Providers.Database.SQLite;
global using Jeninnet.Localization.Providers.Database.SQLite.Options;
global using Jeninnet.Localization.Providers.Database.SqlServer;
global using Jeninnet.Localization.Providers.Database.SqlServer.Options;
global using Jeninnet.Localization.Providers.Memory.Extensions;
global using Jeninnet.Localization.Providers.ResX;
global using Jeninnet.Localization.Providers.ResX.Options;
global using Jeninnet.Localization.Reference.Shared.Application;
global using Jeninnet.Localization.Reference.Shared.Domain;
global using Jeninnet.Localization.Reference.Shared.Localization;
global using Jeninnet.Localization.Translation.Azure;
global using Jeninnet.Localization.Translation.Google;
global using Microsoft.Data.SqlClient;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Localization;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using MySql.Data.MySqlClient;
global using Npgsql;
