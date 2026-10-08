-- Idempotent schema patch for databases created before the transfer_id column existed
-- (EnsureCreated never alters an existing schema; EF migrations cover the postgres/sqlite paths).
-- Adds the one-to-many link transaction_logging.transfer_id -> fund_transfers.id with RESTRICT semantics.
IF COL_LENGTH('transaction_logging', 'transfer_id') IS NULL
BEGIN
    ALTER TABLE transaction_logging ADD transfer_id INT NULL;
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_transaction_logging_fund_transfers_transfer_id')
BEGIN
    ALTER TABLE transaction_logging
        ADD CONSTRAINT fk_transaction_logging_fund_transfers_transfer_id
        FOREIGN KEY (transfer_id) REFERENCES fund_transfers(id) ON DELETE NO ACTION;
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_transaction_logging_transfer_id')
BEGIN
    CREATE INDEX ix_transaction_logging_transfer_id ON transaction_logging(transfer_id);
END;
