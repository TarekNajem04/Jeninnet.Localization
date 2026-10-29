# Third-Party Notices

This file records the third-party notices and license attributions for
`Jeninnet.Localization`. The project itself is licensed under the MIT License; see
[`LICENSE`](LICENSE). Nothing in this file changes the terms of a third-party component.

The inventory below covers the runtime packages referenced by the published
`Jeninnet.Localization.*` packages. Package versions can vary by target framework and by release;
the exact resolved version for a given target is recorded in that target's package metadata and in
`Directory.Packages.props`. Build-only tooling and test-only packages are not redistributed unless
they are explicitly listed.

## Standards data and message syntax

### Unicode CLDR

The plural-category engine in `Jeninnet.Localization.Abstractions` contains a hand-ported,
test-vector-pinned subset of the Unicode CLDR plural rules (CLDR 48 supplemental data, Unicode 17).
CLDR data is made available by the Unicode Consortium under the
[Unicode License v3](https://www.unicode.org/license.txt).

Copyright © 1991-2025 Unicode, Inc. All rights reserved. Unicode and the Unicode Logo are
registered trademarks of Unicode, Inc. in the United States and other countries.

### ICU MessageFormat

The `IcuMessageFormatter` implements ICU MessageFormat-compatible syntax in original project code;
no ICU runtime library is redistributed. ICU is a project of the Unicode Consortium, and the ICU
name and its documentation are attributed to the [ICU project](https://icu.unicode.org/).

## Runtime dependencies

| Component | Typical version(s) | License / notice | Upstream |
|---|---|---|---|
| Avalonia, Avalonia.BuildServices, Avalonia.Remote.Protocol | 12.1.4 / 11.3.2 | MIT | [Avalonia](https://avaloniaui.net/) |
| MicroCom.Runtime | 0.11.6 | MIT | [MicroCom](https://github.com/kekekeks/MicroCom) |
| Microsoft.Bcl.Cryptography | 8.x / 9.x | MIT | [.NET](https://github.com/dotnet/runtime) |
| Microsoft.CodeAnalysis.Common, Microsoft.CodeAnalysis.CSharp, Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.Common | 5.9.0 | MIT | [Roslyn](https://github.com/dotnet/roslyn) |
| Microsoft.Data.SqlClient, Microsoft.Data.SqlClient.Extensions.Abstractions, Microsoft.Data.SqlClient.Internal.Logging, Microsoft.Data.SqlClient.SNI.runtime | 7.1.x | MIT (native SNI notices accompany the package) | [SqlClient](https://aka.ms/sqlclientproject) |
| Microsoft.Data.Sqlite, Microsoft.Data.Sqlite.Core | 10.0.13 | MIT | [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) |
| Microsoft.EntityFrameworkCore, .Abstractions, .Relational, .SqlServer, .Analyzers | 8.x / 9.x / 10.x | MIT | [EF Core](https://github.com/dotnet/efcore) |
| Microsoft.Extensions.Caching.*, Configuration.*, DependencyInjection.*, Diagnostics.*, FileProviders.*, Hosting.*, Http, Localization.*, Logging.*, Options.*, Primitives | 8.x / 9.x / 10.x | MIT | [.NET Extensions](https://github.com/dotnet/runtime) |
| Microsoft.IdentityModel.* and System.IdentityModel.Tokens.Jwt | 8.16.x | MIT | [Microsoft IdentityModel](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) |
| Microsoft.Maui.Core, Microsoft.Maui.Essentials, Microsoft.Maui.Graphics | 10.0.90 | MIT | [.NET MAUI](https://github.com/dotnet/maui) |
| Microsoft.SqlServer.Server | 1.0.x | MIT | [Microsoft.SqlServer.Server](https://www.nuget.org/packages/Microsoft.SqlServer.Server) |
| Microsoft.WindowsAppSDK and its Windows App SDK component packages | 1.8.x | Microsoft Windows App SDK license (the package carries the full `license.txt`) | [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.x | Microsoft license terms (build-time tooling) | [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) |
| System.Buffers, System.Collections.Immutable, System.Configuration.ConfigurationManager, System.Diagnostics.DiagnosticSource, System.Diagnostics.EventLog, System.Formats.Asn1, System.IO.Pipelines, System.Memory, System.Numerics.Vectors, System.Reflection.Metadata, System.Runtime.CompilerServices.Unsafe, System.Security.Cryptography.*, System.Security.Permissions, System.Text.Encoding.CodePages, System.Threading.*, System.Windows.Extensions | 4.x–10.x | MIT | [.NET](https://github.com/dotnet/runtime) |
| BouncyCastle.Cryptography | 2.6.2 | MIT | [Bouncy Castle](https://www.bouncycastle.org/) |
| Google.Protobuf | 3.35.0 | BSD-3-Clause | [Google Protobuf](https://github.com/protocolbuffers/protobuf) |
| K4os.Compression.LZ4, K4os.Compression.LZ4.Streams, K4os.Hash.xxHash | 1.3.8 / 1.0.8 | MIT | [K4os](https://github.com/MiloszKrajewski) |
| **MySql.Data** | 26.10.0 | **GPL-2.0-only WITH Universal-FOSS-exception-1.0**; Oracle Corporation | [MySQL Connector/NET](https://dev.mysql.com/downloads/) |
| Npgsql | 10.0.3 | PostgreSQL License | [Npgsql](https://github.com/npgsql/npgsql) |
| SQLitePCLRaw.bundle_e_sqlite3, .core, .lib.e_sqlite3, .provider.e_sqlite3 | 2.1.13 | Apache-2.0; the bundled SQLite library is in the public domain | [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) / [SQLite](https://www.sqlite.org/) |
| ZstdSharp.Port | 0.8.8 | MIT | [ZstdSharp](https://github.com/oleg-st/ZstdSharp) |

### Important MySQL notice

`Jeninnet.Localization.Providers.Database.MySql` depends on Oracle's `MySql.Data` package. That
package is licensed under the GNU General Public License version 2 only, with the Universal FOSS
Exception. The exception is intended to permit distribution of applications that use the package
under other licenses, including in this project's packages. Review the exact
[GPL-2.0-only WITH Universal-FOSS-exception-1.0](https://licenses.nuget.org/GPL-2.0-only%20WITH%20Universal-FOSS-exception-1.0)
terms and Oracle's package notice before redistributing or modifying the connector. The MySQL
provider is optional; hosts that do not need MySQL do not reference that package.

## License references

- MIT: <https://opensource.org/license/mit>
- BSD-3-Clause: <https://opensource.org/license/bsd-3-clause>
- Apache-2.0: <https://www.apache.org/licenses/LICENSE-2.0>
- PostgreSQL License: <https://www.postgresql.org/about/licence/>
- Unicode License v3: <https://www.unicode.org/license.txt>
- GNU GPL version 2: <https://www.gnu.org/licenses/old-licenses/gpl-2.0.html>
- Universal FOSS Exception: <https://oss.oracle.com/licenses/universal-foss-exception/>
- Microsoft Windows App SDK license: see the `license.txt` in the `Microsoft.WindowsAppSDK` package.

## Changes

When a dependency is added, removed, or relicensed, update this file in the same change. The
package validation tests require every published package to contain this notice as
`THIRD-PARTY-NOTICES.md`.
