-- CONCEPT: SQL Server stored procedure (usp_GetAuthTokenByJti)
-- Reads a single auth_tokens row by its token_jti (the same lookup the EF
-- AuthTokenRepository.GetTokenAsync performs). Returns every column the mapper
-- (AuthMapper.ToDomain) needs to rebuild the AuthToken domain object.
-- Idempotent: CREATE OR ALTER lets startup re-run the script safely.
CREATE OR ALTER PROCEDURE usp_GetAuthTokenByJti
    @token_jti VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        id, user_id, login_id, token_jti, issued_at, expires_at, is_revoked, created_at
    FROM auth_tokens
    WHERE token_jti = @token_jti;
END;
