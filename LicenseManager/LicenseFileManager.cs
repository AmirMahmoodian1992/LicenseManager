using Microsoft.AspNetCore.Hosting.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

public static class LicenseFileManager
{
    private static readonly string LicenseFile = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Barsa",
        "LicenseManager",
        "LicensesDb.license");

    private static readonly string ConfigFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Barsa",
        "LicenseManager",
        "LicenseConfigurations.License");
    private static readonly string KeyFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Barsa",
        "LicenseManager",
        "keyFile.json");

    private static byte[] EncryptionKey;
    private static byte[] EncryptionIV;

    private static Lazy<ConcurrentDictionary<string, LicenseBase>> LicenseCache = new Lazy<ConcurrentDictionary<string, LicenseBase>>(() => new ConcurrentDictionary<string, LicenseBase>());
    private static Lazy<ConcurrentDictionary<string, LicenseConfigurationData>> ConfigCache = new Lazy<ConcurrentDictionary<string, LicenseConfigurationData>>(() => new ConcurrentDictionary<string, LicenseConfigurationData>());

    private static Lazy<LicenseRuleData> SystemRulesCache = new Lazy<LicenseRuleData>(LoadSystemRulesFromFile);
    private static Lazy<LicenseConfigurationData> AllConfigurationsCache = new Lazy<LicenseConfigurationData>(LoadAndCacheConfigurations);

    private static readonly byte[] HardcodedKey = Convert.FromBase64String("Ijm3zDVX6VlAbz7dRktrFmI0DU8vRQjYTMFTVlfm2PY=");
    private static readonly byte[] HardcodedIV = Convert.FromBase64String("ykhxrF9ooZbDkG3fdd/9EQ==");

    static LicenseFileManager()
    {
        InitializeFileSystem();

        InitializeEncryptionKeys();

        if (!File.Exists(LicenseFile))
        {
            SaveSystemRules(new LicenseRuleData { CustomerList = new List<Customer>() });
        }

    }
    private static void InitializeFileSystem()
    {
        string directoryPath = Path.GetDirectoryName(LicenseFile);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
    private static void InitializeEncryptionKeys()
    {
        if (HardcodedKey != null && HardcodedIV != null)
        {
            EncryptionKey = HardcodedKey;
            EncryptionIV = HardcodedIV;
        }
        else
        {
            if (!File.Exists(KeyFile))
            {
                var (key, iv) = GenerateAesKeyAndIV();

                //Convert.ToBase64String(key);
                //Convert.ToBase64String(iv);

                SaveKeys(key, iv);
            }
            else
            {
                LoadKeys();
            }
        }
    }

    // ------------------ System Config Handling ------------------
    public static void UpdateDeviceInstance(string licenseId, DeviceInstanceConfigurationData instanceData)
    {
        var systemConfig = LoadAllConfigurations();

        if (instanceData == null)
            return;

        if (systemConfig.DeviceList.Any(d =>
            (d.LicenseId == licenseId &&
                d.InstanceList.Any(i =>
                    i.Index != instanceData.Index && i.DeviceData!= null &&
                    i.DeviceData.IsSameAs(instanceData.DeviceData))) ||
            (d.LicenseId != licenseId &&
                d.InstanceList.Any(i =>
                    i.DeviceData.IsSameAs(instanceData.DeviceData)))))
        {
            throw new InvalidOperationException($"Device with IP {instanceData.DeviceData.DeviceIp} or Name {instanceData.DeviceData.DeviceName} already exists in the system.");
        }


        var deviceConfig = systemConfig.DeviceList.FirstOrDefault(d => d.LicenseId == licenseId);
        if (deviceConfig != null)
        {
            var existingInstance = deviceConfig.InstanceList.FirstOrDefault(i => i.Index == instanceData.Index);
            if (existingInstance != null)
            {
                existingInstance.DeviceData = instanceData.DeviceData;
            }
            else
            {
                deviceConfig.InstanceList.Add(instanceData);
            }

        }
        else
        {
            //deviceConfig = CreateNewDeviceConfiguration(licenseId, instanceData);
            //systemConfig.DeviceList.Add(deviceConfig);
        }
        SaveAllConfigurations(systemConfig);
        return;
    }
    public static void UpdateLoadBalancerInstance(string licenseId, DeviceInstanceConfigurationData instanceData)
    {
        if (instanceData == null)
        {
            throw new ArgumentNullException(nameof(instanceData), "Instance data cannot be null.");
        }

        if (string.IsNullOrEmpty(instanceData.DeviceData?.DeviceIp) && string.IsNullOrEmpty(instanceData.DeviceData?.DeviceName))
        {
            //throw new ArgumentException("Both IP and Name of the device are empty!");
        }

        var systemConfig = LoadAllConfigurations();
        if (systemConfig == null)
        {
            throw new InvalidOperationException("System configuration could not be loaded.");
        }
        if (instanceData.DeviceData == null)
            return;
        // Check for conflicting device data in the system
        if (systemConfig.ServerList != null && systemConfig.ServerList.Any(s =>
            (s.LicenseId == licenseId &&
                s.WebServerLoadBalanceDeviceList != null &&
                s.WebServerLoadBalanceDeviceList.Any(i =>
                    i.Index != instanceData.Index &&
                    i.DeviceData != null &&
                    i.DeviceData.IsSameAs(instanceData.DeviceData)
                )
            )
            ||
            (s.LicenseId != licenseId &&
                s.WebServerLoadBalanceDeviceList != null &&
                s.WebServerLoadBalanceDeviceList.Any(i =>
                    i.DeviceData != null &&
                    i.DeviceData.IsSameAs(instanceData.DeviceData)
                )
            )
        ))
        {
            throw new InvalidOperationException($"A device with IP {instanceData.DeviceData?.DeviceIp} or Name {instanceData.DeviceData?.DeviceName} already exists in the system under conflicting conditions.");
        }

        // Check if WebServerDeviceData conflicts
        if (systemConfig.ServerList != null && systemConfig.ServerList.Any(s =>
            s.WebServerDeviceData != null &&
            instanceData.DeviceData != null &&
            s.WebServerDeviceData.IsSameAs(instanceData.DeviceData)
        ))
        {
            throw new InvalidOperationException($"Web server with IP {instanceData.DeviceData?.DeviceIp} or Name {instanceData.DeviceData?.DeviceName} already exists in the system.");
        }

        // Update or add the instance to the appropriate server configuration
        var serverConfig = systemConfig.ServerList?.FirstOrDefault(s => s.LicenseId == licenseId);
        if (serverConfig != null)
        {
            if (serverConfig.WebServerLoadBalanceDeviceList == null)
            {
                serverConfig.WebServerLoadBalanceDeviceList = new List<DeviceInstanceConfigurationData>();
            }

            var existingInstance = serverConfig.WebServerLoadBalanceDeviceList.FirstOrDefault(i => i.Index == instanceData.Index);
            if (existingInstance != null)
            {
                existingInstance.DeviceData = instanceData.DeviceData;
            }
            else
            {
                serverConfig.WebServerLoadBalanceDeviceList.Add(instanceData);
            }
        }
        else
        {
            // Uncomment and implement this if needed
            // serverConfig = CreateNewLoadBalancerConfiguration(licenseId, instanceData);
            // systemConfig.ServerList.Add(serverConfig);
        }

        // Save the updated configuration
        SaveAllConfigurations(systemConfig);
    }
    //public static void UpdateServerConfiguration(ServerConfigurationData serverConfig, requetType type)
    //{
    //    var systemConfig = LoadAllConfigurations();

    //    //if (type == requetType.WebServer &&
    //    //        string.IsNullOrEmpty(serverConfig.WebServerDeviceIp) && string.IsNullOrEmpty(serverConfig.WebServerDeviceName))
    //    //{
    //    //    throw new Exception("both ip and name are empty!");
    //    //}

    //    //if (type == requetType.WindowsServer &&
    //    //    string.IsNullOrEmpty(serverConfig.WinServerDeviceIp) && string.IsNullOrEmpty(serverConfig.WinServerDeviceName))
    //    //{
    //    //    throw new Exception("both ip and name are empty!");
    //    //}

    //    if (type == requetType.WebServer)
    //    {
    //        if (systemConfig.ServerList.Any(s =>
    //        s.LicenseId != serverConfig.LicenseId &&
    //        (
    //         (!string.IsNullOrEmpty(serverConfig.WebServerDeviceIp) && s.WebServerDeviceIp == serverConfig.WebServerDeviceIp) ||
    //         (!string.IsNullOrEmpty(serverConfig.WebServerDeviceName) && s.WebServerDeviceName == serverConfig.WebServerDeviceName))))
    //        {
    //            throw new InvalidOperationException($"Web server with IP {serverConfig.WebServerDeviceIp} or Name {serverConfig.WebServerDeviceName} already exists.");
    //        }
    //    }
    //    if (type == requetType.WindowsServer)
    //    {
    //        // Check for Windows Server duplicates
    //        if (systemConfig.ServerList.Any(s =>
    //        s.LicenseId != serverConfig.LicenseId &&
    //        (
    //        (!string.IsNullOrEmpty(serverConfig.WinServerDeviceIp) && s.WinServerDeviceIp == serverConfig.WinServerDeviceIp) ||
    //        (!string.IsNullOrEmpty(serverConfig.WinServerDeviceName) && s.WinServerDeviceName == serverConfig.WinServerDeviceName))))
    //        {
    //            throw new InvalidOperationException($"Windows server with IP {serverConfig.WinServerDeviceIp} or Name {serverConfig.WinServerDeviceName} already exists.");
    //        }
    //    }

    //    var existingServer = systemConfig.ServerList.FirstOrDefault(s => s.LicenseId == serverConfig.LicenseId);
    //    if (existingServer != null)
    //    {
    //        existingServer.DbConnection = serverConfig.DbConnection;
    //        existingServer.WebServerDeviceData = serverConfig.WebServerDeviceData;
    //        existingServer.WinServerDeviceData = serverConfig.WinServerDeviceData;
    //        //SaveAllConfigurations(systemConfig);
    //    }
    //    else
    //    {
    //        existingServer = CreateNewServerConfiguration(serverConfig);
    //        systemConfig.ServerList.Add(existingServer);
    //    }

    //    SaveAllConfigurations(systemConfig);
    //}

    //---------------------------------todo----------------------------

    public static void UpdateOrCreateServerConfiguration(ServerConfigurationData serverConfig)
    {
        var systemConfig = LoadAllConfigurations();

        //ValidateServerConfigurationInput(serverConfig);

        CheckForExistingServerConfiguration(systemConfig, serverConfig);

        var existingServer = systemConfig.ServerList.FirstOrDefault(s => s.LicenseId == serverConfig.LicenseId);
        if (existingServer != null)
        {
            UpdateServerConfigurationData(existingServer, serverConfig);
        }
        else
        {
            //if doesnt exists already do noting 
            //var newServerConfig = CreateNewServerConfiguration(serverConfig);
            //systemConfig.ServerList.Add(newServerConfig);
        }

        SaveAllConfigurations(systemConfig);
    }

    //private static void ValidateServerConfigurationInput(ServerConfigurationData serverConfig)
    //{
    //    if (string.IsNullOrEmpty(serverConfig.WebServerDeviceIp) && string.IsNullOrEmpty(serverConfig.WebServerDeviceName) &&
    //        string.IsNullOrEmpty(serverConfig.WinServerDeviceIp) && string.IsNullOrEmpty(serverConfig.WinServerDeviceName))
    //    {
    //        throw new Exception("Both IP and name are empty for the server configuration.");
    //    }
    //}

    private static void CheckForExistingServerConfiguration(LicenseConfigurationData systemConfig, ServerConfigurationData serverConfig)
    {
        // Initialize a flag to track if any conflict is found
        bool conflictFound = false;

        // Check if the serverConfig's WebServerDeviceData is not null
        if (serverConfig.WebServerDeviceData != null)
        {
            // Iterate through the ServerList to check for conflicts
            foreach (var s in systemConfig.ServerList)
            {
                // Ensure that LicenseId is different (check condition)
                if (s.LicenseId != serverConfig.LicenseId)
                {
                    // Check if the other server has WebServerDeviceData and if it matches the current one
                    if (s.WebServerDeviceData != null && serverConfig.WebServerDeviceData.IsSameAs(s.WebServerDeviceData))
                    {
                        conflictFound = true;
                        break; // No need to check further if a conflict is found
                    }

                    // If WebServerDeviceData didn't match, check WinServerDeviceData
                    if (serverConfig.WinServerDeviceData != null && s.WinServerDeviceData != null)
                    {
                        if (serverConfig.WinServerDeviceData.IsSameAs(s.WinServerDeviceData))
                        {
                            conflictFound = true;
                            break; // No need to check further if a conflict is found
                        }
                    }
                }
            }
        }

        // If a conflict was found, throw the exception
        if (conflictFound)
        {
            throw new InvalidOperationException(
                $"A server with one of the provided IPs or names already exists in the system.");
        }


        if (serverConfig.WebServerDeviceData != null)
        {
            foreach (var s in systemConfig.ServerList)
            {
                if (s.LicenseId != serverConfig.LicenseId)
                {
                    foreach (var loadBalancer in s.WebServerLoadBalanceDeviceList)
                    {
                        if (loadBalancer.DeviceData != null &&
                            serverConfig.WebServerDeviceData.IsSameAs(loadBalancer.DeviceData))
                        {
                            throw new InvalidOperationException(
                                $"A load balancer with IP {loadBalancer.DeviceData.DeviceIp} or Name {loadBalancer.DeviceData.DeviceName} already exists in the system, conflicting with the Web Server configuration.");
                        }
                    }
                }
            }
        }
    }

        // Method to update server configuration data if the server already exists
        private static void UpdateServerConfigurationData(ServerConfigurationData existingServer, ServerConfigurationData newServerConfig)
    {
        existingServer.DbConnection = newServerConfig.DbConnection;
        existingServer.WebServerDeviceData = newServerConfig.WebServerDeviceData;
        existingServer.WinServerDeviceData = newServerConfig.WinServerDeviceData;
    }
    //public static void UpdateFullConfiguration(LicenseTreeData licenseTreeData)
    //{
    //    var systemConfig = LoadAllConfigurations();

    //    // Process Server Licenses
    //    foreach (var licenseTreeServer in licenseTreeData.ServerLicenses)
    //    {
    //        var newServerConfig = new ServerConfigurationData
    //        {
    //            LicenseId = licenseTreeServer.Id,
    //            DbConnection = licenseTreeServer.DbConnection,
    //            WebServerDeviceName = licenseTreeServer.WebServerDeviceName,
    //            WebServerDeviceIp = licenseTreeServer.WebServerDeviceIp,
    //            WinServerDeviceName = licenseTreeServer.WinServerDeviceName,
    //            WinServerDeviceIp = licenseTreeServer.WinServerDeviceIp,
    //            WebServerLoadBalanceDeviceList = licenseTreeServer.LoadBalance?.ConvertAll(lb => new DeviceInstanceConfigurationData
    //            {
    //                Index = lb.Index,
    //                DeviceName = lb.Name,
    //                DeviceIp = lb.IP
    //            })
    //        };

    //        // Check and update configuration based on server type
    //        if (licenseTreeServer.WebServerDeviceData!=null || licenseTreeServer.WinServerDeviceData!=null)
    //        {
    //            var existingServer = systemConfig.ServerList.FirstOrDefault(s => s.LicenseId == newServerConfig.LicenseId);
    //            if (existingServer != null)
    //            {
    //                // Update existing server configuration
    //                existingServer.DbConnection = newServerConfig.DbConnection;
    //                existingServer.WebServerDeviceData = newServerConfig.WebServerDeviceData;
    //                existingServer.WinServerDeviceData= newServerConfig.WinServerDeviceData;

    //                // Update or add load balancers
    //                foreach (var newLbDevice in newServerConfig.WebServerLoadBalanceDeviceList)
    //                {
    //                    var existingLbDevice = existingServer.WebServerLoadBalanceDeviceList
    //                        .FirstOrDefault(lb => lb.Index == newLbDevice.Index);

    //                    if (existingLbDevice != null)
    //                    {
    //                        existingLbDevice.DeviceData = newLbDevice.DeviceData;
    //                    }
    //                    else
    //                    {
    //                        existingServer.WebServerLoadBalanceDeviceList.Add(newLbDevice);
    //                    }
    //                }
    //            }
    //            else
    //            {
    //                // Add new server configuration
    //                systemConfig.ServerList.Add(newServerConfig);
    //            }
    //        }
    //    }

    //    foreach (var licenseTreeDevice in licenseTreeData.DeviceLicenses)
    //    {
    //        var existingDevice = systemConfig.DeviceList.FirstOrDefault(d => d.LicenseId == licenseTreeDevice.Id);
    //        if (existingDevice != null)
    //        {
    //            foreach (var newInstance in licenseTreeDevice.Devices)
    //            {
    //                var existingInstance = existingDevice.InstanceList.FirstOrDefault(i => i.Index == newInstance.Index);

    //                if (existingInstance != null)
    //                {
    //                    existingInstance.DeviceData= newInstance.DeviceData;
    //                }
    //                else
    //                {
    //                    existingDevice.InstanceList.Add(new DeviceInstanceConfigurationData
    //                    {
    //                        Index = newInstance.Index,
    //                        DeviceData= newInstance.DeviceData,
    //                    });
    //                }
    //            }
    //        }
    //        else
    //        {
    //            systemConfig.DeviceList.Add(new DeviceConfigurationData
    //            {
    //                LicenseId = licenseTreeDevice.Id,
    //                InstanceList = licenseTreeDevice.Devices.ConvertAll(d => new DeviceInstanceConfigurationData
    //                {
    //                    Index = d.Index,
    //                    DeviceName = d.Name,
    //                    DeviceIp = d.IP
    //                })
    //            });
    //        }
    //    }

    //    SaveAllConfigurations(systemConfig);
    //}

    public static void SaveAllConfigurations(LicenseConfigurationData configuration)
    {
        var jsonString = JsonConvert.SerializeObject(configuration, Formatting.Indented);
        File.WriteAllText(ConfigFile, Encrypt(jsonString));

        AllConfigurationsCache = new Lazy<LicenseConfigurationData>(() => configuration);
    }
    public static bool IsConfigConsistentWithRules(LicenseConfigurationData configData, LicenseRuleData ruleData)
    {
        var ruleServerLicenseIds = ruleData.CustomerList
            .SelectMany(customer => customer.LicenseScopeList)
            .SelectMany(scope => scope.ServerLicenseList ?? new List<ServerLicense>())
            .Select(serverLicense => serverLicense.Id)
            .ToHashSet();

        var ruleDeviceLicenseIds = ruleData.CustomerList
            .SelectMany(customer => customer.LicenseScopeList)
            .SelectMany(scope => scope.DeviceLicenseList ?? new List<DeviceLicense>())
            .Select(deviceLicense => deviceLicense.Id)
            .ToHashSet();

        var configServerLicenseIds = configData.ServerList
            .Select(serverConfig => serverConfig.LicenseId)
            .ToHashSet();

        var configDeviceLicenseIds = configData.DeviceList
            .Select(deviceConfig => deviceConfig.LicenseId)
            .ToHashSet();

        bool serverIdsMatch = ruleServerLicenseIds.SetEquals(configServerLicenseIds);
        bool deviceIdsMatch = ruleDeviceLicenseIds.SetEquals(configDeviceLicenseIds);

        return serverIdsMatch && deviceIdsMatch;
    }

    public static LicenseConfigurationData LoadAllConfigurations()
    {
        return AllConfigurationsCache.Value;
    }


    private static LicenseConfigurationData LoadAndCacheConfigurations()
    {
        if (!File.Exists(ConfigFile) || string.IsNullOrEmpty(File.ReadAllText(ConfigFile)))
        {
            return GenerateAndSaveDefaultConfiguration();
        }

        var jsonString = File.ReadAllText(ConfigFile);
        var decryptedContent = Decrypt(jsonString);

        var AllConfigurations = JsonConvert.DeserializeObject<LicenseConfigurationData>(decryptedContent);

        //UpdateConfigurationBasedOnLicenseRules(LoadSystemRules(), AllConfigurations);

        AllConfigurationsCache = new Lazy<LicenseConfigurationData>(() => AllConfigurations);
        return AllConfigurationsCache.Value;
    }
    private static LicenseConfigurationData GenerateAndSaveDefaultConfiguration()
    {
        var systemConfig = GenerateDefaultConfiguration(LoadSystemRules());
        SaveAllConfigurations(systemConfig);
        return systemConfig;
    }

    public static void ClearAllConfigurations()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                File.WriteAllText(ConfigFile, string.Empty);
            }
            else
            {
                throw new FileNotFoundException("Configuration file not found.");
            }
            AllConfigurationsCache = new Lazy<LicenseConfigurationData>(LoadAndCacheConfigurations);

        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Access to the path is denied. Please check permissions or run as an administrator.");
        }
    }


    // ------------------ System Rules Handling ------------------

    public static void SaveSystemRules(LicenseRuleData systemRules)
    {
        foreach (var customer in systemRules.CustomerList)
        {
            AssignDefaultLicenseDataToLicenses(customer);
        }

        var jsonString = JsonConvert.SerializeObject(systemRules, Formatting.Indented);
        var encryptedContent = Encrypt(jsonString);

        File.WriteAllText(LicenseFile, encryptedContent);

        SystemRulesCache = new Lazy<LicenseRuleData>(() => systemRules);
    }

    public static LicenseRuleData LoadSystemRules()
    {
        return SystemRulesCache.Value;
    }

    private static LicenseRuleData LoadSystemRulesFromFile()
    {
        if (!File.Exists(LicenseFile))
        {
            return new LicenseRuleData { CustomerList = new List<Customer>() };
        }

        var encryptedContent = File.ReadAllText(LicenseFile);

        // Check if the file content is empty or whitespace
        if (string.IsNullOrWhiteSpace(encryptedContent))
        {
            return new LicenseRuleData { CustomerList = new List<Customer>() };
        }

        var decryptedContent = Decrypt(encryptedContent);
        return JsonConvert.DeserializeObject<LicenseRuleData>(decryptedContent);
    }


    // ------------------ License Handling ------------------
    public static void SaveSingleLicense(LicenseBase license)
    {
        var systemLicenseRules = LoadSystemRules();
        var customer = systemLicenseRules.CustomerList
            .FirstOrDefault(c => c.LicenseScopeList
                .Any(ls => ls.ServerLicenseList.Any(l => l.Id == license.Id) || ls.DeviceLicenseList.Any(l => l.Id == license.Id)));

        if (customer != null)
        {
            var scope = customer.LicenseScopeList
                .FirstOrDefault(ls => ls.ServerLicenseList.Any(l => l.Id == license.Id) || ls.DeviceLicenseList.Any(l => l.Id == license.Id));

            if (scope != null)
            {
                if (license is ServerLicense serverLicense)
                {
                    var existingLicense = scope.ServerLicenseList.FirstOrDefault(l => l.Id == serverLicense.Id);
                    if (existingLicense != null)
                    {
                        scope.ServerLicenseList.Remove(existingLicense);
                    }
                    scope.ServerLicenseList.Add(serverLicense);
                }
                else if (license is DeviceLicense deviceLicense)
                {
                    var existingLicense = scope.DeviceLicenseList.FirstOrDefault(l => l.Id == deviceLicense.Id);
                    if (existingLicense != null)
                    {
                        scope.DeviceLicenseList.Remove(existingLicense);
                    }
                    scope.DeviceLicenseList.Add(deviceLicense);
                }
            }
        }
        else
        {
            throw new Exception("license scope is null");
            //var newCustomer = new Customer
            //{
            //    LicenseScopeList = new List<LicenseScope>
            //{
            //    new LicenseScope
            //    {
            //        Id = license.Id,
            //        ServerLicenseList = new List<ServerLicense>(),
            //        DeviceLicenseList = new List<DeviceLicense>()
            //    }
            //}
            //};

            //var newScope = newCustomer.LicenseScopeList.First();

            //if (license is ServerLicense newServerLicense)
            //{
            //    newScope.ServerLicenseList.Add(newServerLicense);
            //}
            //else if (license is DeviceLicense newDeviceLicense)
            //{
            //    newScope.DeviceLicenseList.Add(newDeviceLicense);
            //}

            //systemLicenseRules.CustomerList.Add(newCustomer);
        }

        LicenseCache.Value[license.Id] = license;

        SaveSystemRules(systemLicenseRules);
    }

    public static LicenseBase LoadSingleLicense(string licenseId)
    {
        if (LicenseCache.Value.TryGetValue(licenseId, out var cachedLicense))
        {
            return cachedLicense;
        }

        var systemConfig = LoadSystemRules();
        var customer = systemConfig.CustomerList
            .FirstOrDefault(c => c.LicenseScopeList
                .SelectMany(ls => ls.ServerLicenseList.Concat<LicenseBase>(ls.DeviceLicenseList))
                .Any(l => l.Id == licenseId));

        if (customer != null)
        {
            var scope = customer.LicenseScopeList
                .FirstOrDefault(ls => ls.ServerLicenseList.Concat<LicenseBase>(ls.DeviceLicenseList)
                    .Any(l => l.Id == licenseId));

            if (scope != null)
            {
                var license = scope.ServerLicenseList
                    .Concat<LicenseBase>(scope.DeviceLicenseList)
                    .FirstOrDefault(l => l.Id == licenseId);

                if (license != null)
                {
                    LicenseCache.Value[licenseId] = license;

                    return license;
                }
            }
        }

        throw new FileNotFoundException("License not found");
    }

    public static void DeleteSingleLicense(Guid licenseId)
    {
        var systemConfig = LoadSystemRules();
        var customer = systemConfig.CustomerList
            .FirstOrDefault(c =>
                c.LicenseScopeList.Any(ls =>
                    ls.ServerLicenseList.Any(sl => sl.Id == licenseId.ToString()) ||
                    ls.DeviceLicenseList.Any(dl => dl.Id == licenseId.ToString())));

        if (customer != null)
        {
            var scope = customer.LicenseScopeList
                .FirstOrDefault(ls =>
                    ls.ServerLicenseList.Any(sl => sl.Id == licenseId.ToString()) ||
                    ls.DeviceLicenseList.Any(dl => dl.Id == licenseId.ToString()));

            if (scope != null)
            {
                var serverLicense = scope.ServerLicenseList.FirstOrDefault(sl => sl.Id == licenseId.ToString());
                if (serverLicense != null) scope.ServerLicenseList.Remove(serverLicense);
                else
                {
                    var deviceLicense = scope.DeviceLicenseList.FirstOrDefault(dl => dl.Id == licenseId.ToString());
                    if (deviceLicense != null) scope.DeviceLicenseList.Remove(deviceLicense);
                }

                // Remove from cache (thread-safe)
                LicenseCache.Value.TryRemove(licenseId.ToString(), out _);

                // Save updated system rules to file
                SaveSystemRules(systemConfig);
                return;
            }
        }

        throw new FileNotFoundException("License not found");
    }

    // ------------------ Encryption Utilities ------------------

    public static (byte[] Key, byte[] IV) GenerateAesKeyAndIV()
    {
        using (Aes aes = Aes.Create())
        {
            aes.GenerateKey();
            aes.GenerateIV();
            return (aes.Key, aes.IV);
        }
    }

    public static void SaveKeys(byte[] key, byte[] iv)
    {
        var keyData = new EncryptionData { Key = Convert.ToBase64String(key), IV = Convert.ToBase64String(iv) };
        File.WriteAllText(KeyFile, JsonConvert.SerializeObject(keyData, Formatting.Indented));
        EncryptionKey = key;
        EncryptionIV = iv;
    }

    public static void LoadKeys()
    {
        var keyData = JsonConvert.DeserializeObject<EncryptionData>(File.ReadAllText(KeyFile));
        EncryptionKey = Convert.FromBase64String(keyData.Key);
        EncryptionIV = Convert.FromBase64String(keyData.IV);
    }

    public static string Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = EncryptionKey;
            aes.IV = EncryptionIV;
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (StreamWriter writer = new StreamWriter(cs))
            {
                writer.Write(plainText);
                writer.Flush(); // Ensure all data is written
                cs.FlushFinalBlock(); // Finalize the encryption process

                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    public static string Decrypt(string cipherText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = EncryptionKey;
            aes.IV = EncryptionIV;
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
            using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (StreamReader reader = new StreamReader(cs)) { return reader.ReadToEnd(); }
        }
    }

    //------------------------Helpers-----------------------------
    private static void AssignDefaultLicenseDataToServerLicenses(Customer customer, LicenseScope licenseScope)
    {
        if (licenseScope.ServerLicenseList != null)
        {
            foreach (var serverLicense in licenseScope.ServerLicenseList)
            {
                serverLicense.LicenseData ??= customer.DefaultLicenseData;
            }
        }
    }
    private static void AssignDefaultLicenseDataToDeviceLicenses(Customer customer, LicenseScope licenseScope)
    {
        if (licenseScope.DeviceLicenseList != null)
        {
            foreach (var deviceLicense in licenseScope.DeviceLicenseList)
            {
                deviceLicense.LicenseData ??= customer.DefaultLicenseData;
            }
        }
    }
    private static void AssignDefaultLicenseDataToLicenses(Customer customer)
    {
        foreach (var licenseScope in customer.LicenseScopeList)
        {
            AssignDefaultLicenseDataToServerLicenses(customer, licenseScope);
            AssignDefaultLicenseDataToDeviceLicenses(customer, licenseScope);
        }
    }
    public static ServerConfigurationData CreateNewLoadBalancerConfiguration(string licenseId, DeviceInstanceConfigurationData instanceData = default)
    {
        var serverConfig = new ServerConfigurationData
        {
            LicenseId = licenseId,
            WebServerLoadBalanceDeviceList = new List<DeviceInstanceConfigurationData>()
        };
        if (instanceData is null)
            return serverConfig;

        for (int i = 0; i <= instanceData.Index; i++)
        {
            if (i == instanceData.Index)
            {
                serverConfig.WebServerLoadBalanceDeviceList.Add(instanceData);
            }
            else
            {
                serverConfig.WebServerLoadBalanceDeviceList.Add(new DeviceInstanceConfigurationData
                {
                    Index = i,
                    DeviceData = new DeviceData()
                });
            }
        }

        return serverConfig;
    }
    public static DeviceConfigurationData CreateNewDeviceConfiguration(string licenseId, DeviceInstanceConfigurationData instanceData = default)
    {
        var deviceConfig = new DeviceConfigurationData
        {
            LicenseId = licenseId,
            InstanceList = new List<DeviceInstanceConfigurationData>()
        };
        if (instanceData is null)
            return deviceConfig;

        for (int i = 0; i <= instanceData.Index; i++)
        {
            if (i == instanceData.Index)
            {
                deviceConfig.InstanceList.Add(instanceData);
            }
            else
            {
                deviceConfig.InstanceList.Add(new DeviceInstanceConfigurationData
                {
                    Index = i,
                    DeviceData = new DeviceData()

                });
            }
        }

        return deviceConfig;
    }
    public static ServerConfigurationData CreateNewServerConfiguration(ServerConfigurationData serverConfig)
    {
        return new ServerConfigurationData
        {
            LicenseId = serverConfig.LicenseId,
            DbConnection = serverConfig.DbConnection,
            WebServerDeviceData = serverConfig.WebServerDeviceData,
            WinServerDeviceData = serverConfig.WinServerDeviceData,
            WebServerLoadBalanceDeviceList = new List<DeviceInstanceConfigurationData>()
        };
    }

    public static LicenseConfigurationData PopulateConfigurationWithExisting(
    LicenseConfigurationData defaultConfigData,
    LicenseConfigurationData existingConfigData,
    bool removeUnusedConfigs = true)
    {
        var validServerLicenseIds = new HashSet<string>(defaultConfigData.ServerList.Select(s => s.LicenseId));
        var validDeviceLicenseIds = new HashSet<string>(defaultConfigData.DeviceList.Select(d => d.LicenseId));

        foreach (var serverConfig in defaultConfigData.ServerList)
        {
            var existingServerConfig = existingConfigData.ServerList
                .FirstOrDefault(s => s.LicenseId == serverConfig.LicenseId);

            if (existingServerConfig != null)
            {
                if (!string.IsNullOrEmpty(existingServerConfig.DbConnection))
                {
                    serverConfig.DbConnection = existingServerConfig.DbConnection;
                }

                if (serverConfig.WebServerDeviceData != null && existingServerConfig.WebServerDeviceData != null)
                {
                    serverConfig.WebServerDeviceData = existingServerConfig.WebServerDeviceData;
                }

                if (serverConfig.WinServerDeviceData != null && existingServerConfig.WinServerDeviceData != null)
                {
                    serverConfig.WinServerDeviceData = existingServerConfig.WinServerDeviceData;
                }

                foreach (var defaultDeviceInstance in serverConfig.WebServerLoadBalanceDeviceList)
                {
                    var matchingInstance = existingServerConfig.WebServerLoadBalanceDeviceList
                        .FirstOrDefault(e => e.Index == defaultDeviceInstance.Index);

                    if (matchingInstance?.DeviceData != null)
                    {
                        defaultDeviceInstance.DeviceData = matchingInstance.DeviceData;
                    }
                }
            }
        }

        foreach (var deviceConfig in defaultConfigData.DeviceList)
        {
            var existingDeviceConfig = existingConfigData.DeviceList
                .FirstOrDefault(d => d.LicenseId == deviceConfig.LicenseId);

            if (existingDeviceConfig != null)
            {
                foreach (var defaultInstance in deviceConfig.InstanceList)
                {
                    var matchingInstance = existingDeviceConfig.InstanceList
                        .FirstOrDefault(e => e.Index == defaultInstance.Index);

                    if (matchingInstance?.DeviceData != null)
                    {
                        defaultInstance.DeviceData = matchingInstance.DeviceData;
                    }
                }
            }
        }

        // Handle unused configurations
        if (!removeUnusedConfigs)
        {
            // Add unused server configurations
            defaultConfigData.ServerList.AddRange(existingConfigData.ServerList
                .Where(s => !validServerLicenseIds.Contains(s.LicenseId)));

            // Add unused device configurations
            defaultConfigData.DeviceList.AddRange(existingConfigData.DeviceList
                .Where(d => !validDeviceLicenseIds.Contains(d.LicenseId)));
        }

        return defaultConfigData;
    }


    public static LicenseConfigurationData UpdateConfigurationBasedOnLicenseRules(
        LicenseRuleData licenseRuleData,
        LicenseConfigurationData existingConfigData)
    {

        var defaultConfig = GenerateDefaultConfiguration(licenseRuleData);
        var updatedConfigData = PopulateConfigurationWithExisting(defaultConfig, existingConfigData);
        SaveAllConfigurations(updatedConfigData);
        return updatedConfigData;
    }
    public static LicenseConfigurationData GenerateDefaultConfiguration(LicenseRuleData licenseRuleData)
    {
        var defaultConfigData = new LicenseConfigurationData
        {
            ServerList = new List<ServerConfigurationData>(),
            DeviceList = new List<DeviceConfigurationData>()
        };

        foreach (var customer in licenseRuleData.CustomerList)
        {
            foreach (var licenseScope in customer.LicenseScopeList)
            {
                if (licenseScope.ServerLicenseList != null)
                {
                    foreach (var serverLicense in licenseScope.ServerLicenseList)
                    {
                        var serverConfig = new ServerConfigurationData
                        {
                            LicenseId = serverLicense.Id,
                            DbConnection = string.Empty,
                            WebServerDeviceData = serverLicense.LicenseData.HasWebsite ? new DeviceData() : null,
                            WinServerDeviceData = serverLicense.LicenseData.HasWindowsHost ? new DeviceData() : null,
                            WebServerLoadBalanceDeviceList = new List<DeviceInstanceConfigurationData>()
                        };

                        if (serverLicense.HasLoadBalancing)
                        {
                            for (int i = 0; i < serverLicense.LoadBalancerServerCount; i++)
                            {
                                serverConfig.WebServerLoadBalanceDeviceList.Add(new DeviceInstanceConfigurationData
                                {
                                    Index = i,
                                    DeviceData = new DeviceData()
                                });
                            }
                        }

                        defaultConfigData.ServerList.Add(serverConfig);
                    }
                }

                // Process Device Licenses
                if (licenseScope.DeviceLicenseList != null)
                {
                    foreach (var deviceLicense in licenseScope.DeviceLicenseList)
                    {
                        var deviceConfig = new DeviceConfigurationData
                        {
                            LicenseId = deviceLicense.Id,
                            InstanceList = new List<DeviceInstanceConfigurationData>()
                        };

                        for (int i = 0; i < deviceLicense.DeviceCount; i++)
                        {
                            deviceConfig.InstanceList.Add(new DeviceInstanceConfigurationData
                            {
                                Index = i,
                                DeviceData = new DeviceData()
                            });
                        }

                        defaultConfigData.DeviceList.Add(deviceConfig);
                    }
                }
            }
        }

        return defaultConfigData;
    }
    public static LicenseConfigurationData GenerateLicenseConfiguration(LicenseRuleData licenseRuleData)
    {

        var licenseConfigData = new LicenseConfigurationData
        {
            ServerList = new List<ServerConfigurationData>(),
            DeviceList = new List<DeviceConfigurationData>()
        };

        foreach (var customer in licenseRuleData.CustomerList)
        {
            foreach (var licenseScope in customer.LicenseScopeList)
            {
                foreach (var serverLicense in licenseScope.ServerLicenseList)
                {
                    var serverConfig = new ServerConfigurationData
                    {
                        LicenseId = serverLicense.Id ?? string.Empty,
                        DbConnection = string.Empty,
                        WebServerDeviceData = null,
                        WinServerDeviceData = null,
                        WebServerLoadBalanceDeviceList = new List<DeviceInstanceConfigurationData>()
                    };

                    if (serverLicense.HasLoadBalancing)
                    {
                        for (int i = 0; i < serverLicense.LoadBalancerServerCount; i++)
                        {
                            serverConfig.WebServerLoadBalanceDeviceList.Add(new DeviceInstanceConfigurationData
                            {
                                Index = i,
                                DeviceData = new DeviceData()
                            });
                        }
                    }

                    licenseConfigData.ServerList.Add(serverConfig);
                }

                foreach (var deviceLicense in licenseScope.DeviceLicenseList)
                {
                    var deviceConfig = new DeviceConfigurationData
                    {
                        LicenseId = deviceLicense.Id,
                        InstanceList = new List<DeviceInstanceConfigurationData>()
                    };

                    for (int i = 0; i < deviceLicense.DeviceCount; i++)
                    {
                        deviceConfig.InstanceList.Add(new DeviceInstanceConfigurationData
                        {
                            Index = i,
                            DeviceData = new DeviceData(),
                        });
                    }

                    licenseConfigData.DeviceList.Add(deviceConfig);
                }
            }
        }

        ConfigCache.Value["ALL"] = licenseConfigData;


        SaveAllConfigurations(licenseConfigData);
        return licenseConfigData;
    }


}



public class EncryptionData
{
    public string EncryptedText { get; set; }
    public string Key { get; set; }
    public string IV { get; set; }
}

