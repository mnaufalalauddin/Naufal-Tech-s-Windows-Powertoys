using Naufal_Windows_Tech_s_Powertoys;

internal static class CopilotConsentTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        check(!CopilotSourceConsent.IsGranted, "no source consent before confirmation");
        foreach (bool approved in new[] { false, true })
        foreach (var command in new[] { CopilotStorePolicy.ListArguments(approved), CopilotStorePolicy.RemoveArguments(approved) })
        {
            check(command.Contains("--accept-source-agreements") == approved, "source acceptance follows explicit consent");
            check(command.Contains("--disable-interactivity"), "CLI cannot hang on hidden source prompt");
            check(!command.Contains("--accept-package-agreements"), "lookup/removal does not authorize installations");
            check(command[Array.IndexOf(command, "--source") + 1] == "msstore", "only verified Microsoft Store source");
        }
        check(!CopilotStorePolicy.ListArguments().Contains("--accept-source-agreements") &&
            !CopilotStorePolicy.RemoveArguments().Contains("--accept-source-agreements"), "default commands never accept agreements");

        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> unrelated = Task.Run(async () => { await release.Task; return CopilotSourceConsent.IsGranted; });
        Task<bool> delayed;
        using (CopilotSourceConsent.BeginConfirmedOperation())
        {
            await Task.Yield();
            check(CopilotSourceConsent.IsGranted, "confirmed consent survives await");
            check(await Task.Run(() => CopilotSourceConsent.IsGranted), "consent reaches background catalog worker");
            using (CopilotSourceConsent.BeginConfirmedOperation())
                check(CopilotSourceConsent.IsGranted, "nested operation has consent");
            check(CopilotSourceConsent.IsGranted, "inner disposal preserves outer operation");
            delayed = Task.Run(async () => { await release.Task; return CopilotSourceConsent.IsGranted; });
        }
        check(!CopilotSourceConsent.IsGranted, "completion disposes source consent");
        release.SetResult();
        check(!await unrelated, "concurrent unrelated task never inherits consent");
        check(!await delayed, "delayed child cannot reuse disposed consent");
        try
        {
            using var consent = CopilotSourceConsent.BeginConfirmedOperation();
            throw new OperationCanceledException();
        }
        catch (OperationCanceledException) { }
        check(!CopilotSourceConsent.IsGranted, "cancellation also revokes consent");

        var rejected = new NativeCommandResult(-1978335162, "source agreements were not agreed to", "", false, TimeSpan.Zero);
        check(rejected.ExitCode == CopilotStorePolicy.SourceAgreementsNotAccepted, "screenshot HRESULT has documented agreement meaning");
        check(CopilotStorePolicy.Installed(rejected) is null, "agreement rejection is unknown, never absent");
        check(CopilotStorePolicy.InventoryFailure(rejected).Contains("Agree and continue"), "agreement error gives actionable retry");
        var timeout = new NativeCommandResult(-1978335162, "timeout", "", true, TimeSpan.Zero);
        check(!CopilotStorePolicy.InventoryFailure(timeout).StartsWith("Microsoft Store source consent is required"), "timeout is not mislabelled as consent denial");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string Read(string file) => File.ReadAllText(Path.Combine(root, file));
        foreach (string file in new[] { "MainWindow.xaml.cs", "MainWindow.BuiltInApps.cs" })
        {
            string source = Read(file);
            int confirm = source.IndexOf("if (needsStoreConsent && !await ConfirmCopilotSourceAsync(window)) return;");
            int grant = source.IndexOf("using var storeConsent = needsStoreConsent ? CopilotSourceConsent.BeginConfirmedOperation() : null;");
            check(confirm >= 0 && grant > confirm, "cancel returns before scope grant: " + file);
            check(source.IndexOf("AcquireManagedTaskAsync(", grant) > grant, "confirmation precedes task admission: " + file);
        }
        check(Read("MainWindow.xaml.cs").Contains("targetOn && changes.Any(item => item.Id == \"WindowsAI\")"), "Windows AI apply gets consent; unrelated tweaks do not");
        check(Read("MainWindow.BuiltInApps.cs").Contains("targets.Any(t => t.Id == \"Copilot\")"), "both Copilot uninstall and restore verify consent");
        string ui = Read("MainWindow.CopilotConsent.cs");
        check(ui.Contains("NavigateUri = new Uri(\"https://aka.ms/microsoft-store-terms-of-transaction\")"), "terms link is displayed, not auto-opened");
        check(ui.Contains("two-letter region code") && ui.Contains("WinGet may remember that acceptance"), "consent discloses region and persistent WinGet acceptance");
        check(ui.Contains("primaryButtonText: \"Agree and continue\", closeButtonText: \"Cancel\"") &&
            ui.Contains("== ToolWindowResult.Primary"), "only explicit agree grants consent");
        string service = Read("CopilotStoreService.cs");
        check(service.Contains("ListArguments(CopilotSourceConsent.IsGranted)") &&
            service.Contains("RemoveArguments(CopilotSourceConsent.IsGranted)"), "lookup and removal use operation-scoped consent");
        check(service.Contains("BuiltInAppsCatalog.IsMicrosoftStoreSource") && service.Contains("new SameUserProcessRunner()"), "official source and same-user unelevated route retained");
        string ai = Read("WindowsAiService.cs");
        check(ai.IndexOf("CopilotStoreService.ProbeAsync()") < ai.IndexOf("CaptureAndDisableAiServiceAsync()"), "source preflight before service mutation");
        string apps = Read("BuiltInAppsService.cs");
        check(apps.IndexOf("CopilotStoreService.ProbeAsync()") > apps.IndexOf("ReadForTargetsAsync"), "passive inventory stays deferred");
    }
}
