//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The server-backed database vendors demonstrated by the reference application. Each value maps to
/// one production database provider and to the sample-owned initialization rules of that vendor.
/// </summary>
public enum ReferenceServerVendor {
    /// <summary>Microsoft SQL Server (<c>AddSqlServerLocalization</c>).</summary>
    SqlServer = 0,
    /// <summary>PostgreSQL (<c>AddPostgreSqlLocalization</c>).</summary>
    PostgreSql = 1,
    /// <summary>MySQL (<c>AddMySqlLocalization</c>).</summary>
    MySql = 2,
}
