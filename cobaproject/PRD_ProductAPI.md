# PRD — Toko Online LOSCONSUMER (Consumer & Server)

> **Version:** 2.0.0 | **Date:** 2026-09-03 | **Author:** Engineering
> **Updated:** Rombakan total dari aplikasi admin CRUD menjadi toko online — dua area (publik & `/Panel`), dua kelas akun (pengurus & pelanggan), siklus pesanan penuh, audit trail dua sisi, laporan penjualan. Keputusan desain direkam di `docs/adr/0001..0004`; kosakata domain di `CONTEXT.md`.

---

## 1. Overview

Aplikasi berbasis ASP.NET Core (.NET 10) yang menjadi **toko online**: pelanggan menjelajah produk, mengisi keranjang, dan checkout dengan akun; pengurus toko mengelola katalog, persetujuan diskon, pesanan, pelanggan, dan pengaturan dari area terpisah yang tidak terlihat oleh publik.

| Peran | Keterangan |
|---|---|
| **Pelanggan** | Membeli: katalog → keranjang → checkout (wajib akun) → pesanan |
| **Consumer (API)** | Menarik data dari `https://fakestoreapi.com/products` dan menyinkronkan ke DB lokal |
| **Server (API/UI)** | CRUD produk/kategori/user + persetujuan diskon + pesanan + audit |

Semua keputusan bisnis ditulis permanen (audit trail dua sisi, tabel DB, bukan file). API dilindungi `X-Api-Key`; UI staf dilindungi cookie login.

---

## 2. Area & Navigasi

### 2.1 Area Publik (`/` — root, layout toko, tanpa login wajib)

| Path | Halaman |
|---|---|
| `/` | Katalog (grid + search LIKE + filter kategori, stok 0 = "Habis") |
| `/Produk/{id}` | Detail produk |
| `/Keranjang` | Keranjang (tamu: localStorage; login: server) |
| `/Checkout` | Checkout — **wajib login** |
| `/Masuk` / `/Keluar` | Login/logout pelanggan (email + sandi) |
| `/Daftar` | Daftar akun (nama, email, sandi) |
| `/Profil` | Edit profil + **Hapus Akun** (soft delete oleh pemilik akun) |
| `/GantiKataSandi` | Ganti sandi; juga jalan pemulihan dari blokir |
| `/PesananSaya` | Riwayat pesanan + konfirmasi DITERIMA / batalkan |

### 2.2 Area Pengurus (`/Panel/**` — perlu login; anonim → `/Panel/Masuk`)

| Path | Akses |
|---|---|
| `/Panel` (Dashboard) | ADMIN+OWNER+SA |
| `/Panel/Produk`, `/Panel/Kategori` | ADMIN+OWNER+SA (delete: OWNER+SA) |
| `/Panel/Monitoring` (persetujuan diskon) | semua staf; putuskan: OWNER |
| `/Panel/MasterUser` | OWNER+SA |
| `/Panel/UserControl` | OWNER+SA |
| `/Panel/Pelanggan` | OWNER+SA (blokir/buka; hapus-lunak & aktifkan kembali: SA) |
| `/Panel/Pesanan` | ADMIN+OWNER+SA (DIPROSES→DIKIRIM; batalkan) |
| `/Panel/LaporanPenjualan` | OWNER+SA |
| `/Panel/Settings/PengaturanAplikasi` | **SA saja** (ambang blokir login) |
| `/Panel/Settings/PengaturanToko` | OWNER+SA (ongkir tetap + pajak %) |
| `/Panel/Settings/Audit` | **SA saja** |

### 2.3 Peran (`MASTER_USER`)

Hierarki **SA > OWNER > ADMIN** (`SA`/`OWNER`/`ADMIN`; label "Super Admin/Pemilik Toko/Admin Toko"). Seed: `admin`, `owner`, `sa` (`sa123`). Aturan: SA buat SA/OWNER/ADMIN · OWNER buat OWNER/ADMIN · ADMIN buat ADMIN; akun seed `sa` tak bisa dihapus/diturunkan; minimal satu SA aktif. Cek blokir hanya saat masuk (tanpa kick-session).

---

## 3. Akun Pelanggan (`MASTER_CUSTOMER`)

Login **email + sandi** (bcrypt). Blokir (`IS_BLOCKED`): oleh sistem (gagal login berulang — lihat §7) atau OWNER/SA; buka kembali oleh OWNER/SA. **Hapus lunak** (`IS_ACTIVE=0`): oleh pemilik akun sendiri atau SA; pemulihan hanya SA; email tetap terkunci; riwayat dan audit utuh. Akun pelanggan **tidak bisa dihapus** dari DB.

