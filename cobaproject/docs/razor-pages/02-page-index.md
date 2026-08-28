# 02 — Halaman Daftar Produk (`Index`)

Halaman yang tampil pertama kali di `/Screen/Products`: tabel semua produk
yang aktif, plus tombol Edit dan Hapus di tiap baris.

---

## 1. Kode otaknya — `Pages/Screen/Products/Index.cshtml.cs`

```csharp
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class IndexModel : PageModel
{
    private readonly IProductService _productService;

    public List<ProductDto> Products { get; set; } = [];

    public IndexModel(IProductService productService)
    {
        _productService = productService;
    }

    public async Task OnGetAsync()
    {
        Products = (await _productService.GetAllAsync()).ToList();
    }
}
```

### Penjelasan baris demi baris

| Bagian | Arti kira-kira |
|---|---|
| `IndexModel : PageModel` | "Class ini adalah otak halaman Index." Semua otak halaman harus menurunkan `PageModel` — dari sini datang fasilitas bawaan seperti `ModelState`, `Page()`, `TempData`. |
| `IProductService _productService` | Simpan "pelayan data" di dalam otak. Service ini yang sama persis dipakai `ProductsController` API — **screen dan API berbagi satu logika bisnis**. |
| `public List<ProductDto> Products { get; set; } = [];` | Kotak tempat menaruh hasil yang nanti dibaca halaman `Index.cshtml`. Awalan `[]` = "kalau belum diisi apa-apa, isi dengan daftar kosong" (biar halaman tidak error saat belum ada data). |
| `public IndexModel(IProductService productService)` | **Constructor injection** (DI). Framework-lah yang memanggil constructor ini dan menyerahkan service-nya — kamu tidak perlu membuat service sendiri. Ini persis mekanisme yang sama dengan `ProductsController`. |
| `OnGetAsync()` | Handler untuk kunjungan GET: "saat halaman diminta lewat URL, lakukan ini". `Async` karena ambil data dari database adalah pekerjaan menunggu (I/O). |
| `Products = (await ...GetAllAsync()).ToList();` | Panggil service → tunggu hasilnya → masukkan ke kotak `Products` → nanti dibaca halaman `Index.cshtml`. |

Catatan: method ini bertipe `void` (tidak `return`), artinya setelah selesai,
framework otomatis merender halaman. Ini konvensi untuk handler GET murni.

## 2. Kode tampilannya — `Pages/Screen/Products/Index.cshtml`

```html
@page
@model cobaproject.Pages.Screen.Products.IndexModel
@{
    ViewData["Title"] = "Daftar Produk";
}

<h1>Daftar Produk</h1>

@if (TempData["SuccessMessage"] is string pesan)
{
    <div class="pesan-sukses">@pesan</div>
}

<p><a class="btn btn-utama" asp-page="Create">+ Tambah Produk</a></p>

<table>
    <thead>
        <tr>
            <th>ID</th> <th>Judul</th> <th>Harga</th>
            <th>Kategori</th> <th>Rating</th> <th>Versi</th> <th>Aksi</th>
        </tr>
    </thead>
    <tbody>
        @if (Model.Products.Count == 0)
        {
            <tr><td colspan="7">Belum ada produk. Klik "Tambah Produk" untuk mulai.</td></tr>
        }
        @foreach (var p in Model.Products)
        {
            <tr>
                <td>@p.Id</td>
                <td>@p.Title</td>
                <td>Rp @p.Price.ToString("N0")</td>
                <td>@(p.Category ?? "-")</td>
                <td>@(p.RatingRate?.ToString("0.0") ?? "-") (@(p.RatingCount?.ToString() ?? "0"))</td>
                <td>@p.Version</td>
                <td>
                    <a class="btn btn-kedua" asp-page="Edit" asp-route-id="@p.Id">Edit</a>
                    <a class="btn btn-bahaya" asp-page="Delete" asp-route-id="@p.Id">Hapus</a>
                </td>
            </tr>
        }
    </tbody>
</table>
```

### Penjelasan bagian per bagian

**Dua baris pertama (directive)**

```
@page                     ← "saya halaman, bisa dibuka lewat URL"
@model ...IndexModel      ← "otak saya adalah class IndexModel"
```

