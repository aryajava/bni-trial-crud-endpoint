# 01 — Dasar: Apa itu `@`, `@page`, `@model`, dan bagaimana halaman bekerja

Sebelum membaca halaman-halaman berikut, baca dokumen ini dulu sampai paham.
Ini adalah "fondasi" yang dipakai di semua halaman screen.

---

## 1. Razor itu mesin pembuat HTML, bukan bahasa baru

File dengan ekstensi `.cshtml` adalah **campuran HTML biasa + potongan C#**.
Saat project di-build, ASP.NET **mengompilasi file itu menjadi satu class C# sungguhan**
(kalau penasaran, hasilnya bisa dilihat di folder `obj/Debug/net10.0/Razor/` saat
project di-build — di situ "yang ajaib" berubah jadi kode biasa).

Cara kompiler tahu mana HTML dan mana C# adalah lewat **tanda `@`**.

> **`@` = penanda "mulai kode C# di sini".** Tanpa `@`, isi file dianggap HTML
> mentah yang ditulis apa adanya.

Contoh di halaman Index:

```html
<h1>Daftar Produk</h1>          ← HTML biasa, ditulis apa adanya
<td>@p.Id</td>                  ← @p.Id = "tulis nilai p.Id di sini" (dijalankan C# dulu)
```

Satu hal penting: output Razor **otomatis di-escape** untuk keamanan. Kalau misalnya
`p.Title` berisi `<script>`, browser akan menampilkannya sebagai teks, bukan
menjalankannya. Ini proteksi bawaan dari serangan XSS.

## 2. Directive: perintah ke kompiler

Directive selalu ditulis di **baris paling atas** file (sebelum HTML apa pun),
dimulai dengan `@`:

| Directive | Arti | Dipakai di |
|---|---|---|
| `@page` | **"File ini adalah halaman (endpoint) yang bisa dibuka lewat URL."** Tanpa ini, file hanya dianggap potongan tampilan yang tidak bisa diakses langsung | Semua 4 halaman (wajib!) |
| `@page "{id:int}"` | Sama seperti `@page`, plus: "URL-nya harus diakhiri angka, misal `/Edit/5`" | Halaman Edit & Delete |
| `@model NamaTipe` | "Tipe C# yang menjadi pasangan otak halaman ini" (class `.cshtml.cs`) | Semua 4 halaman |
| `@{ ... }` | Blok kode C# yang dijalankan, tidak menulis apa pun ke HTML | `_ViewStart` |

Penting untuk dipahami:

- `@page` **wajib ada** di tiap halaman. File `.cshtml` di dalam folder `Pages/`
  yang tidak punya `@page` tetap ikut ter-kompilasi, tapi tidak bisa dibuka lewat URL
  — dia menjadi "bahan baku" (partial/layout). Contoh: `_Layout.cshtml` tanpa `@page`.
- `@model` hanya *memberi tahu kompiler tipe datanya* — tidak membuat instance.
  Halaman dan otaknya dihubungkan-otomatis oleh framework dari nama file yang sama
  (`Index.cshtml` ↔ `Index.cshtml.cs`).

## 3. Satu halaman = dua file yang menempel

```
Index.cshtml        ← WAJAH: HTML + @Model (tampilan)
Index.cshtml.cs     ← OTAK: C# murni (mengambil data, menyimpan data)
```

Nama file harus sama persis. Framework yang menyambungkan keduanya: saat request
datang, dia membuat instance class `IndexModel`, mengisi propertinya, lalu
menyerahkan instance itu ke `Index.cshtml` sebagai `Model`.

- `.cshtml` hanya boleh **membaca** (`@Model.Products`) dan menampilkan.
- `.cshtml.cs` yang berisi logika: panggil service, validasi, redirect.

Kenapa dipisah? Supaya tampilan tidak berantakan dengan logika, dan logika
bisa dites seperti kode biasa.

## 4. GET vs POST — dua jenis kunjungan browser

Setiap kali halaman dibuka, browser datang dengan salah satu "jenis kunjungan"
(HTTP method). Dua yang paling penting:

| Kunjungan | Kapan terjadi | Handler yang dijalankan |
|---|---|---|
| **GET** | Mengetik URL, mengklik link (`<a>`), refresh | `OnGet()` atau `OnGetAsync()` |
| **POST** | Menekan tombol submit di form | `OnPost()` atau `OnPostAsync()` |

Otaknya (`.cshtml.cs`) punya method untuk masing-masing. Nama method inilah
**"router"-nya — bukan URL**. URL yang sama bisa melayani dua perilaku berbeda:

