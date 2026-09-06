-- Script0025: Backfill SECRET_KEY untuk akun seed (admin/owner/sa dibuat tanpa kunci,
-- sehingga halaman grid jatuh ke kunci fallback dan endpoint ber-peran tinggi tertolak)
IF EXISTS (SELECT 1 FROM LOSCONSUMER.MASTER_USER WHERE SECRET_KEY IS NULL)
BEGIN
    UPDATE LOSCONSUMER.MASTER_USER
    SET    SECRET_KEY = REPLACE(CAST(NEWID() AS NVARCHAR(36)), '-', ''),
           UPDATED_AT = GETDATE(),
           UPDATED_BY = 'SYSTEM',
           VERSION    = VERSION + 1
    WHERE  SECRET_KEY IS NULL;
END;