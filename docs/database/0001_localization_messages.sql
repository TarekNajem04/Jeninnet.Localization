-- ============================================================================
-- Phase 3 — LocalizationMessages schema migration
-- Project : Jeninnet.Localization
-- Purpose : Transition the legacy ValidationMessages table to the canonical
--           LocalizationMessages table and normalize its columns while
--           preserving existing localized message data.
--
-- Canonical schema (FINAL_ARCHITECTURE.md §14):
--     Table   : LocalizationMessages
--     Columns : Id, MessageKey, Culture, Template
-- Legacy schema:
--     Table   : ValidationMessages
--     Columns : Id, Key, Culture, MessageTemplate
--
-- The migration is deterministic and reviewable: each structural change is
-- guarded so that it only executes when the legacy artifact is still present
-- and the canonical artifact does not yet exist.
-- ============================================================================

BEGIN TRANSACTION;

-- 1. Rename the legacy table to the canonical name, preserving all rows.
IF OBJECT_ID(N'dbo.ValidationMessages', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.LocalizationMessages', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.ValidationMessages', N'LocalizationMessages';
END;

-- 2. Normalize Key → MessageKey.
IF COL_LENGTH(N'dbo.LocalizationMessages', N'Key') IS NOT NULL
   AND COL_LENGTH(N'dbo.LocalizationMessages', N'MessageKey') IS NULL
BEGIN
    EXEC sp_rename N'dbo.LocalizationMessages.Key', N'MessageKey', N'COLUMN';
END;

-- 3. Normalize MessageTemplate → Template.
IF COL_LENGTH(N'dbo.LocalizationMessages', N'MessageTemplate') IS NOT NULL
   AND COL_LENGTH(N'dbo.LocalizationMessages', N'Template') IS NULL
BEGIN
    EXEC sp_rename N'dbo.LocalizationMessages.MessageTemplate', N'Template', N'COLUMN';
END;

COMMIT TRANSACTION;

-- Expected result (SQL Server):
--     dbo.LocalizationMessages (
--         Id          ... PRIMARY KEY (identity-preserving),
--         MessageKey  ... ,
--         Culture     ... ,
--         Template    ... ,
--         UNIQUE (MessageKey, Culture)
--     )
--
-- Note: SQL Server automatically carries constraints/identities over with each
-- rename, so no rows are dropped. This script is SQL Server only. Other
-- vendors need equivalent vendor-specific DDL; fresh-database creation scripts
-- for all four vendors live in the schema/ folder.