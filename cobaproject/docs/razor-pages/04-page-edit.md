# 04 — Halaman Edit Produk (`Edit`)

Seperti halaman Create, tapi datanya **sudah ada** — form harus diisi dulu
dengan data lama, dan simpanannya harus menyentuh produk yang benar.
Ini memperkenalkan tiga hal baru: route `{id}`, field tersembunyi, dan
pertarungan versi (optimistic concurrency).

---

## 1. Kode otaknya — `Pages/Screen/Products/Edit.cshtml.cs`

```csharp
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class EditModel : PageModel
{
    private readonly IProductService _productService;

    public EditModel(IProductService productService)
    {
        _productService = productService;
    }

    public int Id { get; set; }

    [BindProperty]
    public UpdateProductRequest Request { get; set; } = new();

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        Id = id;
        Request = CopyFrom(product);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (product, isConflict) = await _productService.UpdateAsync(id, Request, Caller);
        if (product is null)
        {
            return NotFound();
        }

        if (isConflict)
        {
            ModelState.AddModelError(string.Empty,
                $"Produk sudah diubah orang lain (versi sekarang {product.Version}). " +
                "Form di bawah sudah diperbarui dengan data terbaru — periksa lalu simpan lagi.");
            Request = CopyFrom(product);
            return Page();
        }

        TempData["SuccessMessage"] = $"Produk \"{product.Title}\" berhasil disimpan.";
        return RedirectToPage("Index");
    }

    private static UpdateProductRequest CopyFrom(ProductDto product)
    {
        return new UpdateProductRequest
        {
            Title = product.Title,
            Price = product.Price,
            Description = product.Description,
            Category = product.Category,
            Image = product.Image,
            RatingRate = product.RatingRate,
            RatingCount = product.RatingCount,
            Version = product.Version
        };
    }
}
```

### Penjelasan bagian per bagian

**`public int Id { get; set; }`**

Disimpan ke properti (bukan hanya parameter method) supaya halaman `.cshtml`
bisa menampilkannya (`Edit Produk #@Model.Id` di judul).

**`OnGetAsync(int id)` — pengisian awal form**

```csharp
var product = await _productService.GetByIdAsync(id);
if (product is null) { return NotFound(); }

Id = id;
Request = CopyFrom(product);
return Page();
```

1. Ambil data produk dari database.
2. Kalau tidak ada (mis. URL `Edit/999`), balas **404** (`NotFound()`).
3. Salin data ke `Request` — form akan tampil sudah terisi.
4. `CopyFrom` adalah pemetaan manual dari `ProductDto` → `UpdateProductRequest` —
   tabelnya identik, jadi ini hanya memindahkan nilai. (Kalau bentuknya beda,
   ini tempat yang tepat untuk pakai mapper.)

**Kenapa `id` bisa masuk ke parameter `OnGetAsync`?**

Karena route halaman ditulis `@page "{id:int}"` (lihat tampilan di bawah).
Bagian `{id:int}` berarti: "segmen terakhir URL adalah angka, dan namanya `id`".
URL `/Screen/Products/Edit/5` → `id = 5`. Ini model binding dari **route**,
bukan dari form — berlaku di GET maupun POST.

**`OnPostAsync(int id)` — simpan perubahan**

```csharp
var (product, isConflict) = await _productService.UpdateAsync(id, Request, Caller);
```

Service mengembalikan dua nilai sekaligus (`tuple`): hasil update (atau null)
dan bendera `isConflict`. Arti bendera ini penting untuk dipahami —
lanjut ke bagian "Pertarungan versi" di bawah.

```csharp
if (product is null) { return NotFound(); }
```

Produk dihapus orang lain di tengah proses edit → 404.

```csharp
if (isConflict)
{
    ModelState.AddModelError(string.Empty, "...");
    Request = CopyFrom(product);
    return Page();
}
```

Produk diubah orang lain → tampilkan peringatan, **isi ulang form dengan data
terbaru dari database** (termasuk nomor versi terbaru), dan jangan simpan.
User cukup menekan "Simpan" lagi jika setuju dengan data itu.

## 2. Pertarungan versi (optimistic concurrency) — konsep yang penting

Lihat `ProductService.UpdateAsync`:

```sql
UPDATE LOSCONSUMER.MASTER_PRODUCT
SET    ... , VERSION = VERSION + 1
WHERE  ID = @Id AND VERSION = @Version AND IS_ACTIVE = 1;
```

SQL di atas hanya mengubah baris **jika nomor versinya masih cocok**. Kenapa?

