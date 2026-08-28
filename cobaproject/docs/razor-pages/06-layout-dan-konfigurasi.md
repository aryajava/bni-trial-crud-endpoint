# 06 — Layout, Konfigurasi, dan Perubahan di `Program.cs`

Halaman-halaman sebelumnya tidak pernah dibahas sendirian: mereka bergantung
pada beberapa file "pembantu" yang menyatukan tampilan, dan dua perubahan
di file konfigurasi yang membuat semuanya berjalan. Ini dokumen penutupnya.

---

## 1. Perubahan di `Program.cs` (2 baris)

Sebelum screen dibuat, app hanya tahu tentang API (`AddControllers` +
`MapControllers`). Dua baris ditambahkan:

```csharp
builder.Services.AddControllers();
builder.Services.AddRazorPages();      // BARU: daftarkan mesin Razor Pages ke DI
...
app.MapControllers();
app.MapRazorPages();                   // BARU: aktifkan routing halaman Pages/
```

- `AddRazorPages()` — memberitahu aplikasi: "siapkan layanan yang dibutuhkan
  halaman-halaman Razor" (routing file-based, PageModel, validasi, dsb.).
- `MapRazorPages()` — "mulai terima URL untuk folder `Pages/`".
- Keduanya **tidak menggantikan** kontroler API — keduanya hidup berdampingan.
  Inilah sebabnya `/products` (API) dan `/Screen/Products` (screen) bisa jalan
  bersamaan di satu aplikasi.

## 2. Perubahan di `Helpers/ApiKeyMiddleware.cs` (1 baris)

```csharp
private static readonly string[] ExcludedPathPrefixes =
    ["/swagger", "/openapi", "/favicon.ico", "/_framework", "/_vs", "/screen"];
```

Tambah `"/screen"` ke daftar pengecualian. Kenapa perlu?

- Middleware ini mewajibkan header `X-Api-Key` untuk **setiap** request —
  termasuk halaman web.
- Browser tidak punya cara mengirim header custom saat membuka URL biasa.
- Tanpa pengecualian ini, membuka `/Screen/Products` akan menampilkan JSON
  error 401 alih-alih halaman.

Penting: screen **tidak** terbuka tanpa pengaman total — dia hanya dikecualikan
dari *autentikasi API key*. Konsekuensinya, `HttpContext.Items["Caller"]`
tidak terisi, sehingga semua halaman memakai fallback `"SCREEN"` sebagai
`CREATED_BY`/`UPDATED_BY` (pola yang sama seperti fallback `"SYSTEM"` di API).
`RequestResponseMiddleware` tetap mencatat permintaan screen ke tabel log
seperti biasa.

## 3. `_ViewImports.cshtml` — pengaturan yang berlaku untuk semua halaman

```razor
@using cobaproject
@using cobaproject.Dtos
@using cobaproject.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

Isi file ini "disisipkan" ke **setiap** halaman di bawahnya (dan subfolder
`Screen/Products/`), jadi tidak perlu ditulis ulang di tiap halaman:

- `@using ...` — kependekan `using` C#: tanpa ini, `CreateProductRequest`
  di `.cshtml` harus ditulis dengan nama lengkap.
- `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` — **mengaktifkan
  semua tag helper** (`asp-for`, `asp-page`, `asp-validation-*`, dan
  penyisipan token anti-CSRF pada form). Tanpa baris ini, semua `asp-*`
  diperlakukan sebagai atribut HTML biasa dan tidak berfungsi — salah satu
  penyebab "form saya tidak pernah valid/tidak ada token"-yang paling sering.

## 4. `_ViewStart.cshtml` — pemilihan kerangka untuk semua halaman

```razor
@{
    Layout = "_Layout";
}
```

"Setiap halaman memakai `_Layout` sebagai kerangkanya." Razor mencari
`_Layout.cshtml` di folder halaman, lalu di `Pages/Shared/`. Tanpa file ini,
tiap halaman harus menulis `<html><head>...` sendiri dan tampil beda-beda.

## 5. `Shared/_Layout.cshtml` — kerangka semua halaman

Potongan inti (selain CSS):

```html
<header>
    <strong>Product Screen</strong>
    <nav>
        <a asp-page="/Screen/Products/Index">Daftar Produk</a>
        <a asp-page="/Screen/Products/Create">Tambah Produk</a>
    </nav>
