# Panduan OpenIddict - SSO Self-Hosted untuk .NET

Dokumen ini menjelaskan apa itu OpenIddict, bagaimana ia menjadi "SSO internal" langsung di
dalam aplikasi ASP.NET Core (tanpa panggilan keluar ke pihak ketiga), dan bagaimana rencananya
dipasang di proyek ini (Batch F). Ditulis sebagai bahan belajar dan titik mulai untuk pemakaian
kembali di masa depan.

## Apa itu SSO dan OIDC?

**SSO (Single Sign-On)**: pengguna masuk sekali, lalu otentikasi berlaku untuk banyak aplikasi
(atau banyak bagian aplikasi) tanpa memasukkan kredensial lagi.

**OpenID Connect (OIDC)** adalah standar di atas OAuth 2.0 yang menambahkan lapisan identitas:

```
Browser/SPA                             Authorization Server (aplikasi ini)
─────────────                           ──────────────────────────────────
1. Arahkan ke /connect/authorize       2. Pengguna masuk (username/password,
   (client_id, redirect_uri, scope,        atau SSO terhadap skema lain)
   state, nonce)                        3. Setuju (consent) + kode otorisasi
4. Tukar kode di /connect/token  ──────▶ 5. Terbitkan ID Token + Access Token (JWT)
6. Ambil profil di /connect/userinfo
7. Aplikasi memercayai JWT (verifikasi signature + exp)
```

Singkatnya: aplikasi tidak perlu membuat JWT sendiri - penyedia identitas (di sini OpenIddict,
jalan di proses yang sama) yang melakukannya, dan aplikasi/klien lain bisa memverifikasinya.

## Kenapa OpenIddict?

| Opsi | Catatan |
|---|---|
| **OpenIddict** | MIT, NuGet, server OIDC lengkap (authorize/token/userinfo/discovery) berjalan dalam proses aplikasi yang sama. Standar de facto .NET. |
| Keycloak | Self-hosted tapi app Java berat + DB sendiri; berlebihan untuk trial. |
| OIDC buatan sendiri | Rentan (kriptografi, nonce, PKCE, discovery) dan banyak kerja; tidak disarankan. |

OpenIddict memenuhi prinsip proyek ini: **100% self-hosted, tanpa pihak ketiga**, sekaligus
berstandar (klien apa pun yang paham OIDC bisa bergabung: Postman, SPA, backend lain).

## Konsep kunci

- **Authorization Server (AS)** - OpenIddict di aplikasi ini; menyediakan endpoint
  `/connect/authorize`, `/connect/token`, `/connect/userinfo`, dan dokumen discovery
  di `/.well-known/openid-configuration`.
- **Client** - aplikasi yang minta login. Di proyek ini klien utamanya aplikasi web itu sendiri
  (public client, menuntut PKCE). Nanti bisa ditambah klien lain.
- **ID Token** - JWT berisi identitas (sub, name, email...), klaimnya berasal dari skema login
  yang kita sambungkan (staf via cookie panel / pelanggan via cookie toko).
- **Access Token** - JWT untuk memanggil API; di proyek ini bisa dipakai API `/api/*` dengan
  validasi token OpenIddict di samping skema API key yang sudah ada.
- **Scopes** - izin: minimal `openid profile email`; dibuka lagi untuk `api`.
- **PKCE** - wajib untuk public client (SPA/browser) agar kode otorisasi aman.

## Rencana pemasangan di proyek ini (Batch F)

1. **Paket**: `OpenIddict.AspNetCore` (dan pendukung `OpenIddict.EntityFrameworkCore` bila
   mau menyimpan aplikasi/otorisasi di DB; untuk trial cukup in-memory + `dev` signing key).
2. **Registrasi di `Program.cs`** (sketsa):

```csharp
builder.Services.AddOpenIddict()
    .AddCore(options => { })                                   // opsional: simpan aplikasi di DB
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo");
        options.RegisterScopes(OpenIddictConstants.Scopes.Profile, "api");
        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();               // PKCE
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();             // ganti X509 di produksi
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();                                // validasi JWT dalam proses
        options.UseAspNetCore();                                 // [Authorize] pakai token
    });

builder.Services.AddAuthentication()
    .AddOpenIdConnect("ssoss", options => ...);                  // klien OIDC untuk area web
```

3. **Saklar di PengaturanAplikasi** - konsisten dengan pola `ALTCHA_HMAC_KEY`:
   - `SSO_ENABLED` (`1`/`0`) - default nonaktif, login lama tetap dipakai
   - Saat aktif: halaman `/Masuk` (dan `/Panel/Masuk`) menampilkan tombol
     "Masuk dengan SSO" yang mengarah ke `/connect/authorize` (redirect ke skema login asli
     untuk staf atau pelanggan, lalu kembali dengan kode → token → cookie sesuai skema
     masing-masing: `GKLaku.Staff` / `GKLaku.Customer`).
4. **Callback handler**: endpoin `/sso/callback` menerima kode, tukar ke token, cocokkan
   pengguna (staf via `MASTER_USER.USERNAME`, pelanggan via `MASTER_CUSTOMER.EMAIL`), lalu
   `SignInAsync` dengan cookie yang sudah ada - sehingga semua logika otorisasi yang ada
   (`IsInRole`, `CustomerAuth`, klaim) tidak berubah sama sekali.
5. **Endpoint `authorize`** memakai UI login yang sudah ada (bukan membuat halaman consent baru).

Risiko ke area yang sudah jalan diminimalkan: `SSO_ENABLED=0` berarti alur login sekarang
berjalan persis seperti saat ini; SSO hanya menambah jalur masuk alternatif.

## Menyesuaikan di masa depan

- **Tambah klien**: daftarkan client_id kedua (mis. aplikasi mobile) di konfigurasi
  OpenIddict; skema alur tetap sama.
- **Klaim & scope**: tambah klaim kustom di event yang mengisi ID Token/UserInfo
  (`options.Events`), mis. `display_name`.
- **Masa token**: `options.SetAccessTokenLifetime(...)`, `SetIdentityTokenLifetime(...)`.
- **Sertifikat produksi**: ganti `AddDevelopmentSigningCertificate()` dengan
  `AddSigningCertificate(new X509Certificate2(...))` - jangan pakai dev key di produksi.
- **DB-backed**: pakai `AddCore(...)` + `UseOpenIddict()` di EF untuk menyimpan aplikasi/
  otorisasi/refresh token secara permanen.
- **Logout**: endpoint `/connect/logout` + hapus cookie lokal.
- **API non-browser**: gunakan alur client_credentials untuk service-to-service.

## Batasan & pengamanan

- **Wajib HTTPS/secure context** (localhost aman). Jangan expose dev cert ke produksi.
- Kode otorisasi sekali pakai; PKCE wajib untuk public client; `nonce` mencegah replay.
- Jangan log token/kode; gunakan `state` untuk anti-CSRF pada aliran browser.
- OpenIddict standar: klien pihak ketiga yang sah bisa bergabung kapan saja - batasi
  `redirect_uris` ke daftar yang diketuk saja.

## Referensi resmi

- Dokumentasi: https://documentation.openiddict.com/
- Alur authorization code + PKCE: https://documentation.openiddict.com/guides/integrations/
- Repositori: https://github.com/openiddict/openiddict-core