-- ============================================================================
-- LocalizationMessages — fresh-database schema (SQL Server)
-- Project : Jeninnet.Localization
-- Purpose : Create the canonical message table on a database that does not
--           have it yet.
--
-- This script is idempotent: when dbo.LocalizationMessages already exists it
-- does nothing and never drops or alters the existing table. For a database
-- that still has the pre-canonical ValidationMessages table, use the guarded
-- rename migration at ../0001_localization_messages.sql instead.
--
-- Canonical schema (FINAL_ARCHITECTURE.md §14):
--     Table   : LocalizationMessages
--     Columns : Id, MessageKey, Culture, Template
--     Keys    : PRIMARY KEY (Id), UNIQUE (MessageKey, Culture)
--
-- The case-sensitive collation on MessageKey and Culture is intentional:
-- message keys and culture names are matched ordinally by the provider. Adjust
-- the collation if your database standard requires different semantics.
-- ============================================================================

IF OBJECT_ID(N'dbo.LocalizationMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LocalizationMessages
    (
        Id         BIGINT IDENTITY(1,1) NOT NULL,
        MessageKey NVARCHAR(255) COLLATE Latin1_General_100_CS_AS NOT NULL,
        Culture    NVARCHAR(64)  COLLATE Latin1_General_100_CS_AS NOT NULL,
        Template   NVARCHAR(MAX) NOT NULL,
        CONSTRAINT PK_LocalizationMessages
            PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LocalizationMessages_MessageKey_Culture
            UNIQUE (MessageKey, Culture)
    );
END;
