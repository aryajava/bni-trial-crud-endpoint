# Rencana Eksekusi — Rombakan Toko Online

> Desain final: PRD v2.0.0 · keputusan: `docs/adr/0001..0004` · kosakata: `CONTEXT.md`
> Prinsip urutan (diputuskan pemilik produk): fondasi dulu → harga → polish → fitur toko.

## Blok 1 — Improvement (fondasi)

### 1A. RBAC `SA` + Settings + lockout
- [x] `Script0017`: migrasi `CK_MASTER_USER_ROLE` (+'SA'), seed user `sa` (`sa123`, bcrypt), tabel `APP_SETTING` + seed `LOGIN_FAIL_THRESHOLD=5`, `SHIPPING_FEE=0`, `TAX_PERCENT=0`
- [x] `UserRolePolicy`: `SA`, rank 3, DisplayName "Super Admin", `IsValidRole`, `CanManage` (otomatis via rank)
- [x] `AllowedRolesAttribute` menerima `SA`
- [x] `AllowedRolesFor` halaman Create/Edit user + `UserService.ChangeRoleAsync` + guard last-active-in-role untuk SA + proteksi akun seed `sa`
- [x] Endpoint grid: `GET /api/categories/paged` + `GET /api/users/paged` (+ `POST /api/users/{id}/block|unblock`, guard rank) — enabler grid client-side semua tabel
- [ ] Konversi halaman data tabel ke **grid client-side standar** (komponen bersama): Produk ✓ sudah, Monitoring ✓ sudah → Kategori, Master User, User Control, Pelanggan, Pesanan, Audit, (Laporan) — pola fetch `/api/*/paged` + secret key user
- [x] Pindah semua halaman staf ke `/Panel/**` (rename Indonesia): Program.cs (LoginPath/AccessDeniedPath), `_Layout`, seluruh referensi path/redirect/fetch
- [x] Grup Settings: `PengaturanAplikasi` (SA) — UI ambang blokir → `APP_SETTING`; `PengaturanToko` (OWNER+SA) — UI ongkir & pajak
- [x] `Script0018` + rework lockout: tabel `TRX_AUDIT_LOG`; `AuthenticateAsync` membaca rentetan `LOGIN_FAILED` berbasis audit (staf); `Script0019` hapus `LOGIN_FAILED_COUNT`
- [x] Redaksi `password`/`secretKey`/`X-Api-Key` di `RequestResponseMiddleware` + log HTTP hanya `/api` & `/Panel`

### 1B. Harga sebelum/sesudah di persetujuan diskon
- [x] DTO/service Monitoring: tambah `HargaDasar`, `HargaSebelumDiskon`, `HargaSetelahDiskon` (rumus sama, hitung saat baca)
- [x] UI Monitoring: tampilkan harga sebelum↔sesudah di samping persen

### 1C. Polish
- [x] Notif validasi merah di bawah input: `/Panel/Masuk` & `/Panel/GantiKataSandi` (paralel form produk — atribut Required + summary + style di `_LoginLayout`)
- [x] Komponen dropdown standar (JS LIKE-search + ikon sort + default asc) → terpasang di semua `<select>` (role, kategori, filter status, page size)
- [ ] Konversi grid client-side: Kategori, Master User, User Control (komponen grid bersama — dilanjutkan di Blok 2 bersama Pelanggan/Pesanan/Audit)

## Blok 2 — Feature toko online

### 2A. Infrastruktur pelanggan
- [x] `Script0020`: `MASTER_CUSTOMER` + `TRX_CUSTOMER_AUDIT_TRAIL`; cookie/skema auth pelanggan terpisah; lockout berbasis audit untuk pelanggan
- [x] `/Masuk`, `/Daftar` (nama, email, sandi min 6), `/Keluar`; blokir → `/GantiKataSandi` (pemulihan)

### 2B. Area publik
- [x] Layout toko + `/` katalog (search LIKE, filter kategori, "Habis"), `/Produk/Detail/{id}`
- [x] Keranjang: JS localStorage (tamu) + `TRX_CART_ITEM` (login); merge otomatis saat masuk `/Keranjang`; `/Keranjang`
- [x] `/Profil` (edit + Hapus Akun) · `/PesananSaya` (terima/batal, stok kembali saat batal)

### 2C. Checkout & pesanan
- [x] `Script0021`: `TRX_ORDER`, `TRX_ORDER_ITEM` (+status who/when, snapshot harga/ongkir/pajak)
- [x] `/Checkout` (wajib login; data pengiriman → profil; ringkasan + Konfirmasi; stok atomik; DIPROSES)
- [x] `/Panel/Pesanan` (grid client-side; detail; DIPROSES→DIKIRIM; batalkan + alasan) · `/Panel/Pelanggan` (grid; blokir/buka, deaktif/aktifkan-S, reset sandi)

### 2D. Audit & analitik
- [x] Event pelanggan di `TRX_CUSTOMER_AUDIT_TRAIL` (REGISTER/LOGIN/LOGIN_FAILED/PROFILE_UPDATED/PASSWORD_CHANGED/BLOCKED/UNBLOCKED/DEACTIVATED/REACTIVATED/RESET_PASSWORD)
- [x] `/Panel/Settings/Audit` (grid baca-saja SA: entitas/aksi/cari/time filter)
- [x] `/Panel/LaporanPenjualan` (terlaris & jarang terjual, 7/30 hari/sepanjang masa) + KPI Dashboard (pesanan hari ini & menunggu proses)

### 2E. Penutup
- [ ] Sinkron PRD/addendum ✅ (kecuali §11 ditandai selesai), uji alur end-to-end di mesin Windows, build + smoke test
- [ ] Sisa tertunda dari Blok 1: konversi grid client-side Kategori/MasterUser/UserControl + Produk grid pakai secret key user (Q49)