---

## 4. Keranjang & Checkout

1. Keranjang tamu = `localStorage` browser; saat login → digabung otomatis ke `TRX_CART_ITEM` (qty dijumlah, dibatasi stok; produk nonaktif dilewati) dan localStorage dikosongkan; keranjang ikut akun lintas perangkat
2. Tombol **Checkout** → wajib login (`/Masuk`/`/Daftar`)
3. Isi data pengiriman (nama, no. HP, alamat, catatan opsional) — tersimpan ke profil sebagai default
4. Ringkasan: `SUBTOTAL = Σ qty × Harga Setelah Diskon` · `PAJAK = SUBTOTAL × Pajak%` · `TOTAL = SUBTOTAL + ONGKIR + PAJAK` — **harga dihitung ulang saat konfirmasi**
5. **Konfirmasi Pesanan**: stok divalidasi & dikurang atomik; pesanan `DIPROSES`; keranjang dikosongkan
6. Riwayat di `/PesananSaya`

Stok berkurang **hanya saat checkout**, bukan saat masuk keranjang; dikembalikan saat pesanan dibatalkan.

---

## 5. Pesanan (`TRX_ORDER`, `TRX_ORDER_ITEM`)

### Status & transisi

```
DIPROSES ──(staf ADMIN/OWNER/SA: "Tandai Dikirim")──▶ DIKIRIM ──(pelanggan)──▶ DITERIMA
    │                                                        │
    └──(DIBATALKAN: pelanggan sblm DIKIRIM / staf kapan saja, alasan wajib)──▶ DIBATALKAN
```

Snapshot per pesanan: `SUBTOTAL`, `SHIPPING_FEE`, `TAX_AMOUNT`, `TOTAL_AMOUNT` + per baris judul/harga/qty (`TRX_ORDER_ITEM`, tanpa FK). Riwayat status who/when (`DIPROSES_AT`, `DIKIRIM_AT/BY`, `DITERIMA_AT/BY`, `DIBATALKAN_AT/BY/REASON`).

### Pengaturan ongkir & pajak

Global di `APP_SETTING` (`SHIPPING_FEE` Rp tetap, `TAX_PERCENT` %): diubah OWNER/SA di Pengaturan Toko, berlaku untuk semua pesanan, nilai disnapshot per pesanan.

---

## 6. Persetujuan Diskon

Alur **tidak berubah** (pengajuan persen diskon → keputusan oleh OWNER/SYSTEM, satu MENUNGGU per produk). Yang berubah: halaman Monitoring menampilkan **Harga Dasar & Harga Setelah Diskon** sebelum↔sesudah, dihitung saat dibaca (rumus yang sama: diskon dari `PRICE`, pembulatan ke 100). `PRICE` tidak pernah berubah oleh diskon.

---

## 7. Lockout Berbasis Audit

- Sumber kebenaran = event `LOGIN_FAILED` berurutan sejak `LOGIN`/`PASSWORD_CHANGED`/`UNBLOCKED` terakhir ≥ ambang → `IS_BLOCKED = 1`
- Ambang global `APP_SETTING.LOGIN_FAIL_THRESHOLD` (default 5), diubah SA di Pengaturan Aplikasi; berlaku untuk pengurus **dan** pelanggan
- Blokir permanen; pemulihan: ganti sandi (halaman khusus) atau dibuka pengurus
- Kolom `LOGIN_FAILED_COUNT` **dihapus** dari `MASTER_USER` & `MASTER_CUSTOMER`

---

## 8. Audit Trail (DB, bukan file)

| Tabel | Isi |
|---|---|
| `TRX_AUDIT_LOG` | Aksi pengurus: entitas PRODUCT/CATEGORY/USER/DISCOUNT_APPROVAL/ORDER/SETTING; snapshot JSON sebelum-sesudah; `REASON`; `TRACE_ID` → `REQUEST_PRODUCT`; termasuk `LOGIN`/`LOGIN_FAILED` |
| `TRX_CUSTOMER_AUDIT_TRAIL` | REGISTER/LOGIN/LOGIN_FAILED/PROFILE_UPDATED/PASSWORD_CHANGED/BLOCKED/UNBLOCKED/DEACTIVATED/REACTIVATED; ACTOR = email pelanggan atau username pengurus |
| `REQUEST_PRODUCT`/`RESPONSE_PRODUCT` | Log HTTP untuk `/api/*` & `/Panel/*` saja (publik dikecualikan) |

