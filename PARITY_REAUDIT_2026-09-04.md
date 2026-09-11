# Audit ulang keseluruhan — 4 September 2026

> Pembaruan: perbaikan F01–F09 dan bukti uji terbaru dicatat di
> `AUDIT_FIXES_2026-09-04.md`. Isi di bawah adalah kondisi sebelum perbaikan,
> dipertahankan sebagai jejak audit, bukan daftar status rilis terbaru.

## Kesimpulan

**BELUM 1:1.** Ada 20/20 jalur tombol utama yang tersambung, tetapi audit kode
menemukan perbedaan operasi, verifikasi, progres, detail laporan, dan terjemahan.
Jumlah menu/tahap/bahasa yang sama bukan bukti kesetaraan perilaku.

Pass ini melakukan pemeriksaan statis lintas menu dan pendalaman area di bawah,
bukan pengujian setiap cabang pada semua konfigurasi Windows. Perubahan produk
pada pass ini hanya metadata publisher yang diminta. Temuan perilaku **belum
diperbaiki** oleh pass ini. Tes profil, build, dan publish tidak menjalankan repair,
instalasi driver, perubahan registry/services/BCD, Defender, BitLocker, atau reboot.
EXE acuan tidak diluncurkan; PS1 hanya dibaca/diparse, tidak dieksekusi.

Laporan ini menggantikan kesimpulan terlalu luas dari jumlah tahap/menu dalam
laporan sebelumnya. Audit profil sebelumnya tetap berlaku untuk 73 tes sintetisnya,
bukan sebagai sertifikasi seluruh aplikasi.

## Acuan dan bukti yang dapat diulang

Acuan berada di `<private-reference-directory>`:

| Artefak | SHA-256, diperiksa ulang pada pass ini |
|---|---|
| V78.ps1 | `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D` |
| Naufal Windows Powertoys V7.8.exe | `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF` |

Salinan PS1 di proyek memiliki hash yang sama. AST: 649 definisi fungsi, tiga nama
fungsi berulang, nol parse error. Kesetaraan payload EXE/PS1 sudah diperiksa pada
audit sebelumnya; pass ini memvalidasi kembali identitas kedua file dengan hash.
Perbandingan runtime/GPU memakai definisi akhir dan wrapper akhir skrip, bukan
hanya implementasi awal yang kemudian ditimpa.

Jalankan helper audit baca-saja dari folder proyek:

```powershell
.\Tests\ParityAudit\Inspect-StaticParity.ps1
```

Helper memeriksa hash, AST, koneksi 20 handler, metadata publisher, dan membaca
katalog terjemahan terkompresi tanpa mengeksekusi source. Hasil aktual:

- 20/20 tombol utama memiliki event XAML dan definisi handler.
- 23 bahasa, masing-masing 289 entri; total 6.647 entri.
- Enam label contoh belum memiliki terjemahan tepat di 22 bahasa non-Inggris:
  `Repair selected`, `Enable selected`, `Download & install`, `Analyze / reload`,
  `Select safe`, `Apply selected`.
- Kontrol pembanding `System Report` memiliki 22 terjemahan non-Inggris.

Helper tersebut adalah pemeriksaan struktur, **bukan tes kelulusan 1:1**.

## Temuan terkonfirmasi dan prioritas perbaikan

### P1 — F01: BitLocker dapat menganggap read-back gagal sebagai PASS

[SecurityInformationService.cs:315](<SecurityInformationService.cs>)
menyatakan operasi suspend/resume terverifikasi ketika exit code nol dan
`after is null`. Pola yang sama ada pada penerimaan dekripsi sekitar baris 216.
Artinya, sukses menjalankan perintah bercampur dengan sukses memverifikasi hasil.
Acuan `BitLockerManager`, PS1 baris 15355, melakukan `Get-BitLockerVolume
-ErrorAction Stop` dan mengharuskan ProtectionStatus Off/On untuk suspend/resume.

