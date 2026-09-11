# Audit lintas program — 5 September 2026

## Kesimpulan dan batas pengujian

Nama `Widgets - Remove` kini ditampilkan sebagai **Taskbar Widgets**. Nama opsi
sejenis diringkas pada model katalog toggle/action, tanpa mengubah ID, kunci
backup, command, deskripsi efek, tombol aksi, maupun konfirmasi risiko. Judul
Widgets mempunyai padanan untuk 23 bahasa. Terjemahan seluruh nama baru dan
semua teks dinamis belum disertifikasi lengkap; teks tanpa padanan tetap memakai
fallback English. Reference PS1/EXE tidak diedit atau dieksekusi.

Audit ini memeriksa seluruh 20 koneksi menu Main UI, shared infrastructure,
catalog state/restore/progress, metadata, dan mengulang tes regresi lintas modul.
Pendalaman dan perubahan ada pada temuan di bawah. **Belum merupakan bukti bahwa
seluruh fitur dan behavior sudah 1:1** pada semua versi Windows/perangkat.

Percobaan membuka build Debug dengan skill Computer Use berhenti di UAC.
Tidak ada persetujuan UAC yang diotomatisasi. Karena itu tidak ada klaim lulus
smoke-test visual, startup Native AOT, atau matriks semua menu × tema × skala.
Tidak ada Apply/Restore Windows, repair, instalasi driver/runtime, operasi
Defender/BitLocker, reboot, atau instalasi Setup yang dijalankan untuk audit ini.

## Temuan yang diperbaiki

| ID | Temuan | Perbaikan dan bukti |
|---|---|---|
| A01 | Judul katalog masih memuat Remove/Disable/Enable, parameter aksi, dan sebagian label risiko. | `CatalogDisplayNames` menyederhanakan judul yang dikenal pada kedua model katalog. Snapshot ID tetap sama; deskripsi/peringatan tidak dihapus. Tes Widgets, action label, identitas, idempotensi dan 23 padanan bahasa. |
| A02 | Timeout deployment memanggil Cancel lalu mengklaim pembatalan berhasil meskipun belum selesai; Close dipanggil pada operasi pending. | Factory deployment berada di balik `BoundedOperationGate`: timeout 2 menit (5 menit App Installer), grace 5 detik, status cancel terkonfirmasi/tidak terkonfirmasi dibedakan. Operasi AppX selanjutnya ditolak sebelum factory berjalan selama operasi lama pending; resource ditutup hanya setelah task terminal. Tes hung/cancel/late success/late fault/factory failure. |
| A03 | Enumerasi paket Widgets sinkron dapat macet sebelum timeout uninstall berlaku. | Inventory dipindahkan ke worker read-only dengan batas tunggu 30 detik. Retry berbagi satu probe yang masih pending, bukan menambah worker macet. Essential menolak perubahan bila current state tidak dapat dibaca. Tes timeout berulang dan satu probe. Batas ini tidak berarti API Windows dipaksa berhenti; UI mendapat hasil timeout. |
| A04 | Task yang gagal sebelum persentase pertama mempunyai bar merah bernilai nol; task tersisa dapat terus RUNNING/WAITING sesudah batch berhenti. Verifying memakai angka 85% buatan. | State progress terpisah dan diuji: FAILED merah penuh dengan label kegagalan, task belum mulai SKIPPED, completion tanpa verifikasi NOT VERIFIED. Callback terlambat tidak menghidupkan task lagi. Verifying memakai indeterminate; persentase deployment asli berlabel `Windows step` agar tidak disamakan dengan keseluruhan opsi multi-tahap. Overall menghitung item yang selesai diproses, bukan menjanjikan estimasi waktu. |
| A05 | Progress maintenance hanya punya teks per tahap; tombol X bisa menutup progress saat backend berjalan. | Tiap tahap sekarang mempunyai bar hijau/merah, dan busy guard ToolWindow mencakup native Closing. Status dan bar dicat ulang saat tema berubah. Diuji melalui kompilasi; visual dan native-close interaction belum diuji. |
| A06 | Restore biasa melewati opsi gabungan yang sebagian diterapkan karena IsOn hanya true bila seluruh nilai cocok. | `HasAppliedParts` ditambahkan pada state dan diteruskan dari service groups, registry lab, performance lab, low-risk de-bloat, BCD dan agregasi Advanced. Scope Restore menerima ON maupun parsial; Apply tetap menargetkan yang belum sepenuhnya ON. Truth table selection/availability/on/partial diuji. |
| A07 | Missing snapshot satu child Advanced dapat memicu Windows-default fallback untuk seluruh composite, menimpa sibling yang sudah berhasil direstore. | Fallback diproses per child; result menandai bahwa fallback telah ditangani sehingga parent tidak mengulangnya. Vendor state yang tidak diketahui tetap tidak ditebak. Tes runner memastikan parent tidak mengulang fallback. |
| A08 | Gaming BCD memverifikasi restore dengan invers IsOn dan menghapus snapshot sebelum read-back. Snapshot bisa tidak lengkap atau menunjuk opsi lain. | Semua item snapshot divalidasi sebelum penulisan; restore dibandingkan dengan nilai tersimpan/default yang sebenarnya, termasuk absent vs explicit OFF dan alias boolean BCD. Backup dihapus setelah read-back sukses. Nilai tersimpan yang juga ON tetap sah. Tes comparer sintetis; bcdedit tidak dijalankan untuk mutasi. |
| A09 | Wizard Don’t show again dapat diabaikan ketika schema berubah. | Opt-out mendapat prioritas; Skip tetap incomplete/non-suppressed dan muncul lagi. JSON rusak kembali menampilkan wizard. Tes Skip, opt-out schema lama, selesai dan rusak. |
| A10 | Bar MSI dapat hijau walaupun VerifiedConfiguration kosong. | Bar dan teks memakai syarat Success **dan** VerifiedConfiguration, sama dengan hasil batch. Kompilasi/wiring diperiksa; device tidak diubah. |
| A11 | Process cleanup menganggap Kill langsung selesai dan tidak masuk antrean task bersama. | Backend menunggu exit maksimal 5 detik per proses, caller memeriksa ulang. UI busy guard, penguncian pilihan, resource SystemMutation/catalog dan detail `Ending:` ditambahkan. Exception membuka Process Manager ditangani. Tidak ada proses pengguna dihentikan untuk pengujian. |