Redaksi: `password`/`secretKey`/`X-Api-Key` → `***`. Kedua tabel audit **tanpa FK** (bukti selamat dari penghapusan data). Serilog hanya untuk kesalahan teknis.

---

## 9. Laporan Penjualan

`/Panel/LaporanPenjualan` (OWNER+SA): dari `TRX_ORDER_ITEM` (mengecualikan pesanan `DIBATALKAN`) — **terlaris** (top 10: produk, qty, pendapatan) & **jarang terjual** (bottom 10 / nol penjualan), filter 7/30 hari/sepanjang masa. Masukan keputusan harga/diskon Pemilik Toko.

---

## 10. Database — Tabel Baru (penamaan Inggris)

| Tabel | Keterangan |
|---|---|
| `MASTER_CUSTOMER` | Akun pelanggan (email unik, bcrypt, nama, HP, alamat, lockout, audit) |
| `TRX_CART_ITEM` | Keranjang server (CUSTOMER_ID, PRODUCT_ID, QUANTITY; unik per pasangan) |
| `TRX_ORDER`, `TRX_ORDER_ITEM` | Pesanan + snapshot baris |
| `TRX_AUDIT_LOG` | Audit aksi pengurus |
| `TRX_CUSTOMER_AUDIT_TRAIL` | Audit siklus hidup pelanggan |
| `APP_SETTING` | `LOGIN_FAIL_THRESHOLD` (SA), `SHIPPING_FEE` & `TAX_PERCENT` (OWNER/SA) |

Migrasi: dbup `Scripts/Script00XX_*.sql` — lanjutan dari 16 skrip yang ada; constraint `CK_MASTER_USER_ROLE` dimigrasi menerima `'SA'`.

---

## 11. Endpoints API

**Grid client-side (standar semua tabel):** setiap data tabel punya endpoint `paged` dengan parameternya masing-masing (`Page/PageSize/SortBy/SortOrder/Search` + filter spesifik). Sudah ada: produk, persetujuan diskon, **kategori** (`/api/categories/paged`, sort whitelist: id/name/productCount/createdAt/updatedAt, filter `Active`), **user** (`/api/users/paged`, sort whitelist: id/username/displayName/role/lastLoginAt/createdAt/updatedAt, filter `Role`).

| Method | Path | Keterangan |
|---|---|---|
| GET/POST/PUT/DELETE | `/api/products` (`/paged`, `/{id}`) | CRUD produk; DELETE `?type=soft\|hard` (OWNER) |
| GET | `/api/products/public*` | FakeStore API |
| GET/POST | `/api/discount-approvals` (`/paged`, `/{id}/approve\|reject`) | Persetujuan diskon (keputusan: OWNER) |
| GET/POST/PUT/DELETE | `/api/categories` (`/paged`, `/active`, `/{id}`) | Kategori (delete: OWNER+SA) |
| GET/POST/PUT/DELETE | `/api/users` (`/paged`, `/{id}`, role/active/reset-password/secret-key) | User pengurus |
| POST | `/api/users/{id}/block` · `/unblock` | Blokir/buka (OWNER+SA, guard rank) |

**Endpoint masa depan** (dibangun bersama bloknya): `/api/customers` (paged, block/unblock, reset-sandi, hapus-lunak/aktifkan — Blok 2A) · `/api/orders` (paged, `{id}/ship`, `{id}/cancel` — Blok 2C) · `/api/audit-logs/paged` (Blok 2D) · `/api/settings` (get, `{key}` put; threshold→SA, ongkir/pajak→OWNER+SA — menyusul setelah TRX_AUDIT_LOG) · `/api/reports/sales` (Laporan Penjualan — Blok 2D).

Auth API: `X-Api-Key` (fallback `TEST123` → SYSTEM/OWNER; selain itu `SECRET_KEY` per user). Bypass: `/swagger`, `/openapi`, `/favicon.ico`. Halaman grid memakai **secret key user yang login** (pola Monitoring — lihat ADR-0004).

---

## 12. Rencana Eksekusi (ringkas)

Lihat `docs/execution-plan-rombak-online.md`. Urutan: **Blok 1** (fondasi RBAC/SA + Settings + lockout berbasis audit → harga di approval → polish UI) dilanjut **Blok 2** (toko online: akun pelanggan, area publik, keranjang/checkout/pesanan, audit dua sisi, laporan).

---

*PRD v2.0.0 — dirancang melalui sesi perancangan berulang (grilling + domain modeling), 2026-09-03*