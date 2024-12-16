using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;

public class HardwareInfo
{
    private static string GetCpuId()
    {
        string cpuId = string.Empty;
        var searcher = new ManagementObjectSearcher("select ProcessorId from Win32_Processor");

        foreach (var item in searcher.Get())
        {
            cpuId = item["ProcessorId"]?.ToString();
            break;
        }

        return cpuId;
    }

    private static string GetMacAddress()
    {
        var macAddress = NetworkInterface
            .GetAllNetworkInterfaces()
            .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                   nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                   nic.GetPhysicalAddress().ToString() != "")
            ?.GetPhysicalAddress()
            .ToString();

        return macAddress ?? string.Empty;
    }

    private static string GetUUID()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["UUID"]?.ToString() ?? "Unknown";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving UUID: {ex.Message}");
        }
        return "Unknown";
    }

    private static string GetSystemUptime()
    {
        try
        {
            // Calculate uptime from TickCount64 (milliseconds)
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return $"{uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes, {uptime.Seconds} seconds";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving system uptime: {ex.Message}");
            return "Unknown";
        }
    }

    public static string GetHardwareInfo()
    {
        string cpuId = GetCpuId();
        string macAddress = GetMacAddress();
        string uuid = GetUUID();
        string systemUptime = GetSystemUptime();

        return $"{cpuId}|{macAddress}|{uuid}";
    }
}


