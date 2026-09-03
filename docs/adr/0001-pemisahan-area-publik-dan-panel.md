# Pemisahan area publik dan area pengurus

Aplikasi berubah dari aplikasi admin menjadi toko online: pelanggan tidak boleh tahu bahwa area pengelolaan toko itu ada, jadi area publik memakai URL bersih di root host dan area pengurus disembunyikan di balik prefix `/Panel`. Seluruh halaman staf yang ada dipindah dan dinamai ulang ke Bahasa Indonesia (`/Panel/Produk`, `/Panel/Kategori`, `/Panel/Monitoring`, `/Panel/MasterUser`, `/Panel/UserControl`, `/Panel/Pelanggan`, `/Panel/Pesanan`, `/Panel/LaporanPenjualan`, `/Panel/Masuk`, `/Panel/GantiKataSandi`, `/Panel/Ditolak`), diputuskan dalam iterasi desain bersama pemilik produk.

## Status
accepted

## Keputusan

- **Publik (root, layout toko)**: `/` (katalog — untuk semua orang, termasuk staf), `/Produk/{id}`, `/Keranjang`, `/Checkout`, `/Masuk`, `/Daftar`, `/Profil`, `/GantiKataSandi`, `/PesananSaya`, `/Keluar`
- **Pengurus (`/Panel/**`)**: semua halaman staf + `Settings` (grup: `PengaturanAplikasi` SA saja, `PengaturanToko` SA+OWNER, `Audit` SA saja)
- **Login terpisah dua kelas akun**: cookie staf `GKLaku.Auth` (username) vs cookie pelanggan baru (email) — cookie tidak saling menerima
- **`/api/*` + `X-Api-Key` tidak berubah**; anonim yang membuka halaman `/Panel` diarahkan ke `/Panel/Masuk`

## Considered Options

- `/Toko/*` sebagai prefix publik: ditolak — URL tidak "bersih" dan memperlihatkan keberadaan area staf dari pemisahan path itu sendiri.
- Satu area dengan guard per halaman: ditolak — tidak memenuhi syarat "pelanggan tidak tahu URL staf".

## Consequences

- Setiap halaman staf berubah path-nya → tautan, redirect, dan referensi fetch perlu diperbarui sekaligus.
- `LoginPath`/`AccessDeniedPath` cookie auth ikut dipindah.