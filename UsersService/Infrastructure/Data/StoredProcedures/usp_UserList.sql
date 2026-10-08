-- CONCEPT: SQL Server stored procedure (usp_UserList)
-- Returns every user row ordered by created_at DESC — the read backing
-- IUserRepository.GetAllUsersAsync (mirrors the EF OrderByDescending(u => u.CreatedAt)).
-- The projected columns match the EF entity mapping so the ADO.NET reader can feed
-- UserMapper.ToDomain unchanged.
-- Idempotent: CREATE OR ALTER lets startup re-run the script safely.
CREATE OR ALTER PROCEDURE usp_UserList
AS
BEGIN
    SET NOCOUNT ON;

    SELECT user_id, username, login_id, password, role, is_active, created_at, updated_at
    FROM users
    ORDER BY created_at DESC;
END;
