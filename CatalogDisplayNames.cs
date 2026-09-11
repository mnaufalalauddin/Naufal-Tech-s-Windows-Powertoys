using System;
using System.Collections.Generic;

namespace Naufal_Windows_Tech_s_Powertoys;

// Presentation only: IDs, snapshot keys, commands and action descriptions stay intact.
internal static class CatalogDisplayNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["Widgets - Remove"] = "Taskbar Widgets",
        ["Show End Task on Taskbar"] = "Taskbar End Task",
        ["Use Classic Context Menu"] = "Classic Context Menu",
        ["Disable Consumer Features"] = "Windows Consumer Features",
        ["Disable Delivery Optimization P2P Sharing"] = "Delivery Optimization",
        ["Disable Hibernation"] = "Hibernation",
        ["Disable Location Tracking"] = "Location Tracking",
        ["Disable Microsoft Telemetry"] = "Windows Telemetry",
        ["Services - Set to Manual"] = "Service Startup",
        ["Disable File Explorer Home and Gallery"] = "File Explorer Home & Gallery",
        ["Disable Windows Artificial Intelligence"] = "Windows AI",
        ["Windows Recommendations / Suggested Content"] = "Windows Recommendations",
        ["Advertising ID / Personalized Experiences"] = "Advertising ID & Personalization",
        ["Taskbar Optional Buttons / Clutter"] = "Taskbar Buttons",
        ["File Explorer Cleanup / Background Discovery"] = "File Explorer Discovery",
        ["TSC Sync Policy - Enhanced"] = "TSC Sync Policy",
        ["Hypervisor Launch - OFF"] = "Hypervisor Launch",
        ["x2APIC Policy - Enable"] = "x2APIC Policy",
        ["PAE - ForceEnable"] = "PAE Policy",
        ["Kernel Debug + Boot Debug - OFF"] = "Kernel & Boot Debugging",
        ["SOS Boot Driver Messages - ON"] = "Boot Driver Messages",
        ["Highest Mode + Remove numproc Cap"] = "Boot Processor Configuration",
        ["Boot Manager Timeout - 1 second"] = "Boot Manager Timeout",
        ["Icon Cache Size - 100 MB"] = "Icon Cache Size",
        ["Temporary Files - Remove"] = "Temporary Files",
        ["Lock Pages in Memory - Current User"] = "Lock Pages in Memory",
        ["MTU Auto-Detection / Apply"] = "MTU Auto-Detection",
        ["Clear GPU Shader Cache"] = "GPU Shader Cache",
        ["Create System Restore Point"] = "System Restore Point",
        ["DNS - Cloudflare"] = "Cloudflare DNS",
        ["DNS - Google"] = "Google DNS",
        ["Explorer Network Folder Auto-Crawl Disable"] = "Explorer Network Discovery",
        ["Task View Button Hide"] = "Task View Button",
        ["Taskbar Chat / Teams Icon Hide"] = "Taskbar Chat / Teams",
        ["NVIDIA ShadowPlay / Overlay Disable"] = "NVIDIA ShadowPlay / Overlay",
        ["NVIDIA Control Panel Telemetry Opt-Out"] = "NVIDIA Telemetry",
        ["Prefetcher + Superfetch Registry Disable"] = "Prefetcher / Superfetch",
        ["Sticky / Toggle / Filter Keys Shortcut Popups Disable"] = "Accessibility Keyboard Shortcuts",
        ["Windows Visual Effects / Animations Disable"] = "Visual Effects / Animations",
        ["DWM UseDpiScaling Disable"] = "DWM DPI Scaling",
        ["Low Disk Space Notification Disable"] = "Low Disk Space Notifications",
        ["Show File Extensions"] = "File Extensions",
        ["UAC Secure Desktop Dimming Disable"] = "UAC Secure Desktop Dimming",
        ["Windows Startup Experience Disable"] = "Windows Startup Experience",
        ["Fast Startup / Hiberboot Enable"] = "Fast Startup",
        ["Win32 Long Path Support Enable"] = "Win32 Long Paths",
        ["Intel PPM Driver Disable"] = "Intel PPM Driver",
        ["NumLock at Sign-In Enable"] = "NumLock at Sign-In",
        ["NVIDIA CUDA Cache Maximum - 4 GB"] = "NVIDIA CUDA Cache",
        ["NVIDIA TDR Watchdog - 10 Seconds"] = "NVIDIA TDR Watchdog",
        ["NVIDIA L1 ECC Disable Flag"] = "NVIDIA L1 ECC",
        ["LanmanServer IRP Stack Size - 20"] = "LanmanServer IRP Stack Size",
        ["DPC Watchdog Experimental Override"] = "DPC Watchdog",
        ["Legacy IRQ8 / IRQ9 Priority Hints"] = "IRQ8 / IRQ9 Priority"
    };

    private static readonly Dictionary<string, string> WidgetTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "Taskbar Widgets", ["id"] = "Widget Taskbar", ["de"] = "Taskleisten-Widgets",
        ["fr"] = "Widgets de la barre des tâches", ["ar"] = "عناصر واجهة شريط المهام",
        ["tl"] = "Mga Widget sa Taskbar", ["vi"] = "Tiện ích thanh tác vụ",
        ["zh-CN"] = "任务栏小组件", ["zh-TW"] = "工作列小工具", ["th"] = "วิดเจ็ตบนแถบงาน",
        ["ru"] = "Виджеты панели задач", ["uk"] = "Віджети панелі завдань",
        ["pt"] = "Widgets da barra de tarefas", ["ja"] = "タスクバーのウィジェット",
        ["ko"] = "작업 표시줄 위젯", ["ur"] = "ٹاسک بار ویجٹس", ["ta"] = "பணிப்பட்டி விட்ஜெட்டுகள்",
        ["hi"] = "टास्कबार विजेट", ["ms"] = "Widget Bar Tugas", ["jv"] = "Widget Taskbar",
        ["ban"] = "Widget Taskbar", ["sv"] = "Widgetar i aktivitetsfältet", ["es"] = "Widgets de la barra de tareas"
    };

    public static string? LocalizedTitle(string name, string code) =>
        name == "Taskbar Widgets" && WidgetTitles.TryGetValue(code, out string? title) ? title : null;

    public static string Simplify(string name) => Names.TryGetValue(name, out string? simple) ? simple : name;
}
