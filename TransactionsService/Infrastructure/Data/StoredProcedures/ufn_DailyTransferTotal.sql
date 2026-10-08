-- CONCEPT: SQL Server scalar-valued FUNCTION (ufn_DailyTransferTotal)
-- Returns the sum of COMPLETED transfers an account SENT on one calendar day - the figure the
-- daily-limit check compares against. A function (unlike a procedure) composes inside SELECTs:
--   SELECT dbo.ufn_DailyTransferTotal(1000, '2026-09-09')
-- Idempotent: CREATE OR ALTER lets startup re-run the script safely.
CREATE OR ALTER FUNCTION dbo.ufn_DailyTransferTotal
(
    @AccountNumber INT,
    @Date DATE
)
RETURNS DECIMAL(18, 2)
AS
BEGIN
    DECLARE @Start DATETIME2 = CAST(@Date AS DATETIME2);
    DECLARE @End   DATETIME2 = DATEADD(DAY, 1, @Start);
    DECLARE @Total DECIMAL(18, 2);

    SELECT @Total = ISNULL(SUM(amount), 0)
    FROM fund_transfers
    WHERE source_account_id = @AccountNumber
      AND status = 'COMPLETED'
      AND created_at >= @Start
      AND created_at <  @End;

    RETURN @Total;
END;
