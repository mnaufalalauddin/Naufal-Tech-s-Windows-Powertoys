using System;
using System.Globalization;
using System.IO;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal static class ServiceRestoreSnapshot
    {
        // Validate every field before any service in the group is changed.
        public static bool Validate(Func<string, object?> read, string service, bool installed)
        {
            string start = $"{service}.Start";
            if (!RegistrySnapshotCommit.IsCaptured(read, start))
            {
                if (!installed) return false;
                throw new InvalidDataException($"The original startup snapshot for {service} is missing. Restore was not started.");
            }
            if (read($"{start}.Exists") is not int exists || exists != 1 ||
                read($"{start}.Kind") is not string kind || kind != "DWord" ||
                !int.TryParse(read($"{start}.Value") as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode) ||
                mode is not (2 or 3 or 4) || !RegistrySnapshotCommit.IsCaptured(read, $"{service}.Delayed"))
                throw new InvalidDataException($"The service snapshot for {service} is incomplete. Restore was not started.");
            ReadRunningState(read($"{service}.Running"), service);
            return true;
        }

        public static ServiceStartupRestorePlan CreatePlan(
            int savedStart,
            int? delayedAutoStart,
            bool wasRunning,
            int? documentedWindowsDefault)
        {
            ValidateStart(savedStart, nameof(savedStart));
            if (documentedWindowsDefault.HasValue)
            {
                ValidateStart(documentedWindowsDefault.Value, nameof(documentedWindowsDefault));
            }

            int targetStart = documentedWindowsDefault ?? savedStart;
            bool usesDefault = documentedWindowsDefault.HasValue;
            return new ServiceStartupRestorePlan(
                targetStart,
                delayedAutoStart,
                ShouldStart: usesDefault ? targetStart == 2 : wasRunning && targetStart != 4,
                ShouldStop: usesDefault ? targetStart == 4 : !wasRunning || targetStart == 4,
                VerifyRuntime: !usesDefault || targetStart == 2,
                usesDefault);
        }

        public static ServiceStartupRestorePlan CreateWindowsDefaultPlan(
            int documentedWindowsDefault,
            int? delayedAutoStart)
        {
            ValidateStart(documentedWindowsDefault, nameof(documentedWindowsDefault));
            return new ServiceStartupRestorePlan(
                documentedWindowsDefault,
                delayedAutoStart,
                ShouldStart: documentedWindowsDefault == 2,
                ShouldStop: documentedWindowsDefault == 4,
                VerifyRuntime: documentedWindowsDefault == 2,
                UsesDocumentedDefault: true);
        }

        public static ServiceStartupRestorePlan ApplyDocumentedRuntimePolicy(
            string serviceName,
            ServiceStartupRestorePlan plan)
        {
            if (!plan.UsesDocumentedDefault || plan.TargetStart != 2)
            {
                return plan;
            }

            // Automatic trigger-start services are still started, but Windows
            // may immediately stop them when their trigger is absent. These
            // continuously-running services must remain Running before a
            // restore can be reported as successful.
            bool mustRemainRunning = serviceName.Equals("SysMain", StringComparison.OrdinalIgnoreCase) ||
                                     serviceName.Equals("CryptSvc", StringComparison.OrdinalIgnoreCase) ||
                                     serviceName.Equals("DPS", StringComparison.OrdinalIgnoreCase) ||
                                     serviceName.Equals("TrkWks", StringComparison.OrdinalIgnoreCase) ||
                                     serviceName.Equals("DiagTrack", StringComparison.OrdinalIgnoreCase) ||
                                     serviceName.Equals("Spooler", StringComparison.OrdinalIgnoreCase);
            return plan with { VerifyRuntime = mustRemainRunning };
        }

        public static string ToScStartMode(int start, int? delayedAutoStart)
        {
            ValidateStart(start, nameof(start));
            return start switch
            {
                2 when delayedAutoStart == 1 => "delayed-auto",
                2 => "auto",
                3 => "demand",
                4 => "disabled",
                _ => throw new InvalidDataException($"Unsupported service startup value: {start}.")
            };
        }

        internal static bool NeedsStartupConfiguration(int? actualStart, int? actualDelayed,
            ServiceStartupRestorePlan plan) => actualStart != plan.TargetStart ||
            (plan.TargetStart == 2 && (actualDelayed == 1) != (plan.DelayedAutoStart == 1));

        public static string FormatStart(int start) => start switch
        {
            2 => "Automatic",
            3 => "Manual",
            4 => "Disabled",
            0 => "Boot",
            1 => "System",
            _ => $"Unknown ({start.ToString(CultureInfo.InvariantCulture)})"
        };

        public static bool ReadRunningState(object? value, string service) => value switch
        {
            int state when state == 0 => false,
            int state when state == 1 => true,
            _ => throw new InvalidDataException($"The original running state for {service} is missing or invalid. Restore was not started.")
        };

        private static void ValidateStart(int start, string parameterName)
        {
            if (start is not (2 or 3 or 4))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    start,
                    "Only Automatic (2), Manual (3), and Disabled (4) service startup modes are supported.");
            }
        }
    }

    internal readonly record struct ServiceStartupRestorePlan(
        int TargetStart,
        int? DelayedAutoStart,
        bool ShouldStart,
        bool ShouldStop,
        bool VerifyRuntime,
        bool UsesDocumentedDefault);
}