Parser native sekitar baris 331 juga bergantung pada label Inggris dari
`manage-bde`, seperti `Volume`, `Conversion Status`, dan `Protection Status`.
Output Windows non-Inggris dapat tidak terbaca; kondisi tersebut memperbesar
risiko PASS palsu di atas. Ganti sumber status dengan data terstruktur dan bedakan
berhasil, gagal, serta belum terverifikasi. Uji keluaran kosong, query gagal,
locale berbeda, serta perubahan yang belum selesai. Tidak ada perintah BitLocker
yang dijalankan untuk membuktikan temuan kode ini.

### P1 — F02: Reset data Microsoft Store belum diimplementasikan

[MicrosoftStoreRepairService.cs:63](<MicrosoftStoreRepairService.cs>)
memulai tahap `Reset Microsoft Store app data`, tetapi hanya menambahkan teks log.
Cache cleanup dan registrasi manifest pada tahap selanjutnya bukan reset app data.
Acuan `StoreFix`, PS1 baris 13168, pada tahap ketiga mencoba `Reset-AppxPackage`
jika tersedia dan melaporkan warning bila tidak berhasil/tidak tersedia.

Tahap deteksi/registrasi native juga hanya memakai `FindPackagesForUser("")`;
acuan mencari paket dengan `Get-AppxPackage -AllUsers`. Native berhenti jika Store
tidak terdaftar untuk user aktif walaupun payload paket mungkin tersedia untuk
akun lain. Diperlukan operasi reset yang setara, scope enumerasi yang benar, dan
pengujian Store absent/current-user-unregistered/package-in-use pada VM.

### P2 — F03: Progres DISM/SFC belum streaming

[NativeCommandRunner.cs:68](<NativeCommandRunner.cs>)
menampung stdout/stderr sampai proses selesai. `WindowsRepairService.RunStageAsync`
tidak meneruskan output/persentase ke progress window saat proses berjalan.
Full Repair karena itu berpindah menurut tahap DISM/SFC, sedangkan Quick Repair
tidak menunjukkan perkembangan SFC di dalam tahapnya.

Acuan `Invoke-WptRepairProcessWithProgress`, PS1 baris 11078, membaca output secara
bertahap dan mengolah persentase. Progress bar dan elapsed timer native **sudah
ada**, tetapi perilaku streaming belum sama. Tambahkan pembacaan chunk/progres
dengan penanganan CR, encoding, timeout, serta pembaruan UI yang tidak membanjiri
dispatcher. Uji dengan proses simulasi terlebih dahulu, bukan repair PC pengguna.

### P2 — F04: Status per tahap dapat salah menampilkan PASS

[MaintenanceProgressWindow.cs:327](<MaintenanceProgressWindow.cs>)
menandai tahap sebelumnya PASS hanya karena tahap berikutnya dimulai. Kontrak
`MaintenanceProgressUpdate` tidak membawa status selesai WARNING/SKIPPED/FAILED
per tahap. Warning agregat saat Complete tidak menunjukkan tahap asalnya.

Contoh: Explorer melewati registrasi shell karena taskbar sudah pulih, atau Store
mendapat warning registrasi paket; daftar tahap dapat tetap menunjukkan PASS.
Acuan memanggil `Complete-WptMaintenanceStage` dengan status tiap tahap. Perbaikan
harus berlaku di semua konsumen jendela progres, termasuk repair, GPU, dan runtime.

### P2 — F05: Pilihan 23 bahasa belum sama dengan terjemahan seluruh menu

[UiTranslation.cs:119](<UiTranslation.cs>)
menggunakan pencocokan teks exact dan mengembalikan teks asal jika tidak ditemukan,
kecuali beberapa prefix khusus. Label baru native tidak otomatis cocok dengan
label uppercase/istilah lama dari katalog hasil ekstraksi.

Helper membuktikan enam contoh label di atas tidak memiliki terjemahan exact;
`MainWindow.xaml.cs` baris 1539–1556 benar-benar memakainya. Sebaliknya, acuan akhir
`Get-WptGamingRuntimeV83Text`, PS1 baris 37390, mempunyai REPAIR/ENABLE dan teks
konfirmasi khusus untuk 23 bahasa. Perlu key terjemahan stabil serta tabel teks
dinamis/konfirmasi per menu; jumlah folder `.mui` bukan ukuran kelengkapan ini.