Bayangkan dua orang (atau kamu + temanmu lewat Swagger) membuka Edit produk
yang sama bersamaan. Keduanya membawa `Version = 1`. A menekan Simpan →
versi jadi 2. B menekan Simpan → syarat `VERSION = 1` sudah tidak cocok →
**0 baris berubah** → service melaporkan `isConflict = true` → halaman Edit
menampilkan peringatan, bukan diam-diam menimpa pekerjaan A.

Inilah makna field tersembunyi `Version` di form (lihat tampilan):
user tidak perlu melihat/mengubahnya, tapi nilainya **ikut dikirim** saat
POST sehingga SQL punya nomor untuk dibandingkan.

## 3. Kode tampilannya — `Pages/Screen/Products/Edit.cshtml`

```html
@page "{id:int}"
@model cobaproject.Pages.Screen.Products.EditModel
@{
    ViewData["Title"] = "Edit Produk";
}

<h1>Edit Produk #@Model.Id</h1>

<form class="kartu" method="post">
    <div asp-validation-summary="All" class="pesan-error"></div>

    <label asp-for="Request.Title"></label>
    <input asp-for="Request.Title" />
    <span asp-validation-for="Request.Title" class="field-error"></span>

    <label asp-for="Request.Price"></label>
    <input asp-for="Request.Price" />
    <span asp-validation-for="Request.Price" class="field-error"></span>

    <label asp-for="Request.Description"></label>
    <textarea asp-for="Request.Description" rows="3"></textarea>

    <label asp-for="Request.Category"></label>
    <input asp-for="Request.Category" />
    <span asp-validation-for="Request.Category" class="field-error"></span>

    <label asp-for="Request.Image"></label>
    <input asp-for="Request.Image" />

    <label asp-for="Request.RatingRate"></label>
    <input asp-for="Request.RatingRate" />

    <label asp-for="Request.RatingCount"></label>
    <input asp-for="Request.RatingCount" />

    <input type="hidden" asp-for="Request.Version" />

    <div class="tombol">
        <button class="btn btn-utama" type="submit">Simpan</button>
        <a class="btn btn-kedua" asp-page="Index">Batal</a>
    </div>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

### Perbedaan vs halaman Create

| Perbedaan | Arti |
|---|---|
| `@page "{id:int}"` | Route halaman butuh angka: `Edit/5`, bukan `Edit`. |
| `<h1>Edit Produk #@Model.Id</h1>` | Menampilkan id dari properti `Id`. |
| `<input type="hidden" asp-for="Request.Version" />` | Field tak terlihat yang membawa nomor versi ikut di-POST — bahan perbandingan SQL di atas. DTO `UpdateProductRequest` mewajibkan `Version` (`[Required]`), jadi tanpa field ini validasi akan gagal di server. |
| `Request.Version` | Diisi `CopyFrom(product)` saat GET → user tidak perlu menyentuhnya. |

Selain itu bentuknya sama persis dengan Create — itulah konsistensi CRUD:
halaman Create dan Edit hanya beda "membawa data awal atau tidak" dan
"berapa versi id-nya".

## 4. Alur lengkap

```
Edit link di Index → GET /Screen/Products/Edit/3 (dibuat oleh asp-route-id)
→ route "{id:int}" → id = 3
→ OnGetAsync(3) → GetByIdAsync(3) → salin ke Request → form terisi, Version=1
→ user ubah harga → Simpan
→ POST /Screen/Products/Edit/3 (form + id di URL + Version + token CSRF)
→ binder: Request diisi, parameter id = 3
→ ModelState valid? → UpdateAsync(3, ...) → UPDATE ... WHERE VERSION = 1
→ cocok → versi jadi 2 → pesan sukses → redirect ke Index
```

Skenario konflik: simpan dengan `Version` lama setelah orang lain update →
`rowsAffected = 0` → `isConflict = true` → peringatan + form diisi ulang
dengan data terbaru (versionsnya pun diperbarui).

## 5. Coba-coba (untuk belajar)

1. Buka halaman Edit suatu produk di browser, lalu melalui Swagger lakukan
   `PUT /products/{id}` dengan `version` lama. Kembali ke browser, ubah
   judul, Simpan → kamu melihat peringatan konflik dan form ter-refresh.
2. Hapus `<input type="hidden" asp-for="Request.Version" />` lalu submit —
   validasi server gagal karena `Version` wajib ada (pesan merah muncul).
3. Buka langsung `/Screen/Products/Edit/999999` — halaman 404 ditampilkan.

Lanjut ke [05-page-delete.md](05-page-delete.md).