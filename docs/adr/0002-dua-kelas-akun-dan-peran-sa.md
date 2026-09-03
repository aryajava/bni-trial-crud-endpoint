# Dua kelas akun: akun pengurus dan akun pelanggan

Toko online tidak boleh berbagi satu kelas akun dengan pengurus toko: pelanggan login pakai email di area publik, pengurus pakai username di area `/Panel`. Karena itu akun pelanggan hidup di tabel sendiri (`MASTER_CUSTOMER`) dengan autentikasi terpisah, dan hierarki peran (`SA` > `OWNER` > `ADMIN`) tetap hanya berlaku di `MASTER_USER`.

## Status
accepted

## Keputusan

- **`MASTER_USER`** (pengurus): role `SA` (Super Admin, seed `sa`/`sa123`), `OWNER`, `ADMIN`. Simbol role: `SA`/`OWNER`/`ADMIN`; label tampilan "Super Admin/Pemilik Toko/Admin Toko".
- **`MASTER_CUSTOMER`** (pelanggan): login email + kata sandi (bcrypt, pola sama), berisi nama, no. HP, alamat, kolom lockout, kolom audit standar.
- **Akun pelanggan tidak bisa dihapus** — hanya diblokir (`IS_BLOCKED`) oleh OWNER/SA, atau **hapus lunak** (`IS_ACTIVE=0`) oleh pemilik akun sendiri atau SA; pemulihan hanya oleh SA. Email dari akun hapus-lunak tetap terkunci.
- **Pengamanan role**: akun seed `sa` tidak bisa dihapus/diturunkan; minimal satu SA aktif (pola last-active-in-role diperluas); SA buat SA/OWNER/ADMIN, OWNER buat OWNER/ADMIN, ADMIN buat ADMIN.
- **Konvensi penamaan**: semua tabel dan objek DB baru memakai Bahasa Inggris (sudah berlaku ke semua tabel yang ada; `TRX_CART_ITEM`, `TRX_ORDER`, `TRX_ORDER_ITEM`, `MASTER_CUSTOMER`, `TRX_AUDIT_LOG`, `TRX_CUSTOMER_AUDIT_TRAIL`, `APP_SETTING`).

## Considered Options

- Pelanggan sebagai role `PELANGGAN` di `MASTER_USER`: ditolak — mencampur dua permukaan keamanan, mencemari hierarki, dan memaksa halaman staf menyaring pelanggan; pemilik produk memilih pemisahan penuh (URL & layout publik terpisah).

## Consequences

- Autentikasi pelanggan butuh cookie/skema tersendiri; pengecekan blokir login dilakukan dua kali per kelas akun (logika audit bersama).
- Constraint `CK_MASTER_USER_ROLE` harus dimigrasi agar menerima `'SA'`.