# Audit lintas program dan perbaikan — 4 September 2026

## Kesimpulan

Sembilan kelompok temuan baru di bawah telah ditangani pada source. Pemeriksaan
meliputi koneksi seluruh 20 menu Main UI, backend/shared infrastructure, logika
Apply/Restore, lifecycle jendela, antrean, download, backup, dan packaging.
Pendalaman dilakukan pada jalur berisiko yang tertera di tabel, bukan eksekusi
setiap opsi di semua konfigurasi Windows. **Belum merupakan sertifikasi 1:1.**

Perbaikan tombol dari `CATALOG_BUTTON_FIX_2026-09-04.md` tetap berlaku: checkbox
menentukan scope; Apply memproses baris OFF yang dipilih; Restore memakai API
saved-state yang terpisah dari Windows defaults. Perubahan desain yang diminta
pengguna (jendela resizable, header bersih, toolbar bawah, branding) dipertahankan.

Tidak ada tweak registry/service/BCD, Apply profil, repair, operasi Defender atau
BitLocker, download/install driver/runtime, reboot, atau instalasi Setup yang
dijalankan pada PC pengguna. Tes memakai data sintetis, proses anak tes ringan,
dan direktori sementara milik tes. PS1 hanya dibaca/diparse; EXE acuan tidak
diluncurkan. UI build final belum diuji secara interaktif pada pass ini.

## Temuan dan perbaikan

| ID | Prioritas | Temuan dan perubahan | Verifikasi / batas |
|---|---|---|---|
| G01 | P2 | Antrean resource dapat disalip task baru yang memerlukan resource milik task lebih awal yang masih menunggu. Scheduler kini mempertimbangkan antrean sebelumnya, bukan hanya lock aktif. | Tes X → X+Y → Y membuktikan FIFO; task Z independen tetap berjalan. |
| G02 | P2 | Riwayat hanya menampilkan 30 baris terbaru sehingga task aktif lama hilang; Dispose tanpa hasil eksplisit dianggap COMPLETED. Kini semua RUNNING/QUEUED tetap terlihat dan lease tanpa hasil menjadi INTERRUPTED. | Tes 40 task aktif + 40 selesai, duplicate ID, pelepasan lock setelah gagal, Dispose idempotent. |
| G03 | P1 | GPU/runtime memindahkan file download saat handle FileShare.None masih terbuka. Kini helper atomik menutup handle sebelum rename, memeriksa panjang/limit, memakai temporary file unik, dan mempertahankan cache lama bila gagal. | Sukses/replacement Windows, ukuran kurang/lebih/unknown/empty, cancellation, callback failure, cleanup milik sendiri. HTTPS allow-list dan pemeriksaan tanda tangan tetap dipertahankan; tidak ada paket internet diunduh. |
| G04 | P1 | Penanda Captured ditulis sebelum isi snapshot lengkap di beberapa backend/import. Kini data dan flush mendahului marker; marker lama dengan data tidak lengkap ditolak. | Helper dipakai di Essential, Gaming, De-Bloat, Registry Lab, Network/Storage, Service Groups, Windows AI, Xbox. Tes kegagalan pada setiap tahap penulisan sebelum commit, kind invalid, absent-vs-zero, dan snapshot legacy tidak lengkap. |
| G05 | P1 | Restore dapat membuang backup terlalu dini atau melaporkan sukses hanya dari status agregat. Service Groups kini memvalidasi snapshot sebelum perubahan, membandingkan startup/delayed/running/registry dengan snapshot, dan hanya menghapus backup setelah verifikasi. Essential/Gaming melakukan read-back nilai registry (termasuk kind/absence), menunda cleanup backup sampai hasil, menolak saved Restore tanpa snapshot; Gaming memverifikasi BCD. Essential memeriksa keadaan layanan melalui SCM, tidak menafsirkan teks Inggris sebagai status. | Tes validator snapshot dan read-back sintetis. Missing/invalid running-state tidak dianggap Stopped. Snapshot kosong tidak dapat lolos. Pengujian service transition dan boot/driver/hardware asli tetap memerlukan VM/perangkat uji. Restore multi-item bukan transaksi rollback seluruh Windows. |
| G06 | P1 | GPU/runtime/BitLocker/profil bisa menerima klik lagi saat menunggu konfirmasi/antrean. Gate sekarang diambil sebelum await pertama, mengunci aksi/selection dan dilepas pada cancel/error/finally. | Tes gate rapid-click, 32 concurrent attempts, old-lease double-dispose. Wiring UI diperiksa dan dikompilasi; klik nyata belum disertifikasi. |
| G07 | P1 | Tool dapat ditutup ketika operasi/prompt pending; penutupan owner menyisakan child; Exit dan Reboot mempunyai celah ketika pekerjaan dimulai selama konfirmasi. Tool kini melacak owner/child dan busy state; tombol Close/Exit serta native Closing dijaga. Reboot mengunci prompt dan memeriksa ulang pekerjaan setelah konfirmasi, timer UI tidak boleh mengaktifkannya kembali. Callback live status berhenti merender setelah MainWindow ditutup. | Pemeriksaan source/build; tidak ada reboot/termination operasi Windows untuk pengujian. Tidak ada klaim bahwa XAML/UI lifecycle telah diuji end-to-end. |
| G08 | P1 | Permintaan command yang sudah cancelled atau memiliki timeout invalid dapat meluncurkan child terlebih dahulu. Validasi sekarang sebelum Process.Start. | Tes cancelled + executable tidak ada dan timeout invalid membuktikan validasi lebih dahulu; tes streaming/timeout/cancellation sebelumnya tetap lulus. |
| G09 | P2 | SkipPublish memilih direktori terbaru walaupun publish gagal/kosong; installer mempunyai fallback ke folder lama; parameter versi hanya menamai Setup. Sekarang staging dibuat setelah publish sukses, diberi completion marker + hash EXE, SkipPublish memilih versi/hash yang valid, SourceDir wajib eksplisit, versi dikirim ke app dan installer. | Lima tes staging: latest valid, incomplete, tampered EXE, version mismatch, corrupt marker/missing EXE. Build Setup dilakukan; install/upgrade/uninstall belum dijalankan. Marker memvalidasi EXE dan keberhasilan copy, bukan tanda tangan keamanan seluruh isi paket. |

