-- Script0033: Penanda notifikasi terakhir dibaca per staf (notifikasi pesanan batal/diterima)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LOSCONSUMER.MASTER_USER') AND name = 'NOTIF_READ_AT')
    ALTER TABLE LOSCONSUMER.MASTER_USER ADD NOTIF_READ_AT DATETIME2 NULL;
GO