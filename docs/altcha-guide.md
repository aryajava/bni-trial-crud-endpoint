# Panduan ALTCHA - Verifikasi Keamanan Self-Hosted

ALTCHA (_Alternative CAPTCHA_) adalah perlindungan bot berbasis **proof-of-work** (PoW) yang
sepenuhnya berjalan di aplikasi sendiri. Tidak seperti reCAPTCHA Google, tidak ada panggilan
keluar ke pihak ketiga, tidak ada cookie, tidak ada pelacakan, dan tidak butuh internet.
Cocok untuk lingkungan trial/offline seperti proyek ini.

## Cara kerja (ringkas)

```
Server (aplikasi ini)                          Browser (widget altcha-widget)
──────────────────────                         ─────────────────────────────
1. Buat challenge:      ── JSON ─────────────▶ 2. Terima {algorithm, challenge,
   salt acak + expiry,                            salt, signature}
   challenge = SHA256(salt + number),
   signature  = HMAC(key, challenge)
                                              3. Cari number dengan mengulang
                                                 SHA256(salt + n) sampai cocok
                                                 (butuh CPU ~0.5-2 detik)
4. Verifikasi payload:  ◀─── payload (base64) ─ 5. Kirim {algorithm, challenge,
   cek expiry, hitung                              salt, signature, number}
   ulang SHA256 + HMAC, bandingkan
6. Lolos/gagal
```

Inti keamanannya: browser harus "membayar" kerja komputasi; server membuktikan hasilnya
hanya dengan satu perhitungan hash + satu HMAC (sangat murah). Tanpa interaksi user.

## Apa yang terpasang di proyek ini

| Benda | Lokasi | Keterangan |
|---|---|---|
| Logika challenge + verifikasi | `cobaproject/Helpers/Altcha.cs` | ~120 baris, SHA-256 + HMACSHA256, tanpa dependensi eksternal |
| Widget (web component) | `cobaproject/wwwroot/js/altcha.min.js` | Versi dist ruil 2.3.0, dimuat lokal (butuh `app.UseStaticFiles()` di `Program.cs` agar tersaji) |
| File bahasa Indonesia | `cobaproject/wwwroot/js/altcha.lang.id.js` | Belum dipakai; lihat "i18n" di bawah |
| Endpoin challenge | `/Checkout?handler=Altcha` | Mengembalikan JSON challenge baru per permintaan |
| Pengaturan | `APP_SETTING.ALTCHA_HMAC_KEY` | Kosong = nonaktif; minimal 16 karakter = aktif |
| Halaman pengaturan | Panel → Pengaturan Aplikasi (SA) | Input kunci rahasia + tombol Simpan |
| Pemakaian | Modal "Konfirmasi Pesanan" di `/Checkout` | Password + widget ALTCHA di bawahnya, tombol baru aktif setelah widget "verified" |

## Alur di halaman Checkout

1. User mengisi data pengiriman lalu klik **Buat Pesanan**.
2. Modal Bootstrap **Konfirmasi Pesanan** terbuka: field password + slot widget ALTCHA
   (bila kunci rahasia sudah diatur).
3. Saat modal dibuka (`show.bs.modal`), JavaScript membuat elemen
   `<altcha-widget challenge="/Checkout?handler=Altcha" type="checkbox">` di dalam slot.
   Widget otomatis mengambil challenge baru (satu challenge per bukaan modal, jadi selalu
   segar - challenge expired dalam 120 detik).
4. User mencentang kotak verifikasi → browser menyelesaikan PoW → tombol
   **Ya, Buat Pesanan** aktif (event `statechange` state `verified`).
5. Submit → form mengirim field `altcha` (payload) + `Password` + data pengiriman.
6. Server: bila `AltchaAktif`, verifikasi payload via `Altcha.Verify(kunci, payload)` dan
   kata sandi via `VerifyPasswordAsync`; keduanya lolos → pesanan dibuat.
7. Gagal verifikasi → `ModelState` error di bawah widget + halaman dimuat ulang.

Saat modal ditutup, widget dihapus dari DOM (`hidden.bs.modal`) agar challenge berikutnya
selalu baru dan sekali pakai.

## Cara mengaktifkan / menonaktifkan

- **Aktif**: isi `ALTCHA_HMAC_KEY` di Panel → Pengaturan Aplikasi dengan kunci acak
  minimal 16 karakter. Contoh: `openssl rand -hex 32`.
- **Nonaktif**: kosongkan kunci lalu Simpan. Widget tidak dirender, verifikasi dilewati.

Aturan sederhana: **kunci kosong = nonaktif; kunci terisi = aktif**. Tidak ada toggle terpisah.

## Menyesuaikan di masa depan

### 1. Menambah ALTCHA ke formulir lain (Masuk, Daftar, Kontak)

Pola yang sama di mana pun:

```csharp
// 1) Di page model (contoh: Masuk.cshtml.cs)
[BindProperty(Name = "altcha")]
public string? AltchaPayload { get; set; }

// 2) Endpoin challenge (handler di halaman yang sama)
public IActionResult OnGetAltcha()
{
    var kunci = (await _settingService.GetAsync(SettingService.AltchaHmacKey))?.Value.Trim() ?? "";
    var json = Altcha.BuatChallenge(kunci);
    return json is null ? NotFound() : Content(json, "application/json");
}

// 3) Verifikasi di OnPost
if (kunci.Length >= 16 && !Altcha.Verify(kunci, AltchaPayload))
{
    ModelState.AddModelError(nameof(AltchaPayload), "Verifikasi keamanan gagal.");
    return Page();
}
```