## Cakupan per kelompok menu

| Area | Pemeriksaan pada pass ini | Belum disertifikasi |
|---|---|---|
| Full/Quick/Update/Store/Explorer repair | Routing, shared task lifecycle, progress regression, native command cancellation | Efek repair riil, package-in-use, reboot/recovery dan elapsed/progress visual |
| Disk/System Report/Windows & Office Activation | Routing, layanan laporan dan regresi data; activation tetap informasi, bukan bypass | Seluruh vendor/hardware/Office install path, export dialog visual |
| Defender/Smart App Control/BitLocker | Routing, guard, shared backup; BitLocker gate dan regresi read-back | Proteksi Windows riil, encryption transitions, UAC dan policy-managed PC |
| Essential/Gaming/Advanced | Scope Apply/Restore, gate, commit snapshot, read-back dan service group restore | Semua kombinasi kebijakan, services absent, restore setelah reboot, cross-version snapshot di VM |
| GPU/Runtime Compatibility | Gate, download atomik, signature checks tetap ada, action availability | Resolver vendor live, instalasi driver, silent installer, dependency matrix |
| MSI Mode/Legacy Panels | Routing, busy guard MSI dan regresi identity/hardware | Perubahan interrupt nyata, device matrix, semua panel Windows |
| Profil/Live Status/Tasks | 23-check regression, FIFO/history, profile gate, shutdown callback guard | Efek kinerja aktual, perubahan profil pada hardware riil, refresh visual |
| Tema/skala/bahasa | Wiring source, delapan persentase, shared display engine, 23×396 entri | Visual semua menu × tema × skala, seluruh teks dinamis terjemahan |
| Startup/publish/Setup | Source lifecycle/branding, Debug + Native AOT + compiler installer | Startup UI release, install/upgrade/uninstall pada VM bersih, signing |

## Bukti pengujian

- Debug x64: **0 error, 0 warning** setelah perubahan final source.
- **484 assertions regresi sintetis lulus**, termasuk 72 tambahan pada audit ini.
- **5 assertions staging installer lulus**. Total 489 checks; bukan 489 fitur atau
  pengujian admin end-to-end.
- Static parity: **20/20 koneksi handler**, **23 bahasa × 396 entri**, tujuh label
  sample mempunyai 22/22 terjemahan non-English, PS1 nol parse error.
- Reference PS1 SHA-256: `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
- Reference EXE SHA-256: `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`.
- Salinan PS1 proyek identik dengan acuan. 649 fungsi dan tiga nama fungsi
  berulang: jumlah fungsi/route bukan pembuktian equivalence perilaku.

Perintah reproduksi dari root proyek:

```powershell
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore
.\Tests\ParityAudit\Test-PublishStage.ps1
.\Tests\ParityAudit\Inspect-StaticParity.ps1
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
.\build-installer.ps1 -NoRestore
```

`-SkipPublish` kini mensyaratkan staging ber-marker lengkap dari build script baru.
Staging historis tanpa marker tetap disimpan, tetapi tidak dipilih otomatis.
Publisher tetap **Naufal Tech's Softwares**; AppId/signing identity tidak diubah.

## Artefak final

- Native AOT berhasil, termasuk tahap `Generating native code`; compiler Inno
  Setup berhasil. Executable/installer final tidak diluncurkan pada pass ini.
- Staging lengkap: `artifacts/publish/win-x64-20260904-213243-252` (161 file,
  termasuk `publish-complete.json`). Helper memverifikasi ulang hash marker.
- Aplikasi: `Naufal Windows Powertoys.exe`, **18.441.216 byte**.
  SHA-256: `1D92D47C28703E8590CFD1AA2F9B92242F8182839D9B0606D26386C528F82932`.
- Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  **36.522.585 byte**.
  SHA-256: `D0AAED2E0CF9A029E199C3518E60317056202E6E5AA28B1D49A090DD91AC7355`.
- FileVersion keduanya `7.8.0.0`; CompanyName `Naufal Tech's Softwares`.
  Keduanya **NotSigned**. Metadata publisher bukan sertifikat Authenticode.
- Setup lama dengan nama yang sama diganti oleh hasil build ini. Folder staging
  sebelumnya tidak dihapus; build intermediate `win-x64-20260904-212807-022`
  bukan build final di atas.
