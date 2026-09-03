# Audit trail dua sisi dan lockout berbasis audit

Audit adalah tabel basis data, bukan file log: `TRX_AUDIT_LOG` mencatat aksi pengurus, `TRX_CUSTOMER_AUDIT_TRAIL` mencatat siklus hidup akun pelanggan — dan **event audit menjadi satu-satunya sumber kebenaran blokir akun** (kolom counter dihapus). Data sensitif (kata sandi, API key) diredaksi sebelum disimpan; Serilog hanya untuk kesalahan teknis.

## Status
accepted

## Keputusan

- **`TRX_AUDIT_LOG`** (aksi pengurus): entitas `PRODUCT`, `CATEGORY`, `USER`, `DISCOUNT_APPROVAL`, `ORDER`, `SETTING`; aksi antara lain `CREATE/UPDATE/DELETE/APPROVE/REJECT/BLOCK/UNBLOCK/RESET_PASSWORD/CHANGE_ROLE/LOGIN/LOGIN_FAILED/SETTING_CHANGED/ORDER_SHIPPED/ORDER_CANCELLED`; snapshot JSON nilai sebelum-sesudah, `REASON`, `TRACE_ID` (korelasi ke `REQUEST_PRODUCT`). Ditulis eksplisit dari service — bukan trigger — karena service tahu makna aksi & alasan.
- **`TRX_CUSTOMER_AUDIT_TRAIL`** (siklus hidup akun pelanggan): `REGISTER/LOGIN/LOGIN_FAILED/PROFILE_UPDATED/PASSWORD_CHANGED/BLOCKED/UNBLOCKED/DEACTIVATED/REACTIVATED`; `ACTOR` bisa email pelanggan atau username pengurus (blokir oleh staf dicatat di sini, bukan di tabel audit staf).
- **Lockout berbasis audit**: cek blokir = jumlah `LOGIN_FAILED` berurutan sejak `LOGIN`/`PASSWORD_CHANGED`/`UNBLOCKED` terakhir ≥ ambang (`APP_SETTING.LOGIN_FAIL_THRESHOLD`, default 5) → `IS_BLOCKED = 1` (kolom tetap dipakai sebagai kondisi cepat); kolom `LOGIN_FAILED_COUNT` dihapus dari `MASTER_USER` dan `MASTER_CUSTOMER`. Berlaku simetris dua kelas akun; blokir tetap permanen sampai ganti sandi/dibuka.
- **Redaksi**: field `password`, `secretKey`, header `X-Api-Key` diganti `***` sebelum disimpan ke log HTTP maupun audit.
- **Cakupan log HTTP**: `REQUEST_PRODUCT`/`RESPONSE_PRODUCT` mencatat `/api/*` dan `/Panel/*` saja; lalu lintas publik dikecualikan (jejak bisnisnya sudah ada di kolom status pesanan dan audit).
- **Integritas**: kedua tabel audit memakai ID polos tanpa FK — bukti harus selamat dari penghapusan data yang diaudit.

## Considered Options

- Kolom counter sebagai sumber blokir (pola lama) + audit hanya arsip: ditolak — dua sumber yang bisa tidak sinkron dan tabel audit tidak dimanfaatkan.
- Tabel audit per-entitas: ditolak — banyak tabel kaku; satu tabel generik + snapshot JSON cukup dan mudah difilter.
- DB trigger untuk menulis audit: ditolak — aksi tidak bermakna (tidak tahu alasan), sulit dirawat.
- Audit pelanggan digabung ke tabel audit staf: ditolak — pemilik produk memilih pemisahan tegas dua sisi.

## Consequences

- Service yang memutasi data wajib menulis event audit (pola helper bersama); migrasi menghapus kolom counter menuntut rework logika login sebelum blokir berlaku.
- Cek blokir butuh query rentetan `LOGIN_FAILED` (indeks `CUSTOMER_ID/ACTED_AT`) — biaya kecil per login gagal.