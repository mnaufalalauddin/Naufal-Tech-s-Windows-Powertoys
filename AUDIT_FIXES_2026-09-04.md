# Perbaikan sembilan temuan audit — 4 September 2026

## Hasil dan batas pengujian

Kesembilan temuan F01–F09 dalam `PARITY_REAUDIT_2026-09-04.md` telah
ditangani pada kode. Ini **bukan sertifikasi bahwa seluruh aplikasi sudah 1:1**.
Uji operasi administratif pada snapshot VM tetap diperlukan. Tidak ada repair,
instalasi/uninstall driver atau runtime, perubahan BitLocker, registry/services/BCD,
reset Store, atau reboot yang dijalankan pada PC pengguna selama pass ini.

EXE acuan tidak diluncurkan dan PS1 tidak dieksekusi. Identitas kedua file acuan
diperiksa ulang dan tetap cocok dengan audit sebelumnya. Pengecualian UI yang
diminta pengguna (jendela resizable, header bersih, toolbar bawah, branding) dipertahankan.

## Perubahan per temuan

| ID | Perbaikan | Bukti / batas |
|---|---|---|
| F01 | BitLocker memakai Win32_EncryptableVolume dengan getter status terstruktur; read-back null/Unknown tidak boleh PASS. 0% tidak otomatis FullyDecrypted. System Report memakai sumber yang sama. | Tes null, status salah, exit gagal, dekripsi paused/in-progress; probe provider ditolak Access denied pada token pengujian. Suspend/resume/decrypt tidak dijalankan. |
| F02 | Tahap reset memanggil ResetPackageAsync dan memeriksa hasilnya; tidak lagi sekadar menulis log. Deteksi dan registrasi mencari paket all-users, reset menargetkan paket current-user. | Delegate reset diuji untuk sukses, package absent dan package-in-use. API Windows sesungguhnya belum dijalankan. |
| F03 | NativeCommandRunner mengirim stdout/stderr bertahap, menyimpan output akhir, membatasi timeout dan merapikan cancellation. DISM/SFC meneruskan chunk serta persen ke progress window; SFC memakai UTF-16. | Proses anak simulasi membuktikan output tiba sebelum exit; tes Unicode, CR, koma desimal, chunk terbelah, monotonic persen, timeout dan cancellation. DISM/SFC tidak dijalankan. |
| F04 | Status tahap berasal dari backend: PASS/WARNING/SKIPPED/FAILED; UI tidak menebak PASS saat pindah tahap. Tracker mempertahankan warning/skip, menolak late update, dan completion idempotent. Logika status terpisah dari teks terjemahan. | Semua konsumen shared progress disambungkan: Full/Quick, Update, Store, Explorer, GPU, runtime install/enable, first-run. Tes status, unreached stages dan late callback. Visual progress operasi nyata belum diuji. |
| F05 | Katalog tambahan diekstrak statis dari tabel literal bahasa akhir acuan, ditambah alias label native dan stable keys. Tiga konfirmasi runtime memakai teks acuan yang diterjemahkan. Lookup case-insensitive. | 23 bahasa × 396 entri gabungan; enam label yang hilang tersedia di 22/22 bahasa non-Inggris. Semua 23 tabel × 9 key diuji. Ini cakupan key yang diaudit, bukan jaminan seluruh teks dinamis/error/driver/vendor telah diterjemahkan. |
| F06 | Pemilihan perangkat MSI membaca ulang konfigurasi/resource. Detail menampilkan sumber IRQ aktual (allocated/forced/boot), signed allocation, jumlah message resources, NDIS hardware max/MSI-X/match source/BDF/support. Fallback snapshot diberi label. | Matching mengutamakan identitas lalu lokasi, tidak memilih NIC sejenis secara arbitrer; ambiguous/absent tetap Unknown. Probe NDIS read-only berhasil; matriks hardware MSI/MSI-X dan visual selection masih perlu pengujian. |
| F07 | Physical disk menggunakan MSFT_PhysicalDisk untuk Size/MediaType/HealthStatus. Fallback IOCTL memisahkan bus dari media dan menyebut kesehatan Unknown, bukan Detected/Healthy palsu. | Probe membaca dua SSD beserta kapasitas dan Healthy dari provider Windows. Tes enum unknown, HDD/SSD/SCM/Unspecified. USB/Storage Spaces/offline matrix belum diuji. |
| F08 | Runtime Install hanya aktif jika installable dan bukan repairable; Repair dan Enable tetap terpisah serta semuanya nonaktif ketika busy. | Seluruh 16 kombinasi busy/installable/repairable/enableable diuji. |
| F09 | Transaksi profil disimpan atomik dengan schema version, durasi, restart, KnownPowerGuids, 23 checks dan hasil rollback. Detail checks dimasukkan ke hasil Apply. Gagal menyimpan menghasilkan warning. | Tes sukses, gagal/rollback parsial, legacy alias, failed target, corrupt history, write-denied dan atomic preservation. Menggunakan direktori temp khusus tes, bukan ProgramData. Apply profil nyata tidak dijalankan. |