### P2 — F06: MSI kehilangan detail dan pembacaan resource saat pemilihan

[MsiModeService.cs:492](<MsiModeService.cs>)
menghasilkan detail PNP/PCI/INF dan jumlah IRQ, tetapi tidak memuat sumber/detail
interrupt, informasi NDIS hardware max/MSI-X/match source, dan rincian audit resource
yang ada di `Get-MsiDeviceDetailsV18`, PS1 baris 21776.

Native menyimpan string Details pada hasil Refresh; handler pemilihan perangkat
di `MainWindow.xaml.cs` sekitar baris 3711 hanya menampilkan string tersebut.
Acuan mencoba audit runtime resource baru saat detail dipilih, dengan fallback
ke snapshot perangkat. Tabel utama, edit MSI/limit/priority, Apply dirty values,
dan Open registry sudah ada, tetapi itu tidak menggantikan detail yang hilang.
Tambahkan pembacaan terstruktur dan uji perangkat line-based, MSI, MSI-X serta
NIC yang tidak menyediakan data NDIS; jangan mengarang nilai saat unavailable.

### P2 — F07: Disk Info/System Report belum mempunyai status kesehatan setara

[SystemReportService.cs:816](<SystemReportService.cs>)
membaca descriptor dan ukuran disk lalu mengisi status dengan literal `Detected`.
Kolom MediaType memakai bus type (misalnya NVMe/USB), bukan kategori media SSD/HDD
dari acuan. PS1 `DiskInfo` baris 14417 dan `SystemReport` memakai MediaType serta
HealthStatus dari data physical disk. Detected tidak membuktikan Healthy.

Perlu sumber kesehatan dan tipe media terstruktur; pisahkan bus/interface dari
jenis media, dan tampilkan Unknown ketika provider tidak mendukung. Uji USB,
NVMe, SATA, removable kosong, disk offline, Storage Spaces dan volume tanpa akses.

### P2 — F08: Runtime Install/Repair memiliki gating berbeda

[MainWindow.xaml.cs:1573](<MainWindow.xaml.cs>)
mengaktifkan Download & install jika ID installable, termasuk saat item sudah Ready
dan repairable. Acuan akhir `Show-WptGamingRuntimeCompatibility`, PS1 baris 37512,
mengaktifkan Install hanya bila InstallId ada **dan bukan repairable**.

Native sudah memisahkan Repair selected dan Enable selected; yang belum sama
adalah aturan enable/disable untuk item siap diperbaiki. Buat tes matriks Ready,
Missing, Optional, repairable, enableable, dan source-only, bukan sekadar tes klik.

### P2 — F09: Transaksi Performance Profile belum disimpan setara acuan

Pemeriksaan 23 kondisi sudah diimplementasikan pada pass sebelumnya dan 73 tes
sintetisnya kembali lulus. Namun acuan `Save-PerformanceProfileTransaction`, PS1
baris 16557, menyimpan hasil Apply/gagal, durasi, restart, KnownPowerGuids serta
hasil rollback, lalu menampilkan rincian verifikasi.

Native `PerformanceProfileVerificationService.ReadKnownPowerGuids` membaca state
lama, tetapi jalur Apply di `PerformanceProfileService`/`PerformanceProfileExtendedService`
tidak menulis record transaksi setara. Belum bisa dinyatakan lifecycle profil 1:1.
Tambahkan persistensi atomik/versioned dan uji apply gagal, rollback parsial,
restart aplikasi, serta pemuatan transaksi lama. Data read-only yang gagal dibaca
tetap harus mencegah VERIFIED; jangan menghapus pengamanan ini demi meniru bug.

## Matriks seluruh tombol Main UI

Semua koneksi handler pada tabel ini dicek ulang. “Ada” hanya berarti implementasi
dan jalurnya ditemukan; **bukan lulus uji operasi di Windows**. Detail katalog yang
tidak dibahas sebagai temuan masih perlu matriks per-ID/per-kondisi di VM.

