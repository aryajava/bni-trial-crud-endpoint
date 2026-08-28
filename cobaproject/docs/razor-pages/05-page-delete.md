# 05 — Halaman Hapus Produk (`Delete`)

Halaman konfirmasi: menunjukkan data produk, lalu meminta kepastian sebelum
menghapus. Halaman ini memperkenalkan **dua tombol submit dalam satu form**
yang memicu **dua handler berbeda** (soft delete vs hard delete).

---

## 1. Kode otaknya — `Pages/Screen/Products/Delete.cshtml.cs`

```csharp
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class DeleteModel : PageModel
{
    private readonly IProductService _productService;

    public DeleteModel(IProductService productService)
    {
        _productService = productService;
    }

    public ProductDto? Product { get; set; }

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var deleted = await _productService.SoftDeleteAsync(id, Caller);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Produk ID {id} dihapus (soft delete).";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostHardAsync(int id)
    {
        var deleted = await _productService.HardDeleteAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Produk ID {id} dihapus permanen (hard delete).";
        return RedirectToPage("Index");
    }
}
```

### Penjelasan bagian per bagian

**`public ProductDto? Product { get; set; }`**

Data produk yang akan dihapus — diisi saat GET, dibaca halaman untuk
konfirmasi. Tanda `?` berarti "boleh kosong"; halaman harus siap dengan
kemungkinan itu (lihat tampilan: ada cabang bila `Product is null`).

**`OnGetAsync(int id)` — tampilkan konfirmasi**

```csharp
var product = await _productService.GetByIdAsync(id);
if (product is null) { return NotFound(); }
Product = product;
return Page();
```

Pola yang sama dengan Edit: ambil data → 404 bila tidak ada → simpan ke
properti → render halaman.

**Dua handler POST — dua jenis hapus**

| Handler | Method service yang dipanggil | Efek di database |
|---|---|---|
| `OnPostAsync` | `SoftDeleteAsync(id, Caller)` | `IS_ACTIVE = 0` — data **tetap ada** di tabel, tapi tidak muncul di daftar (dan API menolak mengambilnya). Seperti "menonaktifkan". |
| `OnPostHardAsync` | `HardDeleteAsync(id)` | `DELETE` baris — data **hilang permanen**. Hanya ini yang tidak bisa dibatalkan. |

Satu form punya dua tombol → dua handler: framework membedakannya lewat
`asp-page-handler` pada tombol (lihat tampilan). Tanpa atribut itu, tombol
memicu handler default `OnPost`.

**Kenapa konfirmasi itu penting?** Halaman ini GET-nya hanya *menampilkan*;
tidak ada yang dihapus sampai POST datang. Artinya penghapusan butuh dua
langkah sadar: buka halaman → tekan tombol. Ini mencegah klik tidak sengaja
dari daftar, dan terutama menyelamatkan dari hard delete yang permanen.

## 2. Kode tampilannya — `Pages/Screen/Products/Delete.cshtml`

```html
@page "{id:int}"
@model cobaproject.Pages.Screen.Products.DeleteModel
@{
    ViewData["Title"] = "Hapus Produk";
}

<h1>Hapus Produk</h1>

@if (Model.Product is null)
{
    <p>Produk tidak ditemukan.</p>
    <a class="btn btn-kedua" asp-page="Index">Kembali ke daftar</a>
}
else
{
    <table>
        <tr><th>ID</th><td>@Model.Product.Id</td></tr>
        <tr><th>Judul</th><td>@Model.Product.Title</td></tr>
        <tr><th>Harga</th><td>Rp @Model.Product.Price.ToString("N0")</td></tr>
        <tr><th>Kategori</th><td>@(Model.Product.Category ?? "-")</td></tr>
        <tr><th>Rating</th><td>@(Model.Product.RatingRate?.ToString("0.0") ?? "-") (@(Model.Product.RatingCount?.ToString() ?? "0"))</td></tr>
    </table>

    <p>Yakin ingin menghapus produk <strong>@Model.Product.Title</strong>?</p>

    <form method="post">
        <button class="btn btn-bahaya" type="submit">Hapus (soft delete)</button>
        <button class="btn btn-bahaya" type="submit" asp-page-handler="Hard">Hapus permanen</button>
        <a class="btn btn-kedua" asp-page="Index">Batal</a>
    </form>
}
```

### Penjelasan bagian per bagian

**Percabangan `@if (Model.Product is null)`**

- Produk ada → tabel data + pertanyaan + tombol.
- Produk tidak ada (mis. sudah dihapus orang lain, atau id salah) → teks
  "Produk tidak ditemukan." + tautan balik. Halaman tidak pernah error.

Sekaligus ini contoh penting: `Page()` di `OnGet` selalu sukses merender —
perbedaan tampilan diatur lewat data (`Product` ada atau tidak), bukan
lewat file halaman lain.

**Dua tombol, satu form**

```html
<form method="post">
    <button type="submit">Hapus (soft delete)</button>
    <button type="submit" asp-page-handler="Hard">Hapus permanen</button>
</form>
```

- Tombol tanpa `asp-page-handler` → POST biasa → `OnPostAsync` (soft).
- Tombol dengan `asp-page-handler="Hard"` → POST dengan tambahan
  `handler=Hard` di body → framework memanggil `OnPostHardAsync`.
- `asp-page-handler` diterjemahkan menjadi `name="handler" value="Hard"`
  pada tombol. Cek Source di browser untuk melihatnya.

Jadi satu URL (`/Screen/Products/Delete/3`) melayani **tiga perilaku**:
menampilkan (GET), hapus nonaktif (POST), hapus permanen (POST handler Hard).
Semua ditentukan oleh method yang dipanggil — bukan oleh URL yang berbeda.

**`<a asp-page="Index">Batal</a>`**

Tombol batal bukan tombol submit — ini link biasa (GET) yang membatalkan
seluruh proses tanpa efek apa pun. Karena bukan POST, tidak ada yang
diubah di database.

## 3. Alur lengkap

```
Klik "Hapus" di Index → GET /Screen/Products/Delete/3 (dibuat oleh asp-route-id)
→ route "{id:int}" → id = 3
→ OnGetAsync(3) → data diambil → halaman konfirmasi tampil
→ user memilih:
    "Hapus (soft delete)"    → POST biasa       → OnPostAsync     → SoftDeleteAsync (IS_ACTIVE = 0)
    "Hapus permanen"         → POST handler=Hard → OnPostHardAsync → HardDeleteAsync (DELETE baris)
    "Batal"                  → link GET         → tidak ada efek
→ sukses → TempData pesan → redirect ke Index → banner hijau
```

## 4. Coba-coba (untuk belajar)

1. Soft delete sebuah produk, lalu cek database (`SELECT ... WHERE ID = ...`)
   — barisnya masih ada, `IS_ACTIVE` berubah jadi `0`. Sekarang coba akses
   via API `GET /products/{id}` — hasil 404, karena service menyaring
   `IS_ACTIVE = 1`.
2. Hard delete produk lain, lalu cek database — barisnya benar-benar hilang.
3. Buka `/Screen/Products/Delete/999999` — muncul "Produk tidak ditemukan.",
   bukan error.
4. Perhatikan Source halaman (Ctrl+U): cari `name="handler"` pada tombol
   permanen — bukti cara `asp-page-handler` bekerja di balik layar.

Lanjut ke [06-layout-dan-konfigurasi.md](06-layout-dan-konfigurasi.md) untuk
bagian yang menyatukan semua halaman.