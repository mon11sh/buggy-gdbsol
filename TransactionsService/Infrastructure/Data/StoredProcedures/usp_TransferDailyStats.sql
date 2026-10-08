-- CONCEPT: SQL Server stored procedure (usp_TransferDailyStats)
-- Aggregates a single account's fund transfers for one calendar day (source_account_id side),
-- returning ONE row with the totals used by the AdoNet transfer-limit checks:
--   TotalAmount   : SUM(amount)  for the day  (ISNULL -> 0 when none)
--   TransferCount : COUNT(*)     for the day
-- Matches the EF query in TransferLimitRepository.GetDailyUsedAmountAsync (no status filter,
-- window [@Date, @Date + 1 day)).
-- Idempotent: CREATE OR ALTER lets startup re-run the script safely.
CREATE OR ALTER PROCEDURE usp_TransferDailyStats
    @AccountNumber INT,
    @Date DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Start DATETIME2 = CAST(@Date AS DATETIME2);
    DECLARE @End   DATETIME2 = DATEADD(DAY, 1, @Start);

    SELECT
        ISNULL(SUM(amount), 0) AS TotalAmount,
        COUNT(*)               AS TransferCount
    FROM fund_transfers
    WHERE source_account_id = @AccountNumber
      AND created_at >= @Start
      AND created_at <  @End;
END;
