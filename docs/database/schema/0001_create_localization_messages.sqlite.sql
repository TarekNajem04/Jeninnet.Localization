-- ============================================================================
-- LocalizationMessages — fresh-database schema (SQLite)
-- Project : Jeninnet.Localization
-- Purpose : Create the canonical message table on a database file that does
--           not have it yet.
--
-- This script is idempotent: when LocalizationMessages already exists it does
-- nothing and never drops or alters the existing table. If the database still
-- has the pre-canonical ValidationMessages table, do not run this script: see
-- ../README.md for the SQL Server rename migration and vendor limitations.
--
-- Canonical schema (FINAL_ARCHITECTURE.md §14):
--     Table   : LocalizationMessages
--     Columns : Id, MessageKey, Culture, Template
--     Keys    : PRIMARY KEY (Id), UNIQUE (MessageKey, Culture)
--
-- The explicit BINARY collation matches SQLite's default and keeps message
-- keys and culture names case-sensitive. Change it only if the application
-- deliberately wants case-insensitive matching.
-- ============================================================================

CREATE TABLE IF NOT EXISTS "LocalizationMessages"
(
    "Id"         INTEGER PRIMARY KEY AUTOINCREMENT,
    "MessageKey" TEXT COLLATE BINARY NOT NULL,
    "Culture"    TEXT COLLATE BINARY NOT NULL,
    "Template"   TEXT NOT NULL,
    CONSTRAINT "UQ_LocalizationMessages_MessageKey_Culture"
        UNIQUE ("MessageKey", "Culture")
);
