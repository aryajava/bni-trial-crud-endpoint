-- Kolom audit standar untuk TRX_DISCOUNT_APPROVAL
-- (mengikuti format MASTER_PRODUCT / MASTER_USER).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('LOSCONSUMER.TRX_DISCOUNT_APPROVAL')
      AND name = 'IS_ACTIVE'
)
BEGIN
    ALTER TABLE LOSCONSUMER.TRX_DISCOUNT_APPROVAL
        ADD IS_ACTIVE   BIT          NOT NULL DEFAULT 1,
            CREATED_AT  DATETIME2    NOT NULL DEFAULT GETDATE(),
            CREATED_BY  NVARCHAR(100) NOT NULL DEFAULT 'SYSTEM',
            UPDATED_AT  DATETIME2    NULL,
            UPDATED_BY  NVARCHAR(100) NULL,
            VERSION     INT          NOT NULL DEFAULT 1;

    -- Backfill baris lama: audit mengikuti fakta yang sudah tercatat.
    UPDATE LOSCONSUMER.TRX_DISCOUNT_APPROVAL
    SET    CREATED_AT = REQUESTED_AT,
           CREATED_BY = REQUESTED_BY,
           UPDATED_AT = DECIDED_AT,
           UPDATED_BY = DECIDED_BY,
           VERSION    = CASE WHEN DECIDED_AT IS NULL THEN 1 ELSE 2 END,
           IS_ACTIVE  = CASE
                            WHEN STATUS = 'DITOLAK' AND DECIDED_BY = 'SISTEM' THEN 0
                            ELSE 1
                        END;
END;