| Menu | Implementasi native / hasil audit | Batas yang masih terbuka |
|---|---|---|
| Full Repair | WindowsRepairService; DISM lalu SFC | F03/F04; servicing dan reboot-required belum diuji |
| Quick Repair | WindowsRepairService; SFC | F03/F04; output/failure matrix belum setara |
| Windows Update Fix | WindowsUpdateRepairService; 6 tahap, cache/service/read-back | F04; service locked, cache in-use, recovery perlu VM |
| Microsoft Store Fix | MicrosoftStoreRepairService; 9 tahap | F02/F04; reset dan all-user detection berbeda |
| Explorer Fix | ExplorerRepairService; 7 tahap, taskbar verification/rescue | F04; skipped-stage status, scheduled launch/fail-safe perlu VM |
| Disk Info | SystemReportService.CollectDiskInformation | F07; provider/volume edge cases |
| System Report | SystemReportService.CollectAsync; SMBIOS/security/storage | F07; firmware/TPM/BitLocker/locales belum lengkap diuji |
| Windows Activation | LicensingInformationService; slmgr /xpr | Query utama sama; OS metadata registry vs CIM, error/locale belum diuji |
| Office Activation | LicensingInformationService; OSPP /dstatus | Enam lokasi dan command sama; tidak ada bypass aktivasi; belum uji Office terpasang/absent |
| Disable Defender | DefenderPolicyService | Target/pengamanan sudah ada; tamper protection, fail/rollback perlu VM |
| Restore Defender | DefenderPolicyService | Restore/default/read-back belum disertifikasi lewat operasi |
| BitLocker Manager | SecurityInformationService; status/suspend/resume/decrypt | F01; query gagal/locale bukan PASS |
| Smart App Control | SecurityInformationService; status dan Windows Security | ON/OFF/Evaluation/unsupported dan batas perubahan perlu VM |
| Essential Windows Tweaks | Shared catalog + EssentialTweaks/Actions/Performance | F05; Apply/Restore/default/migrasi setiap item belum diuji |
| Gaming Tweaks | GamingTweaks/Actions/BCD/DeviceNetwork/Performance | F05/F09; per-item snapshots, network/BCD/reboot perlu VM |
| Games Runtime & Compatibility | GamingRuntimeCompatibility/Installer | F04/F05/F08; 18-entry detection/install/enable/repair matrix |
| GPU Driver Manager | GpuDriverService; inventory, resolver, signed download, 6 tahap | F04/F05; vendor catalogs/packages dan hardware belum diuji langsung |
| Advanced Windows Tweaks & De-Bloat | DebloatCatalog dan layanan registry/services/network/Xbox/AI | F05; atomic/group rollback, unavailable, defaults, migrasi state perlu VM |
| MSI Mode Utility | MsiModeService dan editor native | F06; device capabilities, invalid limit, read-back/partial failure |
| Legacy Windows Panels | LegacyWindowsPanelsService | Jalur launch ada; applet absent/deprecated, focus dan locale belum diuji |

## Perilaku aplikasi secara keseluruhan

| Area | Penilaian |
|---|---|
| Startup/admin/single instance | Manifest, mutex, dan first-run flow sudah ada; first-run sembilan tahap belum dieksekusi pada pass ini |
| Task queue/concurrency | Resource locks, queued/running/history serta Main UI exit/reboot guard ada; urutan konflik/cancel/close perlu tes interaktif |
| Child-window lifecycle | Shared ToolWindow tidak punya Closing guard saat backend sibuk. Runtime acuan menonaktifkan ControlBox saat busy. Progress native mengabaikan late update setelah close; jangan menyimpulkan pasti crash, tetapi uji system-X, close owner, dan reopen |
| Theme/scale | Delapan opsi dan baseline scaling ada; visual clipping, seluruh item virtualized, RTL dan 23 bahasa perlu uji Light/Dark x 8 skala dengan binary terbaru |
| Persistence | Preferensi theme/language/scale memakai lokasi canonical; migrasi beberapa snapshot sudah ada; F09 dan schema backend lain tetap terbuka |
| LIVE GAMING STATUS | Backend lengkap telah ditambahkan sebelumnya; ketepatan sampling/locale/failed counters belum dites ulang pada pass ini |
| Installer | Publisher baru terverifikasi pada file hasil build; install/upgrade/uninstall serta startup release belum diuji pada pass ini |

