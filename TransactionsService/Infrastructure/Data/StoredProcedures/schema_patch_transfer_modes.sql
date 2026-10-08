-- Idempotent schema patch for SQL Server databases created before the transfer-mode tables existed
-- (EnsureCreated never alters an existing schema; EF migrations cover the postgres/sqlite paths).
-- Many-to-many: transfer_limits (privilege) <-> transfer_modes (mode) through transfer_limit_modes.
IF OBJECT_ID('transfer_modes', 'U') IS NULL
BEGIN
    CREATE TABLE transfer_modes (
        mode        NVARCHAR(450) NOT NULL PRIMARY KEY,
        description NVARCHAR(MAX) NULL
    );
END;
IF OBJECT_ID('transfer_limit_modes', 'U') IS NULL
BEGIN
    CREATE TABLE transfer_limit_modes (
        privilege NVARCHAR(450) NOT NULL REFERENCES transfer_limits(privilege) ON DELETE CASCADE,
        mode      NVARCHAR(450) NOT NULL REFERENCES transfer_modes(mode) ON DELETE CASCADE,
        CONSTRAINT pk_transfer_limit_modes PRIMARY KEY (privilege, mode)
    );
END;
MERGE transfer_modes AS t
USING (VALUES ('NEFT', 'Batch settlement, any amount'), ('IMPS', 'Instant, small value'), ('UPI', 'Instant, small value'), ('RTGS', 'Real-time gross settlement, high value')) AS s(mode, description)
ON t.mode = s.mode
WHEN NOT MATCHED THEN INSERT (mode, description) VALUES (s.mode, s.description);
MERGE transfer_limit_modes AS t
USING (VALUES ('SILVER','NEFT'),('SILVER','IMPS'),('SILVER','UPI'),
              ('GOLD','NEFT'),('GOLD','IMPS'),('GOLD','UPI'),('GOLD','RTGS'),
              ('PREMIUM','NEFT'),('PREMIUM','IMPS'),('PREMIUM','UPI'),('PREMIUM','RTGS')) AS s(privilege, mode)
ON t.privilege = s.privilege AND t.mode = s.mode
WHEN NOT MATCHED AND EXISTS (SELECT 1 FROM transfer_limits l WHERE l.privilege = s.privilege) THEN INSERT (privilege, mode) VALUES (s.privilege, s.mode);
