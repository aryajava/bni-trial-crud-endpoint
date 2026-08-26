# PRD — Product CRUD API (Consumer & Server)

> **Version:** 1.5.0 | **Date:** 2026-08-24 | **Author:** Engineering  
> **Updated:** Skema MASTER_PRODUCT hanya menggunakan ID (PRODUCT_ID/EXTERNAL_ID dihapus), Dapper SQL langsung di Service layer, cek duplikasi /insert berbasis ID, Swagger UI dengan Authorize Key.

---

## 1. Overview

API berbasis ASP.NET Core (.NET 10) yang memiliki dua peran utama:

| Peran | Keterangan |
|---|---|
| **Consumer** | Mengambil data produk dari `https://fakestoreapi.com/products` dan menyinkronkan ke DB lokal |
| **Server** | Menyajikan CRUD produk ke/dari SQL Server lokal |

Semua endpoint dilindungi **API Key** melalui HTTP Header `X-Api-Key`. Response dikembalikan dengan **HTTP Status Code dinamis** yang ditentukan otomatis oleh `ResponseHelper` — tidak ada hardcode status code di controller.

---

## 2. Architecture & Pattern

### Layered MVC dengan Dapper di Service Layer
```
Request → [Middleware Pipeline] → Controller → Service (Dapper SQL + Mapper) → SQL Server
```

| Layer | Peran |
|---|---|
| **Controller** | Menerima request, validasi model state, memanggil interface `IProductService`, mengembalikan response via `ResponseHelper` |
| **Service** | Mengimplementasikan logika bisnis, menjalankan query SQL langsung via **Dapper**, menggunakan **Mapper** untuk konversi Entity ⇄ DTO, dan mencatat log audit Serilog |
| **Database** | SQL Server (Database: `LOSCONSUMER`, Schema: `LOSCONSUMER`) |

---

## 3. Technology Stack

| Komponen | Pilihan |
|---|---|
| Framework | .NET 10 (ASP.NET Core Web API) |
| Architecture Pattern | Layered MVC (Controller → Service + Dapper + Mapper) |
| ORM / Data Access | **Dapper** (Dapper Query langsung di Service) |
| Database | Microsoft SQL Server (LocalDB) |
| Primary Key Strategy | **`SEQUENCE NO CACHE`** (`DEFAULT (NEXT VALUE FOR ...)`) — Menjamin ID urut 1, 2, 3 tanpa gap |
| DB Migration | `dbup-sqlserver` (Embedded SQL scripts di folder `Scripts/`) |
| Logging | **Serilog** (File rolling harian di `logs/master_product/`) + Tabel Audit DB |
| Authentication | API Key via HTTP Header `X-Api-Key` |
| API Docs UI | **Swagger UI** (`Swashbuckle.AspNetCore.SwaggerUI` + `Microsoft.AspNetCore.OpenApi`) |

---

## 4. Database Schema (`LOSCONSUMER`)

### 4.1 Tabel `LOSCONSUMER.MASTER_PRODUCT`

| Nama Kolom | Tipe Data | Nullable | Keterangan |
|---|---|---|---|
| `ID` | `INT IDENTITY(1,1)` | NOT NULL | **Primary Key** |
| `TITLE` | `NVARCHAR(500)` | NOT NULL | Nama produk |
| `PRICE` | `DECIMAL(18,2)` | NOT NULL | Harga produk |
| `DESCRIPTION` | `NVARCHAR(MAX)` | NULL | Deskripsi produk |
| `CATEGORY` | `NVARCHAR(200)` | NULL | Kategori produk |
| `IMAGE` | `NVARCHAR(1000)` | NULL | URL gambar produk |
| `RATING_RATE` | `DECIMAL(5,2)` | NULL | Rating produk (0.0 - 5.0) |
| `RATING_COUNT` | `INT` | NULL | Jumlah ulasan |
| `IS_ACTIVE` | `BIT` | NOT NULL | **6 Kolom Wajib**: 1 = Aktif, 0 = Soft Deleted (Default: 1) |
| `CREATED_AT` | `DATETIME` | NOT NULL | **6 Kolom Wajib**: Timestamp dibuat (Default: `GETDATE()`) |
| `CREATED_BY` | `NVARCHAR(100)` | NOT NULL | **6 Kolom Wajib**: User/Key pembuat (Default: `'SYSTEM'`) |
| `UPDATED_AT` | `DATETIME` | NULL | **6 Kolom Wajib**: Timestamp diupdate |
| `UPDATED_BY` | `NVARCHAR(100)` | NULL | **6 Kolom Wajib**: User/Key pengupdate |
| `VERSION` | `INT` | NOT NULL | **6 Kolom Wajib**: Optimistic Concurrency token (Default: 1) |