</header>
<main>
    @RenderBody()
</main>
...
@await RenderSectionAsync("Scripts", required: false)
```

| Bagian | Arti |
|---|---|
| Navbar dengan `asp-page` | Route absolut (`/Screen/...`) dipakai *di layout* karena layout dipakai banyak halaman — relative path di sini bisa membingungkan. Tag helper tetap menghasilkan URL yang benar. |
| `@RenderBody()` | "Sisipkan isi halaman di sini." Inilah tempat konten `Index.cshtml`, `Create.cshtml`, dst. muncul — layout hanya kerangka. |
| `@await RenderSectionAsync("Scripts", required: false)` | "Kalau ada halaman yang menaruh konten di `@section Scripts` (mis. Create/Edit), render di sini." `required: false` = halaman yang tidak punya section tetap sah. Di sinilah `_ValidationScriptsPartial` halaman Create disuntikkan. |
| `@ViewData["Title"]` di `<title>` | Judul yang diatur tiap halaman (`ViewData["Title"] = "Tambah Produk"`) tampil sebagai judul tab browser. |

## 6. `Shared/_ValidationScriptsPartial.cshtml` — bahan validasi di browser

```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.7.1/jquery.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery-validate/1.19.5/jquery.validate.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery-validation-unobtrusive/4.0.0/jquery.validate.unobtrusive.min.js"></script>
```

Tiga script dari CDN (internet) yang membuat atribut `data-val-*` hasil
`asp-for` (dari `[Required]`, `[Range]`, dll. di DTO) berjalan di browser:

- `jquery` — fondasinya.
- `jquery-validate` — mesin validasi.
- `jquery-validation-unobtrusive` — penghubung: membaca atribut `data-val-*`
  pada input dan menjalankannya tanpa menulis JavaScript manual.

Kalau internet mati, script tidak termuat dan validasi browser tidak aktif —
**validasi server tetap melindungi** (lihat dokumen Create, bagian dua lapis
validasi).

## 7. Alur satu request penuh (semua file bekerja sama)

```
GET /Screen/Products
→ MapRazorPages: cocokkan ke Pages/Screen/Products/Index.cshtml
→ ApiKeyMiddleware: path /screen → dikecualikan → lanjut
→ RequestResponseMiddleware: catat request ke tabel log
→ routing pilih handler GET → OnGetAsync() → service → Model.Products terisi
→ render:
    _ViewStart → Layout = _Layout
    _Layout  → header, <title> dari ViewData, lalu @RenderBody()
    Index.cshtml → @foreach → tabel HTML
→ response dicatat → HTML dikirim ke browser
```

## 8. Peta mental satu layar app

```
Program.cs                    ← nyalakan mesin (AddRazorPages / MapRazorPages)
_ViewImports                  ← aturan bersama (tag helper, using)
_ViewStart                    ← kerangka bersama (Layout)
_Layout                       ← tampilan kerangka (header/footer/CSS)
halaman (Index/Create/Edit/Delete)
    .cshtml.cs = otak        → service (DI)
    .cshtml    = wajah       → @Model + tag helper
```

## 9. Pertanyaan akhir untuk menguji pemahaman menyeluruh

1. Apa yang terjadi bila `@addTagHelper` dihapus dari `_ViewImports`?
2. Kenapa `"/screen"` harus ada di `ExcludedPathPrefixes` ApiKeyMiddleware?
3. Di lokasi mana `@RenderBody()` muncul, dan apa yang dirender di sana?
4. Apa peran `Layout = "_Layout"` di `_ViewStart`?
5. `AddControllers` + `MapRazorPages` hidup berdampingan — buktinya apa
   di aplikasi ini?

Seluruh materi ini merujuk ke kode nyata di folder `Pages/`. Buka file-nya
sambil membaca dokumen — setiap baris yang dijelaskan ada di sana.