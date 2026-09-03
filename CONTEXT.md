# BNI Trial — Toko Online (LOSCONSUMER)

Aplikasi toko online trial berbasis ASP.NET Core: katalog publik dengan area pengurus yang terpisah, peran berjenjang (SA, Pemilik Toko, Admin Toko), persetujuan diskon, dan belanja pelanggan (keranjang → checkout → pesanan). Semua fungsi berpegang pada prinsip jejak audit yang jelas.

## Language

### Peran

**Super Admin (SA)**:
Peran puncak yang mengelola pengaturan sistem dan pengguna seluruh peran.
_Avoid_: superuser, root, sa (huruf kecil saja tanpa konteks)

**Pemilik Toko**:
Peran operasional tertinggi toko: mengelola produk, kategori, dan memutuskan persetujuan diskon.
_Avoid_: owner

**Admin Toko**:
Peran operasional toko tanpa wewenang memutuskan persetujuan diskon.
_Avoid_: admin

**Peran**:
Jenjang hak akses: Super Admin (SA) di puncak, di bawahnya Pemilik Toko, lalu Admin Toko.
_Avoid_: role, user level, jabatan

### Harga

**Harga Dasar**:
Harga resmi produk sebelum diskon; tidak pernah berubah oleh diskon.
_Avoid_: harga asli, harga awal

**Harga Setelah Diskon**:
Harga jual yang berlaku bagi produk yang sedang aktif diskon, dihitung dari Harga Dasar dikurangi besaran diskon yang disetujui.
_Avoid_: harga efektif, harga jual, harga promo

### Persetujuan Diskon

**Persetujuan Diskon**:
Proses di mana pengubahan besaran diskon suatu produk diajukan, lalu diputuskan (disetujui/ditolak) oleh Pemilik Toko; hanya satu permintaan menunggu per produk.
_Avoid_: approval, pengajuan diskon, request diskon

### Belanja

**Keranjang**:
Kumpulan produk yang dikumpulkan pelanggan untuk dibeli; untuk tamu tersimpan di browser, untuk pelanggan yang sudah masuk tersimpan di akunnya; keranjang tidak memengaruhi stok produk.
_Avoid_: cart, troli, bakul

**Pesanan**:
Catatan pembelian yang dibuat pelanggan saat menyelesaikan checkout; stok berkurang saat pesanan dibuat, dan harga dihitung ulang dari Harga Setelah Diskon pada saat itu. Berstatus DIPROSES → DIKIRIM → DITERIMA, atau DIBATALKAN — pembatalan hanya dapat dilakukan saat masih DIPROSES, dan saat dibatalkan stok dikembalikan. Qty di Keranjang pengguna lain otomatis menyesuaikan sisa stok.
_Avoid_: order, transaksi, nota

**Blokir Akun**:
Penonaktifan sementara akun pelanggan oleh sistem (setelah gagal masuk berulang) atau oleh pengurus; akun masih ada dan bisa dibuka kembali, jejaknya utuh.
_Avoid_: banned, suspend, kunci akun

**Hapus Akun**:
Penonaktifan permanen akun pelanggan — hanya oleh pemilik akun sendiri atau Super Admin; akun tidak bisa masuk lagi, email terkunci, tetapi data dan jejaknya tetap utuh.
_Avoid_: delete akun, nonaktif akun

**Pelanggan**:
Orang yang berbelanja di toko; boleh menjelajah katalog dan mengisi keranjang tanpa masuk, tetapi wajib memiliki akun (masuk) saat checkout. Akun pelanggan terpisah dari akun pengurus toko — tabel dan area login yang berbeda.
_Avoid_: customer, pembeli, buyer, member

**Pengurus Toko**:
Sebutan kolektif untuk Super Admin, Pemilik Toko, dan Admin Toko — orang-orang yang mengelola toko dari area staf.
_Avoid_: staff, karyawan, internal, user

### Jejak

**Audit Trail**:
Catatan permanen setiap tindakan aplikasi — jejak HTTP (permintaan & tanggapan pengurus), aksi bisnis pengurus yang mengubah data, dan siklus hidup akun pelanggan (daftar, masuk, gagal masuk, ubah profil, ganti sandi, blokir/buka) — siapa, kapan, nilai sebelum-sesudah — disimpan di tabel basis data dan wajib ada di semua fungsi. Kesalahan teknis dicatat terpisah di file log dan bukan bagian dari audit trail.
_Avoid_: log, riwayat, history, file log