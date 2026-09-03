# Siklus pesanan dan stok

Pesanan disimpan permanen dengan jejak status lengkap (prinsip audit berlaku untuk semua). Stok berkurang **saat checkout dikonfirmasi** — bukan saat masuk keranjang — dan dikembalikan saat pesanan dibatalkan. Harga dihitung ulang dari Harga Setelah Diskon pada saat konfirmasi, bukan dari harga yang tersimpan di item keranjang.

## Status
accepted

## Keputusan

- **Status pesanan**: `DIPROSES` → `DIKIRIM` (ditandai staf ADMIN/OWNER/SA) → `DITERIMA` (dikonfirmasi pelanggan); `DIBATALKAN` — **hanya saat berstatus `DIPROSES`** (oleh pelanggan atau staf ADMIN/OWNER/SA, dengan alasan wajib). Pesanan `DIKIRIM` tidak dapat dibatalkan — barang sudah keluar, stok tidak pernah dikembalikan dari status itu.
- **Stok**: validasi & pengurangan atomik saat konfirmasi (`SET STOCK = STOCK - @qty WHERE STOCK >= @qty`, tanpa menyentuh `VERSION`); dikembalikan saat batal (`DIBATALKAN` dari `DIPROSES`). Qty di keranjang otomatis disesuaikan ke sisa stok saat keranjang dibaca (berlebih → dikunci ke stok; produk habis → item dihapus).
- **Harga**: `SUBTOTAL = Σ qty × Harga Setelah Diskon`, `PAJAK_AMOUNT = SUBTOTAL × Pajak%`, `TOTAL = SUBTOTAL + ONGKIR + PAJAK_AMOUNT` — semua di-snapshot ke `TRX_ORDER` (`SUBTOTAL`, `SHIPPING_FEE`, `TAX_AMOUNT`, `TOTAL_AMOUNT`) bersama snapshot judul/harga per baris di `TRX_ORDER_ITEM`.
- **Pengaturan ongkir & pajak**: global di `APP_SETTING` (`SHIPPING_FEE`, `TAX_PERCENT`), diubah oleh OWNER/SA lewat Pengaturan Toko; berlaku untuk semua pesanan; nilai tersimpan sebagai snapshot di pesanan.
- **Integritas**: `TRX_ORDER` boleh ber-FK ke `MASTER_CUSTOMER` (pelanggan tak bisa dihapus); `TRX_ORDER_ITEM` memakai ID polos tanpa FK — baris riwayat harus selamat dari penonaktifan/hapus produk.

## Considered Options

- Mengunci harga di item keranjang: ditolak — harga basi saat admin mengubah harga di tengah sesi belanja.
- Menahan stok saat masuk keranjang: ditolak — keranjang tamu di localStorage tak terlihat server, dan barang yang "dipegang" tanpa checkout mengunci stok.
- Berbagai status perantara (menunggu pembayaran dsb.): ditolak — tanpa integrasi pembayaran, dua status terminal (DITERIMA/DIBATALKAN) cukup untuk trial.

## Consequences

- Eksekusi checkout memerlukan transaksi yang mencakup tulis pesanan + pengurangan stok.
- Halaman Laporan Penjualan bisa dihitung dari `TRX_ORDER_ITEM` tanpa tabel tambahan.