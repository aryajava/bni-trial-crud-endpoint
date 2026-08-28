# 03 — Halaman Tambah Produk (`Create`)

Halaman form: user mengisi judul, harga, dll., menekan "Simpan", dan data
masuk ke database. Halaman ini adalah contoh paling lengkap dari **form +
model binding + validasi + redirect** — inti dari CRUD.

---

## 1. Kode otaknya — `Pages/Screen/Products/Create.cshtml.cs`

```csharp
using cobaproject.Dtos;
using cobaproject.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace cobaproject.Pages.Screen.Products;

public class CreateModel : PageModel
{
    private readonly IProductService _productService;

    public CreateModel(IProductService productService)
    {
        _productService = productService;
    }

    [BindProperty]
    public CreateProductRequest Request { get; set; } = new();

    private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var created = await _productService.CreateAsync(Request, Caller);
        if (created is null)
        {
            ModelState.AddModelError(string.Empty, "Gagal membuat produk.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Produk \"{created.Title}\" berhasil dibuat (ID {created.Id}).";
        return RedirectToPage("Index");
    }
}
```

### Penjelasan bagian per bagian

**`[BindProperty]` di atas properti `Request`**

Ini perintah: "saat ada POST, isi `Request` dari field form yang namanya cocok
(`Request.Title`, `Request.Price`, dst.)". Perhatikan:

- Binding hanya terjadi **saat POST** — saat GET (halaman dibuka pertama kali),
  `Request` tetap form kosong. Itu perilaku default dan memang yang diinginkan.
- DTO yang diikat adalah `CreateProductRequest` — **kelas yang sama** dipakai
  endpoint API `POST /products`. Jadi aturan validasinya (`[Required]`,
  `[StringLength]`) otomatis ikut berlaku di screen, tanpa ditulis ulang.

**`private string Caller => HttpContext.Items["Caller"]?.ToString() ?? "SCREEN";`**

Nama orang yang melakukan aksi, untuk kolom `CREATED_BY` di database. Pada
API, middleware `ApiKeyMiddleware` mengisi `Caller` dari header `X-Api-Key`.
Halaman screen (yang dikecualikan dari API key) tidak punya itu, jadi diberi
fallback `"SCREEN"`. Pola ini sama dengan fallback `"SYSTEM"` di
`ProductsController`.

**`OnGet()` — kosong**

Handler GET tidak perlu melakukan apa pun: form kosong sudah cukup. (Method
tetap ada supaya jelas: "saat dibuka, tampilkan form kosong".)

**`OnPostAsync()` — inti proses**

```csharp
if (!ModelState.IsValid)
{
    return Page();
}
```

"Apakah semua aturan validasi pada data form lolos?" (judul terisi? harga
bukan angka negatif? dll.)

- Lolos → lanjut menyimpan.
- Gagal → **`return Page()`**: render halaman ini **lagi**. Keajaibannya:
  field yang sudah diisi user **tetap terisi** (itu hasil model binding tadi)
  dan pesan error tampil di dekat field yang bermasalah — semua otomatis,
  tidak ada kode ekstra.

```csharp
var created = await _productService.CreateAsync(Request, Caller);
```

Simpan data — memanggil service yang sama dengan API `POST /products`.
`_productService` datang lewat constructor injection (DI), sama seperti API.

```csharp
if (created is null)
{
    ModelState.AddModelError(string.Empty, "Gagal membuat produk.");
    return Page();
}
```

`AddModelError(string.Empty, ...)` = "tambahkan error yang tidak menempel ke
field mana pun" — ditampilkan oleh `asp-validation-summary` di halaman.
Kalau simpan gagal, user melihat pesan merah dan form-nya (bersama isiannya)
tetap tampil.

```csharp
TempData["SuccessMessage"] = $"Produk \"{created.Title}\" berhasil dibuat (ID {created.Id}).";
return RedirectToPage("Index");
```

- `TempData` = kotak pesan sekali pakai (dibaca halaman Index berikutnya).
- `RedirectToPage("Index")` = "suruh browser pindah ke halaman Index".
  Pola ini (Post-Redirect-Get) penting: kalau user menekan F5 setelah
  sukses, yang di-refresh adalah halaman Index (GET), **bukan** POST ulang —
  jadi data tidak tersimpan dua kali.

## 2. Kode tampilannya — `Pages/Screen/Products/Create.cshtml`