Tetap pertahankan pengecualian UI yang memang diminta pengguna: jendela WinUI
terpisah/resizable, header bersih, nama EXE pendek, toolbar bawah berwarna menurut
risiko, dan penghapusan nama skrip dari deskripsi. Itu bukan fitur yang hilang.

## Publisher dan build hasil pass ini

Publisher yang dipilih: **Naufal Tech's Softwares**.

- Company/AssemblyCompany di proyek diisi dan diaktifkan.
- Package PublisherDisplayName diubah.
- Inno Setup AppPublisher dan VersionInfoCompany memakai nama yang sama.
- AppId installer dan identitas paket dipertahankan untuk kompatibilitas upgrade.
- `Identity Publisher="CN=Naufal"` sengaja tidak diubah tanpa sertifikat MSIX
  yang sesuai. Itu identitas penandatanganan, bukan sekadar nama tampilan.
- [AppPublisher](https://jrsoftware.org/ishelp/topic_setup_apppublisher.htm)
  dan [VersionInfoCompany](https://jrsoftware.org/ishelp/topic_setup_versioninfocompany.htm)
  mengatur metadata installer; nama ini tidak membuat signature digital.

Verifikasi aktual:

- Debug x64: **0 error, 0 warning**.
- Tes profil: **73 synthetic regression assertions PASS**, tanpa mutasi.
- Publish Release Native AOT: berhasil, termasuk generating native code.
- Inno Setup: berhasil. Setup dibuat, **tidak diinstal**.
- FileVersionInfo.CompanyName pada EXE aplikasi dan Setup benar-benar terbaca
  `Naufal Tech's Softwares`; versi tetap `7.8.0.0`.
- Kedua executable masih **NotSigned**. Tidak ada sertifikat/trust store diubah.
- Startup/UI binary terbaru belum diuji pada pass ini; tidak mengulang klaim
  bahwa build bersih menjamin tidak crash.

| Artefak | Ukuran | SHA-256 |
|---|---|---|
| `artifacts/publish/win-x64-20260904-154454/Naufal Windows Powertoys.exe` | 17.918.976 byte | `123E673685DD504D8BEA0A56C56D4B40557BEFD2AF8BFE7D664AA7B6F0FC1125` |
| `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe` | 36.372.720 byte | `66A6396C7163ADA68137FEF8D250AE8440F78AC25622154A2404DF72D66AEC3B` |

Staging berisi 160 file; simpan seluruh folder jika menjalankan tanpa Setup.
Pipeline mengganti Setup generated dengan nama sama. Staging sebelumnya tidak
dihapus, sehingga versi sebelumnya masih dapat dibangun ulang. Artefak ini build
metadata baru dengan temuan audit yang masih terbuka, **bukan rilis bersertifikat 1:1**.

## Urutan agar dapat dinyatakan setara

1. Perbaiki F01/F02 terlebih dahulu, lalu kontrak progres F03/F04.
2. Lengkapi terjemahan per-key, detail MSI/disk, runtime gating dan transaksi profil.
3. Buat fixture per katalog: kondisi awal, aksi, target state, no-op, unavailable,
   partial failure, read-back, restore original, restore defaults, dan restart.
4. Jalankan acuan dan native secara terpisah pada snapshot VM yang identik;
   bandingkan hasil Windows, bukan hanya teks sukses. Minta persetujuan sebelum
   tindakan administratif yang mengubah mesin atau membutuhkan instalasi baru.
5. Uji native AOT final, 20 menu, 23 bahasa, 8 skala, Light/Dark, keyboard/RTL,
   concurrent tasks, close/reopen, serta Setup install/upgrade/uninstall.

Tidak ada persentase kesetaraan numerik yang dilaporkan karena denominator seluruh
cabang/kondisi belum ditetapkan. Menyebut 20/20 menu sebagai 100% parity akan keliru.