- `GET /Screen/Products/Create` → `OnGet` → tampilkan form kosong.
- `POST /Screen/Products/Create` → `OnPost` → simpan data ke database.

Analoginya: pintu yang sama di restoran — siang harinya buat makan (GET),
malamnya buat kirim barang (POST). Letak pintunya sama, isi tujuannya beda.

Aturan pengingat yang berguna:

- `OnGet` bertanggung jawab **menyiapkan tampilan** (ambil data, isi properti).
- `OnPost` bertanggung jawab **memproses data** yang dikirim user (simpan/ubah/hapus).

## 5. Bagaimana data form masuk ke C# (model binding)

Saat form dikirim (POST), isinya adalah pasangan `name=value` — misalnya
`Request.Title=Kemeja` dan `Request.Price=150000`.

Framework lalu melakukan **model binding**: mencocokkan nama field form dengan
properti C# (abaikan besar/kecil huruf):

```
input name="Request.Title"  →  properti Request.Title
input name="Request.Price"  →  properti Request.Price
```

Di sinilah peran **tag helper `asp-for`**. Tag helper adalah atribut `asp-*`
yang "dibaca" saat halaman di-render di server, lalu **ditulis ulang menjadi
HTML lengkap**:

```html
<input asp-for="Request.Title" />
```

menjadi (yang benar-benar dikirim ke browser):

```html
<input type="text" id="Request_Title" name="Request.Title" value="...">
```

Jadi kamu tidak perlu menulis `name` sendiri — `asp-for` yang membuatnya, dan
pasti cocok dengan properti C#-nya. Trik ini juga menjaga konsistensi: kalau
nama properti diubah di C#, form ikut berubah; kalau `[Required]` ditambahkan
di DTO, `asp-for` otomatis menambahkan atribut validasi di browser.

Selain itu, form yang memakai tag helper **otomatis diberi token anti-CSRF**
(field tersembunyi). Token ini dicek server di setiap POST — proteksi bawaan
supaya halaman tidak bisa "diposting" oleh situs asing. Form yang ditulis manual
`<form method="post">` tanpa tag helper **tidak** punya token ini dan akan
ditolak server — salah satu kesalahan paling umum.

## 6. Alur lengkap satu halaman (hafalkan urutannya)

```
1. Browser: GET /Screen/Products
2. Routing (dari letak file): Pages/Screen/Products/Index.cshtml terpilih
3. File punya @page       → sah sebagai halaman
4. Method GET             → framework memanggil OnGetAsync()
5. OnGetAsync memanggil service → data ditarik dari database
6. Data ditaruh di properti (Model.Products)
7. Return Page()          → framework merender Index.cshtml jadi HTML
8. HTML dikirim ke browser
9. PageModel & halaman dibuang → request selesai
```

Untuk POST urutannya sama, hanya di langkah 4-6 data form diikat dulu, divalidasi,
lalu disimpan, dan biasanya diakhiri redirect.

## 7. Hal yang WAJIB diingat saat membaca halaman berikutnya

1. `@page` di atas, `@model` di bawahnya — urutan itu.
2. `OnGet` = siapkan tampilan, `OnPost` = proses data.
3. `asp-for` = "buatkan input yang namanya cocok dengan properti ini".
4. `asp-page` = "buatkan link yang URL-nya benar menuju halaman ini".
5. `ModelState.IsValid` = "apakah semua aturan DataAnnotations (mis. `[Required]`)
   pada DTO lolos?" — cek wajib di setiap POST.
6. `TempData` = kotak pesan yang bertahan satu kali kunjungan berikutnya
   (dipakai untuk pesan "berhasil disimpan").
7. `Page()` = render halaman ini lagi; `RedirectToPage("Index")` = suruh browser
   pindah ke halaman lain; `NotFound()` = balas 404.

## 8. Tes pemahaman cepat

Bisakah menjawab tanpa melihat dokumentasi?

1. Apa perbedaan file `Index.cshtml` dan `Index.cshtml.cs`?
2. Saat user mengklik tombol submit pada form, method mana yang dijalankan?
3. Kenapa semua halaman harus punya `@page` di baris pertama?
4. Apa yang terjadi kalau form ditulis tanpa tag helper (HTML manual)?
5. Siapa yang memastikan `name="Request.Title"` cocok dengan properti C#?

Jawaban ada tersebar di dokumen ini — kalau sudah bisa menjawab kelima
pertanyaan, lanjut ke [02-page-index.md](02-page-index.md).