**`@if (TempData["SuccessMessage"] is string pesan)`**

Saat halaman Edit/Create sukses menyimpan, mereka menaruh pesan di `TempData`
lalu mengarahkan browser ke halaman ini (lihat dokumen Create). Di sini pesan
itu diambil dan ditampilkan sebagai kotak hijau.

- `is string pesan` adalah pola C#: "kalau isinya memang string, masukkan ke
  variabel `pesan`".
- `TempData` kosong di luar kondisi itu — jadi banner hanya muncul sekali,
  tepat setelah aksi berhasil. Segar, tidak numpuk.

**Tombol Tambah**

```html
<a class="btn btn-utama" asp-page="Create">+ Tambah Produk</a>
```

`asp-page="Create"` — tag helper link: "bikin link ke halaman Create **yang
berada di folder yang sama**". Di-render menjadi `href="/Screen/Products/Create"`.
Kamu tidak menulis URL manual — kalau halaman dipindah folder, URL otomatis
ikut berubah.

**Loop tabel**

```html
@foreach (var p in Model.Products)
```

"Ulangi untuk setiap data di daftar `Products`" — di dalamnya, `p` adalah satu
produk. Tiga bentuk ekspresi yang dipakai di dalam:

| Ekspresi | Arti |
|---|---|
| `@p.Id`, `@p.Title` | Tulis nilai properti produk |
| `@p.Price.ToString("N0")` | Tulis harga dengan format ribuan (`"N0"`) |
| `@(p.Category ?? "-")` | Kalau `Category` kosong (null), tulis `-` — tanda kurung `()` di `@(...)` = "hitung ekspresi ini dulu, baru tulis hasilnya" |
| `@(p.RatingRate?.ToString("0.0") ?? "-")` | Rating 1 angka desimal, atau `-` kalau belum ada |

**Tombol Edit & Hapus per baris**

```html
<a asp-page="Edit" asp-route-id="@p.Id">Edit</a>
```

Dua tag helper sekaligus: `asp-page="Edit"` (arah ke halaman Edit se-folder)
dan `asp-route-id="@p.Id"` (tambahkan nilai id ke URL). Hasilnya di browser:

```html
<a href="/Screen/Products/Edit/3">Edit</a>
```

Inilah yang membuat halaman Edit tahu produk mana yang mau diedit: angka di
URL `.../Edit/3` → diikat ke parameter `id` pada `OnGetAsync(int id)`
(dibahas di dokumen Edit).

**Baris kosong**

```html
@if (Model.Products.Count == 0) { ... }
```

"Kalau tidak ada produk sama sekali, tampilkan baris pemberitahuan" —
mencegah tabel tampil kosong tanpa penjelasan.

## 3. Alur ketika browser membuka `/Screen/Products`

```
1. Routing cocokkan URL → Pages/Screen/Products/Index.cshtml
2. @page → sah sebagai halaman
3. GET → panggil OnGetAsync()
4. Service → GetAllAsync() → SELECT ... WHERE IS_ACTIVE = 1 (hanya produk aktif)
5. Hasil dimasukkan ke Model.Products
6. Render Index.cshtml:
     - tiap data jadi satu baris <tr>
     - link Edit menjadi /Screen/Products/Edit/{id}
7. HTML dikirim → browser menampilkan tabel
```

## 4. Coba-coba (untuk belajar)

1. Ubah `WHERE IS_ACTIVE = 1` (di `ProductService.GetAllAsync`) menjadi tanpa
   filter — halaman akan menampilkan produk yang pernah di-soft-delete.
   (Ingat, ini mengubah perilaku API juga, karena service dipakai bersama.)
2. Ganti `@p.Price.ToString("N0")` dengan `@p.Price` lalu refresh — lihat
   formatnya berubah.
3. Pindahkan file `Index.cshtml` ke folder lain (mis. `Pages/Screen/`) —
   URL-nya ikut berubah, dan link `asp-page="Create"` otomatis mengikuti
   folder baru tempat `Create` berada.
4. Hapus baris `@page` lalu build — aplikasi gagal kompilasi dengan pesan
   yang menunjuk file ini (karena file Pages tanpa halaman yang valid).

Lanjut ke [03-page-create.md](03-page-create.md) untuk form pertama.