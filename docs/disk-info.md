# Disk Info and network report

## Read-only disk dashboard

Disk Info provides a horizontal device selector, selected-device heading,
Device SMART health and temperature cards, firmware/serial/interface/capacity details,
and reliability counters. The All disks / volumes / provider notes tab preserves
the original physical and logical drive backend. Copy and Save TXT export the
complete snapshot, not just the selected tab. Reopen Disk Info to refresh.

Total host reads and writes use decimal **TB** (1 TB = 1,000,000,000,000 bytes),
with thousands separators and two decimal places in both the dashboard and
Copy / Save TXT. Original NVMe data-unit counters remain in the detailed report.

The layout and standard SMART transports reference [CrystalDiskInfo](https://github.com/hiyohiyo/CrystalDiskInfo)
at revision `9ac83d03283f2dcb6047b8fb162463469a7b6c74`. The NVMe/ATA/SAT approach
and NVMe health rules have been adapted; its MIT notice is included in
THIRD-PARTY-NOTICES.txt and the installer. No CrystalDiskInfo binary, artwork or
vendor DLL is bundled. **This is not the complete CrystalDiskInfo engine.**

### Sources and limitations

- Native local WMI/COM reads `MSFT_PhysicalDisk` and the read-only
  `PS_StorageCmdlets.GetStorageReliabilityCounter` getter used by Windows'
  `StorageCmdlets.cdxml`. It does not depend on WMIC or a PowerShell subprocess.
- Counter results are tied to the physical CIM object and checked by DeviceId.
  Data is never matched by model name or enumeration order.
- The **Disk Health** card is derived from device SMART, never from Windows'
  generic disk status. Standard NVMe log 02h supplies critical warnings, spare,
  estimated endurance remaining, temperature, full 128-bit read/write counts,
  power cycles/hours, unsafe shutdowns, media errors and temperature sensors.
  Windows status remains a separately labelled diagnostic property.
- NVMe critical warnings/spare below threshold mean Bad. Endurance <= 10%,
  spare at threshold, or recorded media errors mean Caution; otherwise Good.
  The percentage is an endurance estimate, not a probability of disk failure.
  ATA health uses the device's read-only SMART RETURN STATUS and pre-failure
  thresholds; no vendor-specific SSD percentage is guessed. Missing health
  evidence is **Unknown**, even when Windows reports Healthy.
- Legacy ATA data/thresholds use the WMI failure-prediction providers. Valid
  512-byte blocks are checked for revision, checksum and duplicate IDs. The table
  shows ID, name hint, current, worst, threshold and hexadecimal raw value.
  Names/units can be vendor-specific. Unknown thresholds remain unknown.
- Direct device reads use MSFT_Disk.Number, verify the opened device number,
  and attach data only on a unique serial/size/bus match with the physical disk.
  Direct ATA/SAT attributes appear on that disk's own tab. Unmapped legacy WMI
  instances retain separate tabs instead of being assigned by list order.
- Unsupported providers, access denied and timeouts are reported explicitly.
  Slow reads have bounded caller waits; retry reuses any still-running probe.
- No self-tests, counter resets, firmware changes or disk writes are issued.

### Transport coverage (28 September 2026)

| Path | Support / verification |
| --- | --- |
| Native NVMe storage protocol query | Implemented; two real NVMe SSDs read successfully |
| ATA_PASS_THROUGH_EX | IDENTIFY, SMART data, thresholds and read-only return status; packet/parser tests; no native SATA hardware available for live validation |
| Standard USB SAT-16 | IDENTIFY, SMART data/thresholds and return-descriptor status; 27 attributes read on the test Kingston USB drive; its status was not verifiable, so health remains Unknown |
| Legacy WMI SMART | Retained as a fallback; unsupported by this test PC's drivers |
| ASMedia ASM2362 USB NVMe (`174C:2362`) and Realtek RTL9210 (`0BDA:9210`) | Read-only identify/log adapters implemented; exact PnP identity gating; synthetic packet/error tests only, no matching bridge available for live validation |
| Intel RST NVMe behind RAID (`iaStorAC` / `iaStorAVC`, Intel PCI parent) | Miniport identify/log path implemented for an OS-exposed disk whose controller serial matches exactly; synthetic packet, completion and identity tests only; not aggregate array health or hidden-member enumeration |
| SATA SSD endurance | Model-scoped Samsung retail/enterprise, Intel/Solidigm, Crucial/Micron, selected Kingston families (including SA400 firmware exception), Kioxia EXCERIA SATA/Toshiba TR rules; synthetic fixtures only |
| Other controllers/bridges | VROC/VMD, AMD RAID, CSMI, hidden RAID members, JMicron NVMe staging and Realtek dual-drive mode switching are not implemented; no complete CrystalDiskInfo parity claim |

### Vendor endurance and controller safeguards

SSD endurance is estimated remaining write endurance, **not years of life**.
Unrecognized models, missing/duplicate attributes and invalid percentage values
remain unreported. The presence of a vendor percentage alone does not upgrade
unknown overall ATA health to Good. Device failure/threshold evidence takes
precedence; remaining endurance <= 10% raises Caution. Raw attributes are retained.
Model selection uses ATA IDENTIFY, not the USB enclosure's marketing name.

USB vendor commands run only after the standard NVMe query fails and only for
the exact supported VID/PID found in the disk's PnP ancestry. A rejected vendor
read can still fall back to standard SAT. Intel RST checks miniport and NVMe
completion status, packet bounds, SCSI routing, and controller serial identity.
A logical RAID volume or ambiguous member remains Unknown; a member's health
is never presented as proof that an entire array is healthy.

The port references CrystalDiskInfo's pinned `AtaSmart.cpp` / `AtaSmart.h` above.
Firmware variants and different RAID driver versions can reject these paths.
No bridge mode-switching, controller reconfiguration or device-writing command
was ported. Hardware coverage must be expanded on machines containing those
specific controllers before claiming production hardware compatibility.

The Windows IOCTL interface requires a read/write handle for ATA/SAT pass-through,
but the app allowlists only read-only commands. Opening that handle does not mean
the program writes to the disk. SMART can wake a sleeping drive. All callers have
bounded waits; an unresponsive driver operation may continue in its retained worker.

The disk selector's content does not intercept pointer presses. Buttons own the
click event and can also be activated by keyboard. The regression host exercises
their automation Invoke path, not just the underlying SelectPage method.

References: [Microsoft reliability counters](https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-storagereliabilitycounter),
[Get-StorageReliabilityCounter](https://learn.microsoft.com/en-us/powershell/module/storage/get-storagereliabilitycounter),
[physical disk association](https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisktostoragereliabilitycounter),
[Microsoft disk driver WMI sample](https://github.com/microsoft/Windows-driver-samples/blob/main/storage/class/disk/src/diskwmi.c).
Additional protocol references: [Windows NVMe query API](https://learn.microsoft.com/en-us/windows/win32/fileio/working-with-nvme-devices),
[NVMe health log fields](https://learn.microsoft.com/en-us/windows/win32/api/nvme/ns-nvme-nvme_health_info_log),
[ATA pass-through structure](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddscsi/ns-ntddscsi-_ata_pass_through_ex).

## Optional IP and MAC addresses

System Report has a **Show IP and MAC addresses (also include in Copy / Save TXT)**
checkbox, unchecked when opened. Checking it reveals local adapters, their state,
MAC addresses, and unicast IPv4/IPv6 addresses. Unchecking removes that section
from both the display and subsequent exports. Disconnected, virtual and loopback
adapters are labelled, not silently treated as the primary connection.

There is no external request for a public IP address. Wi-Fi MAC randomization
may change the reported MAC. Other existing report fields, such as machine and
disk identity, are not anonymized by this checkbox; review reports before sharing.