```html
<!-- 4) Di dalam <form> (atribut version 2.3.0: challengeurl / challengejson) -->
<altcha-widget challengeurl="/Masuk?handler=Altcha" type="checkbox" hidelogo hidefooter></altcha-widget>
<!-- 5) Di bagian script halaman -->
<script src="/js/altcha.min.js" type="module"></script>
```

`type="checkbox"` menampilkan kotak centang seperti CAPTCHA klasik (paling familiar);
`type="native"` menampilkan tombol dengan progres solusi; `type="switch"` ala toggle.

### 2. Menyesuaikan tingkat kesulitan (berapa lama browser bekerja)

Di `Helpers/Altcha.cs`:

```csharp
// Rentang angka yang harus dicoba browser. Default (50_000, 100_000) ≈ 0.5-2 detik.
Altcha.NumberRange = (100_000, 200_000);   // lebih lama, lebih sulit utk bot
// Umur challenge (default 120 detik, harus selaras dgn timeout widget 90 detik).
Altcha.Expiry = TimeSpan.FromSeconds(120);
```

Semakin lebar rentang, semakin mahal bagi bot yang memecah ribuan challenge, tanpa
merusak pengalaman user biasa (sekali per buka modal).

### 3. Fungsi sebagai pengatur serangan ulang (anti-replay)

Payload tidak menyimpan status di server; siapa pun yang menangkap payload sah bisa
memakainya ulang sampai challenge kedaluwarsa (120 detik). Untuk kebutuhan ketat,
tambahkan penyimpanan payload terpakai di memori/DB lalu tolak duplikat. Contoh pola:

```csharp
private static readonly ConcurrentDictionary<string, byte> Terpakai = new();
// di Verify: var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
// if (!Terpakai.TryAdd(Convert.ToBase64String(hash), 0)) return false;
// bersihkan berkala: hapus entri yang expired (> Altcha.Expiry).
```

### 4. Algoritma lebih kuat (Argon2id / Scrypt)

Widget bawaan mendukung algoritma `SHA-*` dan `PBKDF2*` "out of the box" dengan
`SubtleCrypto`; `Argon2id` dan `Scrypt` butuh WASM dan file pendukung dari npm ALTCHA.
Menggantinya berarti: `algorithm` di challenge JSON diganti
(`PBKDF2-SHA256`, `Argon2id-13`, `Scrypt-1`), salt memuat param `cost`, dan sisi verifikasi
di `Altcha.cs` harus menghitung ulang dengan KDF yang sama. Detail lengkap di
`https://altcha.org/docs/integration/proof-of-work-captcha/`.

### 5. Bahasa widget (i18n)

Widget default berbahasa Inggris dengan teks minimal. File `altcha.lang.id.js` sudah
disertakan di `wwwroot/js`. Cara pakai (versi widget 2.x):

```html
<!-- Muat widget, lalu file bahasa, lalu atur language -->
<script src="/js/altcha.min.js" type="module"></script>
<script src="/js/altcha.lang.id.js"></script>
<altcha-widget language="id" ...></altcha-widget>
```

Perilaku persis mekanisme pemuatan bahasa dapat berubah antar versi widget; cek
`https://altcha.org/docs/integration/widget/` saat upgrade.

### 6. Batasi penyalahgunaan endpoin challenge

Endpoin challenge publik; bot bisa meminta challenge terus-menerus (murah di sisi server
karena hanya membuat hash+HMAC). Untuk produksi, pasang rate limiting (mis. ASP.NET Core
RateLimiter: 30 permintaan/menit per IP) pada `/Checkout?handler=Altcha`.

### 7. Endpoin challenge hanya saat aktif

Handler `OnGetAltcha` mengembalikan `404` bila kunci kosong. Widget yang terlanjur
meminta challenge pada halaman yang dirender sebelum kunci dihapus akan menampilkan
kesalahan - aman, dan halaman selanjutnya tidak merender widget sama sekali.

## Batasan yang perlu diketahui

- **Butuh HTTPS/secure context** (kecuali `http://localhost` yang dianggap aman oleh
  browser): `SubtleCrypto` tidak tersedia di HTTP biasa. Pastikan produksi pakai HTTPS.
- **PoW bukan "skor kecerdasan"**: tidak membedakan manusia vs bot yang sabar; hanya
  menaikkan biaya serangan. Untuk deteksi ancaman adaptif ada ALTCHA Sentinel (layanan
  terpisah) - itu di luar cakupan "tanpa pihak ketiga".
- **Verifikasi wajib di server**: widget di browser bisa dimanipulasi; keamanan hanya
  berarti bila `Altcha.Verify` selalu dijalankan di `OnPost`.
- **Kunci rahasia jangan dibocorkan**: `ALTCHA_HMAC_KEY` adalah penanda digital challenge;
  siapa yang tahu kunci bisa membuat challenge "sah". Jangan log payload/challenge penuh.

## Referensi resmi

- Dokumentasi ALTCHA: https://altcha.org/docs/
- Proof-of-work v2 (algoritma & param): https://altcha.org/docs/integration/proof-of-work-captcha/
- Integrasi widget: https://altcha.org/docs/integration/widget/
- Repositori: https://github.com/altcha-org/altcha