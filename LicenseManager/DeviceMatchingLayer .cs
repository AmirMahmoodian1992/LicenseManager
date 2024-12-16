//using LicenseManager;
//using System.ComponentModel.DataAnnotations;
//using System.Linq;

//public class DeviceMatchingLayer : IDeviceMatchingLayer
//{
//    private readonly ILicenseConfigurationProvider _licenseConfigProvider;

//    public DeviceMatchingLayer(ILicenseConfigurationProvider licenseConfigProvider)
//    {
//        _licenseConfigProvider = licenseConfigProvider;
//    }

//    public ValidationResult Match(LicenseRequest request)
//    {
//        // Load configuration data
//        var configData = _licenseConfigProvider.LoadConfiguration();

//        // Match servers
//        var serverLicenses = MatchServers(request, configData.ServerList);

//        // Match devices
//        var deviceLicenses = MatchDevices(request, configData.DeviceList);

//        // Combine results
//        var matchedLicenses = serverLicenses.Concat(deviceLicenses).ToList();

//        if (!matchedLicenses.Any())
//        {
//            return new ValidationResult
//            {
//                IsValid = false,
//                Reason = "No matching servers or devices found",
//                MatchedLicenses = new List<LicenseBase>()
//            };
//        }

//        return new ValidationResult
//        {
//            IsValid = true,
//            MatchedLicenses = matchedLicenses
//        };
//    }

//    private List<ServerLicense> MatchServers(LicenseRequest request, List<ServerConfigurationData> serverConfigs)
//    {
//        return serverConfigs
//            .Where(config => IsDbConnectionValid(config, request.DbConnection))
//            .SelectMany(config => FindMatchingServerLicenses(config, request.MachineName, request.Ip))
//            .ToList();
//    }

//    private IEnumerable<ServerLicense> FindMatchingServerLicenses(
//        ServerConfigurationData config,
//        string deviceName,
//        string deviceIp)
//    {
//        var matchedLicenses = new List<ServerLicense>();

//        // Match by individual server
//        if (IsMatchingServer(config, deviceName, deviceIp))
//        {
//            var license = LicenseFileManager.LoadSingleLicense(config.LicenseId);
//            if (license is ServerLicense serverLicense)
//            {
//                matchedLicenses.Add(serverLicense);
//            }
//        }

//        // Match by load-balanced configuration
//        if (IsMatchingLoadBalancedLicenses(config, deviceName, deviceIp))
//        {
//            var license = LicenseFileManager.LoadSingleLicense(config.LicenseId);
//            if (license is ServerLicense serverLicense)
//            {
//                matchedLicenses.Add(serverLicense);
//            }
//        }

//        return matchedLicenses;
//    }

//    private List<DeviceLicense> MatchDevices(LicenseRequest request, List<DeviceConfigurationData> deviceConfigs)
//    {
//        return deviceConfigs
//            .Where(config => IsDbConnectionLocal(request.DbConnection) || IsDbConnectionExists(config.LicenseId))
//            .SelectMany(config => FindMatchingDeviceLicenses(config, request.MachineName, request.Ip))
//            .ToList();
//    }

//    private IEnumerable<DeviceLicense> FindMatchingDeviceLicenses(
//        DeviceConfigurationData config,
//        string deviceName,
//        string deviceIp)
//    {
//        var matchedLicenses = new List<DeviceLicense>();

//        foreach (var instance in config.InstanceList)
//        {
//            if (instance.DeviceData?.IsMatch(deviceName, deviceIp) == true)
//            {
//                var license = LicenseFileManager.LoadSingleLicense(config.LicenseId);
//                if (license is DeviceLicense deviceLicense)
//                {
//                    matchedLicenses.Add(deviceLicense);
//                }
//            }
//        }

//        return matchedLicenses;
//    }

//    private bool IsMatchingServer(ServerConfigurationData config, string deviceName, string deviceIp)
//    {
//        return
//            config.WinServerDeviceData.IsMatch(deviceName, deviceIp) ||
//            config.WebServerDeviceData.IsMatch(deviceName, deviceIp);
//    }

//    private bool IsMatchingLoadBalancedLicenses(ServerConfigurationData config, string deviceName, string deviceIp)
//    {
//        return config.WebServerLoadBalanceDeviceList.Any(device =>
//            device.DeviceData?.IsMatch(deviceName, deviceIp) == true);
//    }

//    private bool IsDbConnectionValid(ServerConfigurationData config, string dbConnection)
//    {
//        return config.DbConnection == dbConnection;
//    }
//}
