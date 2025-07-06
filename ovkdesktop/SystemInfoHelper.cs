using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ovkdesktop
{
    public static class SystemInfoHelper
    {
        public static string GetDotNetVersion()
        {
            var assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
            var assemblyPath = assembly.Location.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
            if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2)
            {
                return $".NET {assemblyPath[netCoreAppIndex + 1]}";
            }
            // Запасной вариант
            return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        }

        public static string GetAppVersion()
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                return "N/A (Unpackaged)";
            }
        }

        public static string GetOsVersion()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        }

        public static string GetOsBuildVersion()
        {
            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        string productName = key.GetValue("ProductName")?.ToString() ?? "N/A";
                        string displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "N/A"; 
                        string currentBuild = key.GetValue("CurrentBuild")?.ToString() ?? "N/A";

                        return $"{productName}, Version {displayVersion} (OS Build {currentBuild})";
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogWarning($"Could not read OS build version from registry: {ex.Message}");
            }

            return Environment.OSVersion.ToString();
        }

        public static string GetArchitecture()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
        }
    }
}
