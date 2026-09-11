using System.Security.Principal;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal static class WindowsPrivilegeService
    {
        public static bool IsAdministrator()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