## Verifikasi yang dapat diulang

Dari folder proyek:

```powershell
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore
.\Tests\ParityAudit\Inspect-StaticParity.ps1
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
.\build-installer.ps1 -NoRestore
```

Hasil pass ini:

- **373 assertions lulus**: 73 profil sebelumnya + 300 regresi audit.
- Debug x64: **0 error, 0 warning**.
- Pemeriksaan statis: 20/20 handler Main UI tersambung, 0 parse error PS1,
  23 bahasa dan enam label sample tercakup.
- Probe read-only di luar sandbox: dua physical disks (512110190592 dan
  500107862016 byte, SSD/Healthy); tiga baris NDIS, max interrupt 16/32/unknown.
  MSI-X entries kosong ditampilkan not reported, bukan direkayasa.
- Query BitLocker: `0x80041003` (Access denied) pada token test. Tidak dilakukan
  eskalasi UAC atau operasi volume untuk menghilangkan hasil ini.
- Computer-use mencoba EXE staging `win-x64-20260904-192948`. OS mengembalikan
  jendela utama, tetapi helper melaporkan higher integrity. Capture tidak memverifikasi
  render Main UI. Tidak dilakukan klik menu atau bypass pembatasan, dan tidak
  ada klaim 20/20 menu runtime lulus pada pass ini.
- Setup dibangun, **tidak diinstal**. Install/upgrade/uninstall dan UI release final
  belum disertifikasi. Publisher tetap `Naufal Tech's Softwares`, tanpa perubahan
  identitas sertifikat/AppId.

## Artefak final

- Release Native AOT berhasil, termasuk generating native code.
- Final staging: `artifacts/publish/win-x64-20260904-193529`, berisi 160 file.
- Aplikasi: `Naufal Windows Powertoys.exe`, 18.380.288 byte.
  SHA-256: `32DE9667F80732C0386CA8B54D32900022AC5CD6D37108509EA194FABAFF98A7`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36.513.472 byte. SHA-256:
  `356667B2A714160A220A2E31599BE28D566B928E923641CB28D1BEDE2AAB6575`.
- CompanyName kedua executable: `Naufal Tech's Softwares`; versi `7.8.0.0`.
  Keduanya masih unsigned (`NotSigned`).
- Setup generated dengan nama yang sama diganti oleh build ini. Staging lama
  tidak dihapus, sehingga paket sebelumnya masih dapat dibangun ulang.
- Final staging berbeda dari staging intermediate yang sempat diluncurkan:
  hanya rincian sumber IRQ MSI ditambahkan setelah percobaan UI tersebut.
  Final EXE/Setup tidak diluncurkan atau diinstal pada pass ini.

## Lokasi implementasi

- BitLocker/WMI/disk: `SecurityInformationService.cs`, `BitLockerVolumeInfo.cs`,
  `NativeHardwareData.cs`, `NativeRscReader.cs`, `SystemReportService.cs`.
- Store: `MicrosoftStoreRepairService.cs`, `StoreResetStage.cs`.
- Progress: `NativeCommandRunner.cs`, `CommandOutputProgress.cs`,
  `MaintenanceProgress.cs`, `MaintenanceProgressWindow.cs`, semua service repair,
  `GpuDriverService.cs`, `GamingRuntimeInstallerService.cs`,
  `FirstRunPrerequisiteService.cs`, `MainWindow.xaml.cs`.
- Bahasa: `UiTranslation.cs`, `UiTextKeys.cs`, `SupplementalUiCatalog.cs`,
  `Tests/ParityAudit/Export-SupplementalTranslations.ps1`.
- MSI/gating: `MsiModeService.cs`, `MsiNdisIdentity.cs`, `RuntimeActionAvailability.cs`.
- Profil: `PerformanceProfileTransaction.cs`, `PerformanceProfileService.cs`,
  `PerformanceProfileExtendedService.cs`, `PerformanceProfileVerificationService.cs`.
- Tes: `Tests/ProfileVerification/AuditRegressionTests.cs`, `Program.cs`,
  test project, dan helper static parity yang membaca katalog gabungan.

## Referensi API primer

- Getter status BitLocker: [Win32_EncryptableVolume](https://learn.microsoft.com/en-us/windows/win32/secprov/win32-encryptablevolume)
  dan [GetConversionStatus](https://learn.microsoft.com/en-us/windows/win32/secprov/getconversionstatus-win32-encryptablevolume).
- Reset app data: [PackageDeploymentManager.ResetPackageAsync](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.management.deployment.packagedeploymentmanager.resetpackageasync?view=windows-app-sdk-2.0).
- Tipe media/kesehatan: [MSFT_PhysicalDisk](https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisk).

## Pengujian lanjutan yang belum diklaim selesai

Snapshot VM identik diperlukan untuk reset Store (absent/locked/current-user-unregistered),
servicing DISM/SFC dan service recovery, BitLocker encrypted/locked/paused/reboot,
Apply/rollback profil, pemasangan GPU/runtime, serta installer. Uji visual
23 bahasa × 8 skala × Light/Dark dan lifecycle child-window juga tetap terbuka.