/* using System;
using System.IO;
using System.Management;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Xml;

namespace SystemInfoChecker
{
    class Program
    {
        static string startupFilePath = Path.Combine("systeminfo.json");

        static void Main(string[] args)
        {
            try
            {
                // Retrieve system information
                var currentSystemInfo = GetSystemInformation();

                // Print current system information
                Console.WriteLine("Current System Information:");
                PrintSystemInfo(currentSystemInfo);

                // Check if a previous file exists
                if (File.Exists(startupFilePath))
                {
                    // Read the previous system info
                    var previousSystemInfo = JsonConvert.DeserializeObject<SystemInfo>(File.ReadAllText(startupFilePath));

                    // Compare with the previous startup info
                    Console.WriteLine("\nComparing with the previous startup information...");
                    CompareSystemInfo(previousSystemInfo, currentSystemInfo);
                }
                else
                {
                    Console.WriteLine("\nNo previous system information found. Saving current info...");
                }

                // Save the current system information to the startup folder
                File.WriteAllText(startupFilePath, JsonConvert.SerializeObject(currentSystemInfo, Newtonsoft.Json.Formatting.Indented));
                Console.WriteLine($"\nSystem information saved at: {startupFilePath}");
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            // Wait for user to press a key before exiting
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        // Retrieve full system information
        static SystemInfo GetSystemInformation()
        {
            return new SystemInfo
            {
                CPU = GetHardwareInfo("Win32_Processor", "Name"),
                CPUCores = GetHardwareInfo("Win32_Processor", "NumberOfCores"),
                CPUSpeed = GetHardwareInfo("Win32_Processor", "MaxClockSpeed"),
                Motherboard = GetHardwareInfo("Win32_BaseBoard", "Manufacturer") + " " + GetHardwareInfo("Win32_BaseBoard", "Product"),
                BIOSVersion = GetHardwareInfo("Win32_BIOS", "SMBIOSBIOSVersion"),
                RAMSize = (Convert.ToDouble(GetHardwareInfo("Win32_PhysicalMemory", "Capacity")) / (1024 * 1024 * 1024)).ToString("0.00") + " GB",
                RAMType = GetRAMType(),
                RAMSpeed = GetHardwareInfo("Win32_PhysicalMemory", "Speed") + " MHz",
                Storage = GetStorageDevices(),
                GPU = GetHardwareInfo("Win32_VideoController", "Name"),
                GPUVRAM = (Convert.ToDouble(GetHardwareInfo("Win32_VideoController", "AdapterRAM")) / (1024 * 1024 * 1024)).ToString("0.00") + " GB",
                NetworkAdapters = GetNetworkAdapters(),
                OS = Environment.OSVersion.ToString(),
                OSVersion = GetHardwareInfo("Win32_OperatingSystem", "Version"),
                RunningProcesses = System.Diagnostics.Process.GetProcesses().Length,
                EnvironmentVariables = GetEnvironmentVariables(),

                // Additional information
                DiskID = GetDiskID(),
                UUID = GetUUID(),
                MachineID = GetMachineID(),
                SerialNumber = GetSerialNumber()
            };
        }

        static string GetDiskID()
        {
            try
            {
                string diskId = string.Empty;
                using (var searcher1 = new ManagementObjectSearcher("Select * from Win32_DiskDrive"))
                {
                    foreach (ManagementObject mo in searcher1.Get())
                    {
                        return mo["SerialNumber"]?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Disk ID: {ex.Message}");
            }
            return "Unknown";
        }

        static string GetUUID()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["UUID"]?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving UUID: {ex.Message}");
            }
            return "Unknown";
        }

        static string GetMachineID()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Machine ID: {ex.Message}");
            }
            return "Unknown";
        }

        static string GetSerialNumber()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Serial Number: {ex.Message}");
            }
            return "Unknown";
        }

        static List<StorageDevice> GetStorageDevices()
        {
            var devices = new List<StorageDevice>();
            var searcher = new ManagementObjectSearcher("SELECT Model,Size,MediaType FROM Win32_DiskDrive");
            foreach (ManagementObject obj in searcher.Get())
            {
                devices.Add(new StorageDevice
                {
                    Model = obj["Model"]?.ToString() ?? "Unknown",
                    Type = obj["MediaType"]?.ToString() ?? "Unknown",
                    Capacity = (Convert.ToDouble(obj["Size"]) / (1024 * 1024 * 1024)).ToString("0.00") + " GB"
                });
            }
            return devices;
        }

        static List<NetworkAdapter> GetNetworkAdapters()
        {
            var adapters = new List<NetworkAdapter>();
            var searcher = new ManagementObjectSearcher("SELECT MACAddress,IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (ManagementObject obj in searcher.Get())
            {
                adapters.Add(new NetworkAdapter
                {
                    MACAddress = obj["MACAddress"]?.ToString() ?? "Unknown",
                    IPAddress = (obj["IPAddress"] as string[])?.FirstOrDefault() ?? "Unknown"
                });
            }
            return adapters;
        }

        static string GetRAMType()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT MemoryType FROM Win32_PhysicalMemory");
                var type = searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["MemoryType"];
                return type switch
                {
                    20 => "DDR",
                    21 => "DDR2",
                    24 => "DDR3",
                    26 => "DDR4",
                    _ => "Unknown"
                };
            }
            catch
            {
                return "Unknown";
            }
        }

        static Dictionary<string, string> GetEnvironmentVariables()
        {
            return Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                              .ToDictionary(d => d.Key.ToString(), d => d.Value?.ToString());
        }

        // Retrieve specific hardware information
        static string GetHardwareInfo(string wmiClass, string wmiProperty)
        {
            try
            {
                var searcher = new ManagementObjectSearcher($"SELECT {wmiProperty} FROM {wmiClass}");
                var result = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return result?[wmiProperty]?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        // Print system information
        static void PrintSystemInfo(SystemInfo info)
        {
            Console.WriteLine($"CPU: {info.CPU}");
            Console.WriteLine($"Cores: {info.CPUCores}");
            Console.WriteLine($"CPU Speed: {info.CPUSpeed} MHz");
            Console.WriteLine($"Motherboard: {info.Motherboard}");
            Console.WriteLine($"BIOS Version: {info.BIOSVersion}");
            Console.WriteLine($"RAM: {info.RAMSize}, {info.RAMType}, {info.RAMSpeed}");
            Console.WriteLine("Storage Devices:");
            foreach (var device in info.Storage)
                Console.WriteLine($"  {device.Model} - {device.Type} - {device.Capacity}");
            Console.WriteLine($"GPU: {info.GPU} ({info.GPUVRAM})");
            Console.WriteLine("Network Adapters:");
            foreach (var adapter in info.NetworkAdapters)
                Console.WriteLine($"  MAC: {adapter.MACAddress}, IP: {adapter.IPAddress}");
            Console.WriteLine($"OS: {info.OS} ({info.OSVersion})");
            Console.WriteLine($"Running Processes: {info.RunningProcesses}");
            Console.WriteLine("Environment Variables:");
            foreach (var envVar in info.EnvironmentVariables)
                Console.WriteLine($"  {envVar.Key}: {envVar.Value}");

            // Additional Information
            Console.WriteLine($"Disk ID: {info.DiskID}");
            Console.WriteLine($"UUID: {info.UUID}");
            Console.WriteLine($"Machine ID: {info.MachineID}");
            Console.WriteLine($"Serial Number: {info.SerialNumber}");
        }

        // Compare system information and print differences
        static void CompareSystemInfo(SystemInfo previous, SystemInfo current)
        {
            var diffs = new List<string>();

            if (previous.CPU != current.CPU) diffs.Add($"CPU changed from {previous.CPU} to {current.CPU}");
            if (previous.CPUCores != current.CPUCores) diffs.Add($"CPU Cores changed from {previous.CPUCores} to {current.CPUCores}");
            if (previous.CPUSpeed != current.CPUSpeed) diffs.Add($"CPU Speed changed from {previous.CPUSpeed} MHz to {current.CPUSpeed} MHz");
            if (previous.Motherboard != current.Motherboard) diffs.Add($"Motherboard changed from {previous.Motherboard} to {current.Motherboard}");
            if (previous.BIOSVersion != current.BIOSVersion) diffs.Add($"BIOS Version changed from {previous.BIOSVersion} to {current.BIOSVersion}");
            if (previous.RAMSize != current.RAMSize) diffs.Add($"RAM Size changed from {previous.RAMSize} to {current.RAMSize}");
            if (previous.RAMType != current.RAMType) diffs.Add($"RAM Type changed from {previous.RAMType} to {current.RAMType}");
            if (previous.RAMSpeed != current.RAMSpeed) diffs.Add($"RAM Speed changed from {previous.RAMSpeed} to {current.RAMSpeed}");
            // Storage devices comparison (basic, can be more complex if needed)
            for (int i = 0; i < previous.Storage.Count && i < current.Storage.Count; i++)
            {
                if (previous.Storage[i].Model != current.Storage[i].Model || previous.Storage[i].Capacity != current.Storage[i].Capacity)
                {
                    diffs.Add($"Storage Device {i + 1} changed from {previous.Storage[i].Model} ({previous.Storage[i].Capacity}) to {current.Storage[i].Model} ({current.Storage[i].Capacity})");
                }
            }

            // Network adapters comparison
            for (int i = 0; i < previous.NetworkAdapters.Count && i < current.NetworkAdapters.Count; i++)
            {
                if (previous.NetworkAdapters[i].MACAddress != current.NetworkAdapters[i].MACAddress || previous.NetworkAdapters[i].IPAddress != current.NetworkAdapters[i].IPAddress)
                {
                    diffs.Add($"Network Adapter {i + 1} changed from MAC: {previous.NetworkAdapters[i].MACAddress}, IP: {previous.NetworkAdapters[i].IPAddress} to MAC: {current.NetworkAdapters[i].MACAddress}, IP: {current.NetworkAdapters[i].IPAddress}");
                }
            }

            if (previous.OS != current.OS) diffs.Add($"OS changed from {previous.OS} to {current.OS}");
            if (previous.OSVersion != current.OSVersion) diffs.Add($"OS Version changed from {previous.OSVersion} to {current.OSVersion}");
            if (previous.RunningProcesses != current.RunningProcesses) diffs.Add($"Running Processes count changed from {previous.RunningProcesses} to {current.RunningProcesses}");

            // Environment variables comparison
            foreach (var env in previous.EnvironmentVariables)
            {
                if (!current.EnvironmentVariables.ContainsKey(env.Key))
                {
                    diffs.Add($"Environment variable {env.Key} was removed.");
                }
                else if (current.EnvironmentVariables[env.Key] != env.Value)
                {
                    diffs.Add($"Environment variable {env.Key} changed from {env.Value} to {current.EnvironmentVariables[env.Key]}");
                }
            }

            foreach (var env in current.EnvironmentVariables)
            {
                if (!previous.EnvironmentVariables.ContainsKey(env.Key))
                {
                    diffs.Add($"New environment variable {env.Key} added with value {env.Value}");
                }
            }

            // New comparison for additional fields
            if (previous.DiskID != current.DiskID) diffs.Add($"Disk ID changed from {previous.DiskID} to {current.DiskID}");
            if (previous.UUID != current.UUID) diffs.Add($"UUID changed from {previous.UUID} to {current.UUID}");
            if (previous.MachineID != current.MachineID) diffs.Add($"Machine ID changed from {previous.MachineID} to {current.MachineID}");
            if (previous.SerialNumber != current.SerialNumber) diffs.Add($"Serial Number changed from {previous.SerialNumber} to {current.SerialNumber}");

            if (diffs.Count > 0)
            {
                Console.WriteLine("Differences found:");
                foreach (var diff in diffs)
                {
                    Console.WriteLine(diff);
                }
            }
            else
            {
                Console.WriteLine("No changes detected.");
            }
        }
    }

    public class SystemInfo
    {
        public string CPU { get; set; }
        public string CPUCores { get; set; }
        public string CPUSpeed { get; set; }
        public string Motherboard { get; set; }
        public string BIOSVersion { get; set; }
        public string RAMSize { get; set; }
        public string RAMType { get; set; }
        public string RAMSpeed { get; set; }
        public List<StorageDevice> Storage { get; set; }
        public string GPU { get; set; }
        public string GPUVRAM { get; set; }
        public List<NetworkAdapter> NetworkAdapters { get; set; }
        public string OS { get; set; }
        public string OSVersion { get; set; }
        public int RunningProcesses { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        // Additional fields
        public string DiskID { get; set; }
        public string UUID { get; set; }
        public string MachineID { get; set; }
        public string SerialNumber { get; set; }
    }

    public class StorageDevice
    {
        public string Model { get; set; }
        public string Type { get; set; }
        public string Capacity { get; set; }
    }

    public class NetworkAdapter
    {
        public string MACAddress { get; set; }
        public string IPAddress { get; set; }
    }
} */