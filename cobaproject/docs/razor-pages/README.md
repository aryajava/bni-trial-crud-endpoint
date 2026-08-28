# Dokumentasi Screen Razor Pages (CRUD Produk)

Dokumen ini menjelaskan **screen (tampilan web)** yang dibuat untuk API CRUD produk,
ditulis dengan bahasa awam supaya bisa dipelajari pelan-pelan oleh siapa pun,
termasuk yang baru tahu Razor Pages.

---

## Apa yang dibuat

Sebuah tampilan web sederhana, masih dalam project `cobaproject` yang sama
(ASP.NET Core, `net10.0`), yang bisa:

| Halaman | URL | Fungsi |
|---|---|---|
| Daftar Produk | `/Screen/Products` | Lihat semua produk + tombol edit/hapus |
| Tambah Produk | `/Screen/Products/Create` | Form untuk membuat produk baru |
| Edit Produk | `/Screen/Products/Edit/{id}` | Form untuk mengubah produk |
| Hapus Produk | `/Screen/Products/Delete/{id}` | Konfirmasi hapus (soft atau permanen) |

Semua halaman ini **memakai service dan aturan validasi yang sama dengan API** —
tidak ada logika bisnis yang ditulis ulang. Bedanya hanya "pintunya": API balas JSON,
screen balas HTML.

## Struktur file

```
cobaproject/
├── Program.cs                        → ditambah AddRazorPages() + MapRazorPages()
├── Helpers/ApiKeyMiddleware.cs       → ditambah pengecualian "/screen"
└── Pages/                            ← folder BARU (semua screen ditaruh di sini)
    ├── _ViewImports.cshtml           → pengaturan bersama semua halaman
    ├── _ViewStart.cshtml             → pemilihan layout bersama
    ├── Shared/
    │   ├── _Layout.cshtml            → kerangka halaman (header, CSS, footer)
    │   └── _ValidationScriptsPartial.cshtml → script validasi browser
    └── Screen/
        └── Products/
            ├── Index.cshtml   + Index.cshtml.cs    (daftar produk)
            ├── Create.cshtml  + Create.cshtml.cs   (tambah produk)
            ├── Edit.cshtml    + Edit.cshtml.cs     (ubah produk)
            └── Delete.cshtml  + Delete.cshtml.cs   (hapus produk)
```

## Kenapa URL-nya `/Screen/...`?

API product sudah menempati URL `/products` (di `Controllers/ProductsController.cs`).
Kalau halaman Razor juga dipasang di `/Products`, dua "juru jawab" berebut URL yang
sama dan aplikasi error (ambiguous match). Maka halaman diberi awalan `/Screen`
melalui letak folder: `Pages/Screen/Products/Index.cshtml` otomatis berarti URL
`/Screen/Products`. Nanti kalau ada entity lain, contohnya `Pages/Screen/Categories/`,
URL-nya jadi `/Screen/Categories` — pola yang rapi dan tidak bentrok.

## Bagaimana menjalankan

1. Pastikan SQL Server (LocalDB) aktif — app butuh database `LOSCONSUMER` (dibuat otomatis oleh migration dbup saat start).
2. Jalankan dari folder `cobaproject`:
   ```
   dotnet run
   ```
3. Buka browser ke:
   - Screen: `http://localhost:5251/Screen/Products`
   - API/Swagger: `http://localhost:5251/swagger` (seperti biasa)

Catatan: halaman screen **tidak butuh API key** (`X-Api-Key`), karena layar
browser tidak bisa mengirim header custom. Yang memakai key tetaplah API-nya.
Setiap permintaan dari screen tercatat di log request/response seperti biasa.

## Peta dokumentasi (baca berurutan)

| File | Isi |
|---|---|
| [01-dasar-razor.md](01-dasar-razor.md) | **Mulai dari sini.** Apa itu `@`, `@page`, `@model`, GET vs POST, dan cara kerja pasangan `.cshtml` + `.cshtml.cs` |
| [02-page-index.md](02-page-index.md) | Pembahasan halaman Daftar Produk baris demi baris |
| [03-page-create.md](03-page-create.md) | Pembahasan halaman Tambah Produk: form, binding, validasi |
| [04-page-edit.md](04-page-edit.md) | Pembahasan halaman Edit Produk: route `{id}`, versi, konflik |
| [05-page-delete.md](05-page-delete.md) | Pembahasan halaman Hapus Produk: dua tombol, dua handler |
| [06-layout-dan-konfigurasi.md](06-layout-dan-konfigurasi.md) | Layout, `_ViewImports`, `_ViewStart`, dan perubahan di `Program.cs` |