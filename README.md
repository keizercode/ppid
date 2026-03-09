# Sistem Permohonan Permintaan Data — PPID Diskominfo

ASP.NET Core 8 MVC · PostgreSQL · Tailwind CSS CDN

---

## Setup

### 1. Edit koneksi DB di `appsettings.json`
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=permintaan_data_db;Username=postgres;Password=yourpassword"
}
```

### 2. Migrasi DB
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Jalankan
```bash
dotnet run
```

---

## URL

### Frontend Publik
| URL | Keterangan |
|-----|-----------|
| `/` | Lacak permohonan |
| `/Home/Lacak?noPermohonan=PPD/2024/0001` | Detail status |
| `/Home/Kuesioner?id={guid}` | Kuesioner kepuasan |

### API Proxy (internal)
| URL | Sumber |
|-----|--------|
| `GET /api/provinsi` | api-wilayah.dinaslhdki.id |
| `GET /api/kabupaten` | api-wilayah.dinaslhdki.id (DKJ) |
| `GET /api/kecamatan?kab=ID` | api-wilayah.dinaslhdki.id |
| `GET /api/kelurahan?kec=ID` | api-wilayah.dinaslhdki.id |
| `GET /api/cek-nik?nik=NIK` | banksampah.jakarta.go.id |
| `GET /api/bidang` | ekinerjapjlp.jakarta.go.id |

### Backend Admin
| URL | Role |
|-----|------|
| `/petugas-loket` | Petugas Loket |
| `/petugas-loket/identifikasi` | Petugas Loket |
| `/petugas-loket/daftar?kategori=Mahasiswa` | Petugas Loket |
| `/kepegawaian` | Kepegawaian |
| `/kdi` · `/kdi/psmdi` · `/kdi/bidang` | KDI |

---

## Fitur Formulir

### Form DaftarPemohon (`/petugas-loket/daftar`)

**2 Mode Input:**
- 🔍 **Cari via NIK** — masukkan 16 digit NIK, sistem memanggil API `cek-nik` dan auto-fill nama, RT/RW, dan alamat
- ✏️ **Isi Manual** — isi semua field secara manual

**Cascading Address:**
- Provinsi → Kabupaten/Kota → Kecamatan → Kelurahan
- Semua data wilayah diambil dinamis dari API wilayah

**Keperluan (multi-checkbox):**
- ✅ Observasi → muncul textarea "Deskripsi Observasi"
- ✅ Permintaan Data → muncul textarea "Data yang Diperlukan"
- ✅ Wawancara → muncul textarea "Materi Wawancara"

**Bidang:**
- Dropdown dinamis dari API `ekinerjapjlp.jakarta.go.id`

---

## DB Schema Notes

- `NoPermohonan` = auto-generated: `PPD/{YEAR}/{SEQUENCE:D4}`
- `NoSuratPermohonan` = nomor surat permohonan yang dibawa pemohon
- Keperluan master: 1=Observasi, 2=Permintaan Data, 3=Wawancara
- Status PPID: 1=Baru ... 11=Selesai
