# Panduan SignalR - Notifikasi Real-Time di Panel (Self-Hosted)

Dokumen ini menjelaskan SignalR, bagaimana ia dipakai untuk toast/badge real-time di area
panel (Batch D), dan cara mengembangkannya ke depan. Semua berjalan di dalam aplikasi ini
tanpa layanan pihak ketiga.

## Apa itu SignalR?

SignalR adalah pustaka real-time bawaan ASP.NET Core: server **mengirim** data ke browser
yang terhubung tanpa browser bertanya (push), tidak seperti polling yang menanyakan terus.

```
Server (Hub)                          Browser (klien @microsoft/signalr)
──────────                            ─────────────────────────────────
1. MapHub("/hub/order-events")        2. HubConnection baru ke path yang sama
3. Event bisnis terjadi (pesanan      4. Data diterima langsung -> tampilkan
   DITERIMA/DIBATALKAN)                  toast + update badge
   hub.Clients.All.SendAsync(...)
```

Transport otomatis: WebSocket (utama) → Server-Sent Events → Long Polling sebagai cadangan,
jadi tetap jalan walau WebSocket diblokir (mis. proxy).

## Konsep kunci

- **Hub** - kelas server yang menjadi titik masuk pesan. Bisa tanpa method (murni publisher)
  atau punya method untuk klien panggil.
- **Clients.All / Groups / User** - sasaran kirim: semua koneksi, grup tertentu (mis.
  grup "staff"), atau per user.
- **HubConnection** (JS) - objek klien; `on("namaEvent", cb)` mendaftarkan penerima.
- **Reconnect** - klien otomatis coba sambung ulang saat koneksi putus
  (`withAutomaticReconnect`).
- **Auth** - koneksi memakai cookie yang sudah ada (same-origin), jadi hanya pengurus panel
  yang mendapat event - tidak perlu token ekstra.

## Rencana pemasangan di proyek ini (Batch D)

1. **Hub** `Hubs/OrderEventHub.cs` (apex kecil, tak ada method yang dipanggil browser):

```csharp
public class OrderEventHub : Hub { }
```

2. **Registrasi & pemetaan** di `Program.cs`:

```csharp
builder.Services.AddSignalR();
// ...
app.MapHub<OrderEventHub>("/hub/order-events");
```

3. **Publisher** - setiap tempat status pesanan berubah (OrderService: TerimaAsync,
   BatalAsync, tambahan status Batch E) kirim event setelah transaksi sukses:

```csharp
var ctx = _hubContext.Clients.All;
await ctx.SendAsync("orderStatusChanged", new
{
    orderNumber = order.OrderNumber,
    status = "DITERIMA",          // atau DIBATALKAN
    actedBy = "..."               // siapa yang mengubah
});
```

   `IHubContext<OrderEventHub>` di-inject ke service (bukan di controller) agar event
   tetap terkirim walau perubahan dipicu dari mana saja (halaman panel, API, dst.). **Catatan
   penting**: kirim event SETELAH transaksi commit, agar toast tidak muncul untuk pesanan
   yang ternyata gagal.

4. **Klien** di `_Layout.cshtml` (panel) - pustaka klien diunduh sekali ke `wwwroot`:

```html
<script src="/js/signalr.min.js"></script>
```

```js
const hub = new signalR.HubConnectionBuilder()
    .withUrl('/hub/order-events')
    .withAutomaticReconnect()
    .build();

hub.on('orderStatusChanged', (e) => {
    // 1) Toast SweetAlert (pola toast yang sudah dipakai)
    // 2) Update badge sidebar "Notifikasi" via fetch ke API status terbaru
});

hub.start().catch(() => { /* jaringan mati? coba lagi otomatis */ });
```

5. **Badge & status dibaca**: endpoint kecil `/api/notifications/unread-count` atau
   halaman yang memuat daftar (keputusan alur Batch D: badge + toast realtime; halaman
   daftar opsional menurut keputusan saat itu).

## Menyesuaikan di masa depan

- **Event lain**: tinggal `SendAsync("namaEvent", payload)` dari service mana pun +
  `hub.on(...)` di layout.
- **Per user / per grup**: ganti `Clients.All` dengan `Clients.Group("staff")` (add ke grup
  saat `OnConnectedAsync` setelah cek peran), atau `Clients.User(userId)`.
- **Payload terstruktur**: definisikan DTO (mis. `OrderEventDto`) agar kontrak jelas.
- **Pustaka klien**: ambil `@microsoft/signalr/dist/browser/signalr.min.js` sekali ke
  `wwwroot/js/` (sama seperti pola `altcha.min.js`) - tetap tanpa CDN.
- **Skala banyak server**: butuh backplane (Redis/Service Bus) - di luar cakupan single
  instance; catatan saja.
- **Pembatasan**: event bisa dibatasi hanya untuk pesanan yang statusnya mengarah ke
  DITERIMA/DIBATALKAN (sesuai Batch E), sehingga toast tidak bising.

## Batasan & pengamanan

- Koneksi berbasis cookie panel: browser staf yang login menerima event; pengunjung publik
  tidak (endpoint hub menolak bila tidak staff).
- Reconnect otomatis menangani jaringan putus sesaat; event yang terlewat saat offline
  tetap bisa disinkronkan lewat badge/fetch saat halaman dibuka (hybrid push + pull).
- Pengujian localhost: WebSocket jalan di `http://localhost`; di produksi wajib HTTPS.
- Jangan kirim data sensitif (isI body snapshot) lewat event - cukup nomor pesanan &
  status; detail tetap lewat fetch biasa.

## Referensi resmi

- Dokumentasi SignalR ASP.NET Core: https://learn.microsoft.com/aspnet/core/signalr/
- Klien JavaScript: https://learn.microsoft.com/aspnet/core/signalr/javascript-client
- Pemetaan hub & otentikasi: https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz