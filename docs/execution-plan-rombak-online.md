# Rencana Eksekusi — Rombakan Toko Online

> Desain final: PRD v2.0.0 · keputusan: `docs/adr/0001..0004` · kosakata: `CONTEXT.md`
> Prinsip urutan (diputuskan pemilik produk): fondasi dulu → harga → polish → fitur toko.

## Blok 1 — Improvement (fondasi)

### 1A. RBAC `SA` + Settings + lockout
- [x] `Script0017`: migrasi `CK_MASTER_USER_ROLE` (+'SA'), seed user `sa` (`sa123`, bcrypt), tabel `APP_SETTING` + seed `LOGIN_FAIL_THRESHOLD=5`, `SHIPPING_FEE=0`, `TAX_PERCENT=0`
- [x] `UserRolePolicy`: `SA`, rank 3, DisplayName "Super Admin", `IsValidRole`, `CanManage` (otomatis via rank)
- [x] `AllowedRolesAttribute` menerima `SA`
- [ ] `AllowedRolesFor` halaman Create/Edit user + `UserService.ChangeRoleAsync` + guard last-active-in-role untuk SA + proteksi akun seed `sa`
- [x] Endpoint grid: `GET /api/categories/paged` + `GET /api/users/paged` (+ `POST /api/users/{id}/block|unblock`, guard rank) — enabler grid client-side semua tabel
- [ ] Konversi halaman data tabel ke **grid client-side standar** (komponen bersama): Produk ✓ sudah, Monitoring ✓ sudah → Kategori, Master User, User Control, Pelanggan, Pesanan, Audit, (Laporan) — pola fetch `/api/*/paged` + secret key user
- [ ] Pindah semua halaman staf ke `/Panel/**` (rename Indonesia): Program.cs (LoginPath/AccessDeniedPath), `_Layout`, seluruh referensi path/redirect/fetch
- [ ] Grup Settings: `PengaturanAplikasi` (SA) — UI ambang blokir → `APP_SETTING`; `PengaturanToko` (OWNER+SA) — UI ongkir & pajak
- [ ] `Script0018` + rework lockout: tabel `TRX_AUDIT_LOG`; `AuthenticateAsync` membaca rentetan `LOGIN_FAILED` berbasis audit (staf); `Script0019` hapus `LOGIN_FAILED_COUNT`
- [ ] Redaksi `password`/`secretKey`/`X-Api-Key` di `RequestResponseMiddleware`

### 1B. Harga sebelum/sesudah di persetujuan diskon
- [ ] DTO/service Monitoring: tambah `HargaDasar` & `HargaSetelahDiskon` (rumus sama, hitung saat baca)
- [ ] UI Monitoring: tampilkan harga sebelum↔sesudah di samping persen

### 1C. Polish
- [ ] Notif validasi merah di bawah input: `/Panel/Masuk` & `/Panel/GantiKataSandi` (paralel form produk)
- [ ] Komponen dropdown standar (JS LIKE-search + ikon sort + default asc) → pasang ke semua `<select>`

## Blok 2 — Feature toko online

### 2A. Infrastruktur pelanggan
- [ ] `Script0020`: `MASTER_CUSTOMER` + `TRX_CUSTOMER_AUDIT_TRAIL`; cookie/skema auth pelanggan terpisah; lockout berbasis audit untuk pelanggan
- [ ] `/Masuk`, `/Daftar` (nama, email, sandi min 6), `/Keluar`; blokir → `/GantiKataSandi` (pemulihan)

### 2B. Area publik
- [ ] Layout toko + `/` katalog (search LIKE, filter kategori, "Habis"), `/Produk/{id}`
- [ ] Keranjang: JS localStorage (tamu) + `TRX_CART_ITEM` (login); merge otomatis saat login; `/Keranjang`
- [ ] `/Profil` (edit + Hapus Akun) · `/PesananSaya`

### 2C. Checkout & pesanan
- [ ] `Script0021`: `TRX_ORDER`, `TRX_ORDER_ITEM` (+status who/when, snapshot harga/ongkir/pajak)
- [ ] `/Checkout` (wajib login; data pengiriman → profil; ringkasan + Konfirmasi; stok atomik; DIPROSES)
- [ ] `/Panel/Pesanan` (list/filter/detail; DIPROSES→DIKIRIM; batalkan + alasan) · `/Panel/Pelanggan` (blokir/buka/deaktifkan)
- [ ] Transisi `/PesananSaya`: DITERIMA, batalkan (sblm DIKIRIM); stok kembali saat batal

### 2D. Audit & analitik
- [ ] Audit service helper: tulis `TRX_AUDIT_LOG` di semua service yang bermutasi + event pelanggan di `TRX_CUSTOMER_AUDIT_TRAIL`
- [ ] `/Panel/Settings/Audit` (grid pola Monitoring, baca-saja, filter)
- [ ] `/Panel/LaporanPenjualan` (terlaris & jarang terjual, filter 7/30/hari/sepanjang masa) + KPI Dashboard

### 2E. Penutup
- [ ] Sinkron PRD/addendum, uji alur end-to-end (daftar→beli→kirim→terima→batal), build + smoke test