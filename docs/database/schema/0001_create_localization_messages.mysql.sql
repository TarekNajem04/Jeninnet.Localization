-- ============================================================================
-- LocalizationMessages — fresh-database schema (MySQL)
-- Project : Jeninnet.Localization
-- Purpose : Create the canonical message table on a database that does not
--           have it yet.
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
-- Run the script against the target database (USE <database> first). The
-- utf8mb4_bin collation is intentional: message keys and culture names are
-- matched ordinally and must support Arabic text. Adjust it only if your
-- database standard requires different semantics.
--
-- The composite unique key is 319 utf8mb4 characters (1276 bytes). Modern
-- InnoDB (MySQL 5.7+ with the default DYNAMIC row format and large-prefix
-- support) accommodates it under the 3072-byte index limit.
-- ============================================================================

CREATE TABLE IF NOT EXISTS `LocalizationMessages`
(
    `Id`         BIGINT       NOT NULL AUTO_INCREMENT,
    `MessageKey` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `Culture`    VARCHAR(64)  CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `Template`   TEXT         NOT NULL,
    CONSTRAINT `PK_LocalizationMessages`
        PRIMARY KEY (`Id`),
    CONSTRAINT `UQ_LocalizationMessages_MessageKey_Culture`
        UNIQUE (`MessageKey`, `Culture`)
)
ENGINE = InnoDB
DEFAULT CHARSET = utf8mb4;