---

### 4.2 Tabel `LOSCONSUMER.REQUEST_PRODUCT`

| Nama Kolom | Tipe Data | Keterangan |
|---|---|---|
| `ID` | `BIGINT IDENTITY(1,1)` | Primary Key |
| `TRACE_ID` | `NVARCHAR(100)` | Korelasi GUID unik per request |
| `ENDPOINT` | `NVARCHAR(500)` | Path URL endpoint |
| `HTTP_METHOD` | `NVARCHAR(10)` | GET, POST, PUT, DELETE |
| `HEADERS` | `NVARCHAR(MAX)` | Header request (JSON) |
| `QUERY_PARAMS` | `NVARCHAR(MAX)` | Query parameter (JSON) |
| `BODY` | `NVARCHAR(MAX)` | Request body (JSON) |
| `IP_ADDRESS` | `NVARCHAR(50)` | IP address client |
| `REQUESTED_AT` | `DATETIME` | Waktu request masuk |

---

### 4.3 Tabel `LOSCONSUMER.RESPONSE_PRODUCT`

| Nama Kolom | Tipe Data | Keterangan |
|---|---|---|
| `ID` | `BIGINT IDENTITY(1,1)` | Primary Key |
| `TRACE_ID` | `NVARCHAR(100)` | Korelasi GUID unik yang sama |
| `STATUS_CODE` | `INT` | HTTP Status Code (200, 201, 400, 404, dll) |
| `IS_SUCCESS` | `BIT` | `1` jika `STATUS_CODE < 400` |
| `MESSAGE` | `NVARCHAR(MAX)` | Pesan status |
| `RESPONSE_BODY` | `NVARCHAR(MAX)` | Response body (JSON) |
| `ELAPSED_MS` | `BIGINT` | Durasi eksekusi (milidetik) |
| `RESPONDED_AT` | `DATETIME` | Waktu response dikirim |

---

## 5. Endpoints

### 5.1 Server Endpoints (`/products`)

| Method | Path | Deskripsi | Status Code |
|---|---|---|---|
| `GET` | `/products` | Ambil semua produk aktif (`IS_ACTIVE = 1`) | 200 OK |
| `GET` | `/products/{id}` | Ambil produk by ID | 200 OK / 404 Not Found |
| `POST` | `/products` | Tambah produk baru (`VERSION = 1`) | 201 Created / 422 Validation Error |
| `PUT` | `/products/{id}` | Update produk (wajib kirim `version`) | 200 OK / 404 / 409 Conflict |
| `DELETE` | `/products/{id}?type=soft` | Soft delete (`IS_ACTIVE = 0`) | 200 OK / 404 |
| `DELETE` | `/products/{id}?type=hard` | Hard delete (`DELETE FROM`) | 204 No Content / 404 |

### 5.2 Consumer Endpoints (`/products/public`)

| Method | Path | Deskripsi |
|---|---|---|
| `GET` | `/products/public` | Ambil semua produk langsung dari FakeStore API |
| `GET` | `/products/public/{id}` | Ambil satu produk dari FakeStore API by ID |
| `GET` | `/products/public/insert` | Sinkronkan semua produk FakeStore ke database lokal (cek duplikasi berdasarkan ID) |

---

## 6. Authentication

- **Header Name:** `X-Api-Key`
- **Default Key:** `LOS-SECRET-KEY-2026`
- **Bypass Paths:** `/swagger`, `/openapi`, `/favicon.ico`

---

*PRD v1.5.0*