Cancellation WinRT adalah permintaan, bukan jaminan operasi sudah berhenti;
ini menjadi dasar A02. Lihat [Microsoft: cancelling a Windows Runtime operation](https://devblogs.microsoft.com/oldnewthing/20200701-00/?p=103916)
dan [IAsyncInfo.Close](https://learn.microsoft.com/en-us/uwp/api/windows.foundation.iasyncinfo.close).

## Cakupan permintaan sebelumnya

| Area | Pemeriksaan / keadaan source | Uji yang masih diperlukan |
|---|---|---|
| Essential / Gaming / Advanced | Model judul, badge risiko/deskripsi, warnings printing/update, toolbar bawah, selection, restore, snapshot, deployment dan progress ditelusuri; perbaikan A01–A08. | Apply/Restore pada VM bersih, legacy snapshot, service transition, package-in-use dan pemulihan setelah reboot. |
| Full/Quick/Explorer/Update/Store repair | Routing dan shared maintenance, command/backup regressions; bar tiap tahap dan guard diperbaiki. | Efek repair riil, warning Store resources-in-use, elapsed dan persentase visual. |
| Disk / System Report / Windows & Office Activation | Handler terhubung; activation tetap informasi, bukan bypass. Regresi data sebelumnya dijalankan ulang. | Vendor/hardware, seluruh jenis instalasi Office, export dan scrolling dark mode. |
| GPU Driver Manager / Games Runtime Compatibility | Handler dan layanan analisis/install terhubung; regresi download atomik, availability, signature/gate sebelumnya dijalankan ulang. | Resolusi paket vendor terkini, download/install/repair nyata, restart dan seluruh matriks dependencies. |
| MSI / Legacy Panels | Handler, busy gate, koreksi hasil verifikasi MSI. | Matrix PCI/IRQ/MSI, semua panel legacy dan driver setelah restart. |
| Defender / BitLocker / Smart App Control | Handler/shared lifecycle serta backup regressions. Tidak ada kontrol security yang dijalankan. | Windows protections, UAC, policy-managed PC dan encryption transition. |
| Profiles / Live Status / Active Tasks | Regresi verifikasi profil, data live, resource queue/history; source header 🔴 LIVE GAMING STATUS dan penghapusan LIVE SYSTEM SNAPSHOT dipertahankan. | Refresh visual 1 detik, performa dan hasil profile pada hardware nyata. |
| Theme / scaling / language / window layout | Source baseline non-kumulatif, template minimum CheckBox/ToggleSwitch, 8 skala, resizable ToolWindow, 23 bahasa, header bersih diperiksa. | Screenshot seluruh menu pada Light/Dark × 8 skala; teks dinamis belum semuanya terlokalisasi. |
| First Time Wizard / publish / Setup / branding | A09, static publisher dan tes staging. Product dan EXE name tidak berubah. | Startup release, install/upgrade/uninstall, Authenticode signing. Metadata publisher bukan tanda tangan digital. |

## Bukti referensi

- Acuan PS1 identik dengan salinan proyek; nol parse error, 649 definisi fungsi.
- PS1 SHA-256: `660CD561C96FC00FBB26F15DFC94C4E03F4B4594E76C1D72A021C8E2CC18833D`.
- EXE SHA-256: `C920ADF8AB56F644DBAA8EA470F9C4B3248A9CA1DCBD15C71AC980F9C9A01BFF`.
- 20/20 route terhubung. 23 bahasa pada tabel embedded; tujuh label sampel
  masing-masing mempunyai 22/22 terjemahan non-English. Ini bukti struktur,
  bukan pengujian 649 fungsi atau sertifikasi equivalence.
- Source UI tidak memuat literal `V78(91)`, `ONE-SHOT`, `LIVE SYSTEM SNAPSHOT`,
  atau kalimat header restore yang diminta dihapus. File referensi dan catatan
  historis dikecualikan karena merupakan bahan audit, bukan deskripsi UI.

## Reproduksi

Jalankan dari root proyek:

```powershell
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
dotnet run --project Tests\ProfileVerification\ProfileVerification.Tests.csproj -c Debug --no-restore
.\Tests\ParityAudit\Inspect-StaticParity.ps1
.\Tests\ParityAudit\Test-PublishStage.ps1
.\build-installer.ps1 -NoRestore
```

## Hasil final dan artefak

- Debug x64: 0 error, 0 warning.
- 615 assertions regresi sintetis lulus, termasuk nama opsi, partial selection,
  terminal progress, wizard, BCD comparison dan timeout/cancellation/inventory.
- 5 assertions publish-stage lulus. Jumlah assertions bukan jumlah fitur yang
  telah diuji end-to-end.
- Native AOT berhasil melewati `Generating native code`; Inno Setup berhasil.
- Staging hash-verified: `artifacts/publish/win-x64-20260905-234755-858`.
- EXE: `Naufal Windows Powertoys.exe`, 18.690.560 byte,
  SHA-256 `676F278D7A0DBAC10964DF262144685356B7FCE570CDEFE8D62D825D977F2F89`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36.579.446 byte,
  SHA-256 `AC98A966E6228B70652C487F674B904878C38C76C6387E392420CAA56228D55B`.
- FileVersion keduanya `7.8.0.0`, CompanyName `Naufal Tech's Softwares`.
  Keduanya masih **NotSigned**; audit ini tidak menyediakan sertifikat signing.
- Installer lama dengan nama sama diganti oleh hasil kompilasi ini. Folder
  staging historis tetap disimpan. Jangan menyalin EXE sendirian untuk distribusi:
  gunakan Setup atau seluruh isi folder staging beserta runtime pendamping.

Pengujian UAC/admin/installer di PC pengguna tidak dijalankan. Uji UI Debug
tertahan pada prompt UAC; executable publish final belum dijalankan.
