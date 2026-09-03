-- Script0019: Lockout kini berbasis TRX_AUDIT_LOG — kolom counter dihapus.
-- Kolom ini lahir dengan DEFAULT 0 (Script0015); constraint defaultnya di-drop
-- lebih dulu karena ALTER DROP COLUMN menolak kolom yang masih dirujuk.
DECLARE @constraintName NVARCHAR(128);

SELECT @constraintName = dc.name
FROM sys.default_constraints dc
WHERE dc.parent_object_id = OBJECT_ID('LOSCONSUMER.MASTER_USER')
  AND dc.parent_column_id = COLUMNPROPERTY(OBJECT_ID('LOSCONSUMER.MASTER_USER'), 'LOGIN_FAILED_COUNT', 'ColumnId');

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(300) = N'ALTER TABLE LOSCONSUMER.MASTER_USER DROP CONSTRAINT [' + @constraintName + N']';
    EXEC sp_executesql @sql;
END;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LOSCONSUMER.MASTER_USER') AND name = 'LOGIN_FAILED_COUNT')
BEGIN
    ALTER TABLE LOSCONSUMER.MASTER_USER DROP COLUMN LOGIN_FAILED_COUNT;
END;