```html
@page
@model cobaproject.Pages.Screen.Products.CreateModel
@{
    ViewData["Title"] = "Tambah Produk";
}

<h1>Tambah Produk</h1>

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

    <div class="tombol">
        <button class="btn btn-utama" type="submit">Simpan</button>
        <a class="btn btn-kedua" asp-page="Index">Batal</a>
    </div>
</form>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

### Penjelasan bagian per bagian

**`<form class="kartu" method="post">`**

- `method="post"` = "saat submit, datanglah sebagai POST" → nanti memicu
  `OnPostAsync`.
- Karena `form` memakai tag helper (di sini cukup `asp-page` tidak dipakai,
  URL saat ini sudah benar — namun kehadiran `@addTagHelper` di `_ViewImports`
  membuat form ini diperlakukan sebagai tag helper), **token anti-CSRF
  otomatis disisipkan** dan server otomatis memvalidasinya. Coba lihat
  Source (Ctrl+U) halaman ini di browser — ada `<input type="hidden"
  name="__RequestVerificationToken">`. Itulah tokennya.

**`<div asp-validation-summary="All">`**

"Tampilkan semua pesan error yang tidak menempel ke field tertentu" — di
sinilah pesan "Gagal membuat produk" dari `AddModelError(string.Empty, ...)`
muncul.

**Pola tiga serangkai untuk tiap field**

```html
<label asp-for="Request.Title"></label>          ← teks label otomatis dari nama properti
<input asp-for="Request.Title" />                ← input dengan name yang cocok
<span asp-validation-for="Request.Title"></span> ← kotak error khusus field ini
```

Bagaimana cara kerjanya digabung:

1. `asp-for` menentukan `name` dan tipe input (mis. `Price` → `type="number"`).
2. Saat POST, binder memetakan `name` → properti `Request.*`.
3. Jika validasi gagal, `asp-validation-for` menampilkan pesan di span-nya.
4. Dan karena `_ValidationScriptsPartial` disertakan (lihat bagian bawah),
   validasi juga berjalan **di browser** sebelum data dikirim.

**Dua lapis validasi (perhatikan baik-baik)**

| Lapis | Kapan | Disediakan oleh |
|---|---|---|
| Server | Setiap POST, pasti jalan | DataAnnotations di `CreateProductRequest` + `ModelState.IsValid` |
| Browser | Saat user mengetik (tanpa kirim dulu) | Script jQuery Validation dari `_ValidationScriptsPartial` (CDN) |

Lapis browser hanya *pembantu* (hemat waktu, feedback cepat). Lapis server
adalah penjaga sebenarnya. Kalau internet mati, CDN tidak termuat, browser
tidak memvalidasi — tapi server tetap memblokir data buruk. Itu sebabnya
keduanya ada dan **sumber aturannya satu**: atribut di DTO.

**`@section Scripts { <partial name="_ValidationScriptsPartial" /> }`**

`@section` = "tempelkan potongan ini ke tempat khusus bernama `Scripts` yang
sudah disiapkan `_Layout` (di baris `@await RenderSectionAsync("Scripts", ...)`)".
Script validasi hanya dimuat di halaman yang butuh form — tidak membebani
halaman Index.

## 3. Alur lengkap menekan tombol "Simpan"

```
User mengisi form → klik Simpan
→ browser cek validasi (lapis 1) — gagal? blokir + tampilkan pesan
→ jika lolos: POST dikirim (form + token anti-CSRF)
→ binder mengisi Request dari field form (model binding)
→ ModelState.IsValid dicek (lapis 2) — gagal? render ulang form + pesan
→ CreateAsync dipanggil → INSERT ke MASTER_PRODUCT (CREATED_BY = "SCREEN")
→ TempData diisi pesan sukses
→ RedirectToPage("Index") → browser pindah ke daftar produk
→ Index membaca TempData → banner hijau tampil sekali
```

## 4. Coba-coba (untuk belajar)

1. **Klik Simpan dengan judul kosong** — browser mencegahnya dulu (kalau CDN
   termuat). Matikan internet lalu coba lagi: browser lolos, server yang
   menolak. Bukti validasi dua lapis.
2. **Setelah sukses, tekan F5 di halaman daftar** — tidak ada duplikasi data,
   karena POST sudah selesai dan yang di-refresh hanya GET. Itulah gunanya
   Post-Redirect-Get.
3. Hapus `if (!ModelState.IsValid)` lalu submit judul kosong — data buruk
   masuk ke database. Inilah bahaya form tanpa cek validasi server.
4. Tulis form manual `<form method="post">` tanpa tag helper apa pun (buat
   input pakai `name` manual), lalu submit — dapat 400 karena token
   anti-CSRF tidak ada.

Lanjut ke [04-page-edit.md](04-page-edit.md).