using InstallReceipt.Core.Models;
using Microsoft.Win32;

namespace InstallReceipt.Platform.Windows.Scanning;

public sealed class ServiceRegistryScanner
{
    private const string ServicesPath = @"SYSTEM\CurrentControlSet\Services";

    public List<ServiceEntry> CaptureServices(List<string> warnings)
    {
        var services = new List<ServiceEntry>();

        try
        {
            using var servicesKey = Registry.LocalMachine.OpenSubKey(ServicesPath);
            if (servicesKey is null)
            {
                return services;
            }

            foreach (var serviceName in servicesKey.GetSubKeyNames())
            {
                using var serviceKey = servicesKey.OpenSubKey(serviceName);
                if (serviceKey is null)
                {
                    continue;
                }

                var type = ToInt(serviceKey.GetValue("Type"));
                if (!IsWin32Service(type))
                {
                    continue;
                }

                var imagePath = serviceKey.GetValue("ImagePath")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    continue;
                }

                services.Add(new ServiceEntry
                {
                    ServiceName = serviceName,
                    DisplayName = serviceKey.GetValue("DisplayName")?.ToString() ?? serviceName,
                    ExecutablePath = Environment.ExpandEnvironmentVariables(imagePath),
                    StartType = FormatStartType(ToInt(serviceKey.GetValue("Start"))),
                    Status = "Unknown",
                    Signer = string.Empty
                });
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($@"HKLM\{ServicesPath} を読み取れませんでした。{ex.Message}");
        }

        return services
            .OrderBy(service => service.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsWin32Service(int type)
    {
        const int serviceWin32OwnProcess = 0x00000010;
        const int serviceWin32ShareProcess = 0x00000020;
        return (type & serviceWin32OwnProcess) != 0 || (type & serviceWin32ShareProcess) != 0;
    }

    private static int ToInt(object? value)
    {
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => 0
        };
    }

    private static string FormatStartType(int start)
    {
        return start switch
        {
            0 => "Boot",
            1 => "System",
            2 => "Automatic",
            3 => "Manual",
            4 => "Disabled",
            _ => "Unknown"
        };
    }
}
