-- CONCEPT: SQL Server stored procedure (usp_AccountSummary)
-- Returns the aggregates required by AccountSummaryResponse in FOUR result sets:
--   1) Scalars  : TotalAccounts, TotalBalance, ActiveAccounts
--   2) ByType   : AccountType, Cnt
--   3) ByPriv   : Privilege,   Cnt
--   4) Recent   : account_number, name, account_type  (top 5 newest)
-- Idempotent: CREATE OR ALTER lets startup re-run the script safely.
CREATE OR ALTER PROCEDURE usp_AccountSummary
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: headline scalars
    SELECT
        COUNT(*)                                       AS TotalAccounts,
        ISNULL(SUM(balance), 0)                        AS TotalBalance,
        ISNULL(SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END), 0) AS ActiveAccounts
    FROM accounts;

    -- Result set 2: breakdown by account type
    SELECT account_type AS AccountType, COUNT(*) AS Cnt
    FROM accounts
    GROUP BY account_type;

    -- Result set 3: breakdown by privilege
    SELECT privilege AS Privilege, COUNT(*) AS Cnt
    FROM accounts
    GROUP BY privilege;

    -- Result set 4: 5 most-recently created accounts
    SELECT TOP (5) account_number, name, account_type
    FROM accounts
    ORDER BY created_at DESC;
END;
