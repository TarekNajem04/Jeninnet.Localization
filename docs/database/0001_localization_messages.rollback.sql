-- ============================================================================
-- Phase 3 — LocalizationMessages schema migration (rollback path)
-- Project : Jeninnet.Localization
-- Purpose : Reverse the Phase 3 forward migration documented in
--           0001_localization_messages.sql and restore the legacy
--           ValidationMessages table and its original column names while
--           preserving all rows.
--
-- Canonical schema (FINAL_ARCHITECTURE.md §14):
--     Table   : LocalizationMessages
--     Columns : Id, MessageKey, Culture, Template
-- Legacy schema:
--     Table   : ValidationMessages
--     Columns : Id, Key, Culture, MessageTemplate
--
-- Every change is guarded and non-destructive: a rename is executed only when
-- the canonical artifact is still present and the legacy artifact does not yet
-- exist, and SQL Server carries constraints/identities over with each rename.
-- Run this script only to undo the forward migration, and only when the
-- forward migration has already been applied to the same database.
--
-- No live database migration is authorized by Implementation-03; this script
-- is a documented, reviewable rollback vehicle for consumers.
-- ============================================================================

BEGIN TRANSACTION;

-- 1. Normalize Template → MessageTemplate.
IF COL_LENGTH(N'dbo.LocalizationMessages', N'Template') IS NOT NULL
   AND COL_LENGTH(N'dbo.LocalizationMessages', N'MessageTemplate') IS NULL
BEGIN
    EXEC sp_rename N'dbo.LocalizationMessages.Template', N'MessageTemplate', N'COLUMN';
END;

-- 2. Normalize MessageKey → Key.
IF COL_LENGTH(N'dbo.LocalizationMessages', N'MessageKey') IS NOT NULL
   AND COL_LENGTH(N'dbo.LocalizationMessages', N'Key') IS NULL
BEGIN
    EXEC sp_rename N'dbo.LocalizationMessages.MessageKey', N'Key', N'COLUMN';
END;

-- 3. Rename the canonical table back to the legacy name.
IF OBJECT_ID(N'dbo.LocalizationMessages', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.ValidationMessages', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.LocalizationMessages', N'ValidationMessages';
END;

COMMIT TRANSACTION;

-- Expected result (SQL Server):
--     dbo.ValidationMessages (
--         Id              ... PRIMARY KEY (identity-preserving),
--         Key             ... ,
--         Culture         ... ,
--         MessageTemplate ... ,
--         UNIQUE (Key, Culture)
--     )
--
-- Note: renames are executed in inverse order of the forward migration so the
-- final state passes every guard of 0001_localization_messages.sql and the
-- forward migration can be re-applied to this database afterwards.
--