-- Script0023: TRX_CART_ITEM.IS_SELECTED — pilihan item pesanan bertahan di server
-- (checkbox keranjang: tamu → localStorage, akun → server; default semua terpilih)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LOSCONSUMER.TRX_CART_ITEM') AND name = 'IS_SELECTED')
BEGIN
    ALTER TABLE LOSCONSUMER.TRX_CART_ITEM
        ADD IS_SELECTED BIT NOT NULL CONSTRAINT DF_TRX_CART_ITEM_IS_SELECTED DEFAULT (1);
END;