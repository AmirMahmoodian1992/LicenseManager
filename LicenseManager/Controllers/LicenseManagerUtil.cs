using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JsonException = Newtonsoft.Json.JsonException;

namespace LicenseManager.Controllers
{

    public static class LicenseManagerUtil
    {
        private static HashSet<string> usedNonces = new HashSet<string>();
        private static readonly object nonceLock = new object();

        public static readonly string LsPrivateKey = @"
-----BEGIN PRIVATE KEY-----
MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDIBtfVChTpadB7hOhtRpysQdhZ
QE+9v/B5pz/CRwU3x97S8436EtxlOIORGlnRslEo0zttTHoiVZahRZ41tK5birU2m8rrGSd8uuhI
BoMOEamByBDTUsodwbOzOJ5Y4Xu0kbvftiSRM6dAuYwJEs0UHVsCPARDLFZreel4uleIo66RWjMd
TWOOV5PKCWdPlbWxpDMfpPrvDYk8DEjysQbox1bdMN9NBWwU6AcAf3aDiZzpmZG1OiKiXBZC1Zx4
LcMlSAhQqklyhOKs5cv49XXv9loJFg4x56FN72AHpQaxnTHMA/nTcZz0CWcc24OCRTxmCs6aJdX1
MaskH4sBUmAlAgMBAAECggEAfIX+EWtAT3sHRg4coALIgFhRdmaZ/deivilHgQuzjOFJr2veJCNf
v0fqaAfOiMQI0HCH22gz1HIR3v43GtoLfYOhgoiET82ODpFRD522Mqj+LIQ/LT+qAdJXq6gAs/ZT
i7r6CbAbnaVZZurb6b4hm3cW9BIm2Sad+jSgCv9+hNm/em1/TiZe0zAu+sQNj9S9+f096ecgKH+J
rHJezzJO1WTzCQj1kcVvFVJcSTxCB5/4OUOTfIfvQRL1EHOevZfeuA8XZ84Bv8U2ycXjIgJ5AE0t
MVAr1+SI4sXprnT36FiFoygVfb/PBE7YYHQOZUEO/VZ82LnB+bb9b7EZ/chcVQKBgQDcxr/UXdc4
FA4yNa8v15ThfM4Ab4fwBs0xIDeYaTaRrgsRTPiXQHXyu9NiwpsAIaSn5DVHC01fHE8JKM38Rkfx
ZxHat3GI02u4i33mBxKBjH+quiISqP+KbdQU44u2fh5ugStOVdSan/i4Wj4JEOXoRy27/zTh59vl
mrWnjxG7ZwKBgQDn8JvaRFEssQOjIZhKNKSPq4B0kJIKI0dtXHDVPxw8xZnz8VBkJHXrELMZHb5x
TPfmudVhqOSm56O2FQMU6jSz0dCbcyAolniKz4LPk4sNhR0PiJiaiMwsvZ21pROoU6tAFburrHbf
h0LRtukB4sPnVZAmgdODtSHfeNHuPueckwKBgFL9FsDCYZM/3rI7AdZPCDTWkC0Tv5qXQ+xyetN8
wTlznc9sCon4fh23Qtd95XvgKZ++pdRFyFi3MHJ19zAcTxDMOKbpi/Dt4DEnpjSgdfFimd7YRtSS
2DP14uHTQwJfWBsbzOaOVvOkrH7H1UfdJT7x+pwPa5Z7bGpbhGsu2XCDAoGBAL/kTD1n2jT8esLj
j4iNRzzqKkvGNF0Y1QaAvYVgQm9xGK8/jkBb/gct+M8l0aMRh+QnCYhT4GDKMqL5fWQ3ZMUFKO8D
b67E1xQZHOO58QiNgf6RB5iK50l401suKTZRitRpZqxg8rR6xuiUyPBbebMSEkIZlTJZkJ2/6weO
mbbrAoGBAL2f64opnWLCapkWJuma7jdzQYXuln5iDc8K2OZR9A4PD1owe8Y3dsm+FlXujepbc92K
Xef4BkWaC6M25O8j/lSMDIoHA6ZZrM4RKyfEjqkCrn7Z53+T9s9/A5UchWAaOFwyL/C/58SdmWrv
YvVpNf0ngpFP1RbSXh26WsFuQn5n
-----END PRIVATE KEY-----";
        public static readonly string BarsaPublicKey = @"
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAs/fXlhYVH+bwfT6xj480s9drrwogWuLj
VRj7kGTjN5RGSeUgEs4m5Whx9a/PKSptO9tKM+F/1dmoY9JCSceSb2scQOOPbuXtNjHPd0sTBmMs
er85XXaTJ56ViRzPvmGfH4ZDBS51QFHyC1GvDcs80RkzkZgbkxYkGRJpc3Lb71qrhOtOYUqb/7zy
sqMsxaSWUmj5fCZYct1HPIxKeFvndoFvjZhugftb/7bVUzcLGHGyz5/tzz6mds0sbYUH9u04Bbl1
vPpQ+2pqCTfhWhYYRorGgd5O+oFdAy8XDySr27KrCMpOJ1yEJiWCwoECHDw6s0Ir+EuDmva79wZX
WMINLQIDAQAB
-----END PUBLIC KEY-----";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
        public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(10);

        public static (List<ServerLicense> serverLicenses, List<DeviceLicense> deviceLicenses, List<ServerType> serverTypes) LoadPotentialLicenses(
    string name,
    string ip,
    LicenseConfigurationData configData)
        {
            var matchedServers = FindMatchingServerLicenses(name, ip, configData.ServerList).ToList();

            var serverLicenses = matchedServers.Select(m => m.License).ToList();
            var serverTypes = matchedServers.Select(m => m.ServerType).ToList();

            var deviceLicenses = FindMatchingDeviceLicenses(name, ip, configData.DeviceList).ToList();

            return (serverLicenses, deviceLicenses, serverTypes);
        }


        private static IEnumerable<(ServerLicense License, ServerType ServerType)> FindMatchingServerLicenses(
     string name,
     string ip,
     List<ServerConfigurationData> serverConfigs)
        {
            var matchedServerLicenses = new List<(ServerLicense License, ServerType ServerType)>();

            foreach (var serverConfig in serverConfigs)
            {
                ServerType? serverType = null;

                if (serverConfig.WebServerDeviceData?.IsMatch(name, ip) == true)
                {
                    serverType = ServerType.WebServer;
                }
                else if (serverConfig.WinServerDeviceData?.IsMatch(name, ip) == true)
                {
                    serverType = ServerType.WindowsServer;
                }
                else if (IsMatchingLoadBalancedDevices(serverConfig.WebServerLoadBalanceDeviceList, name, ip))
                {
                    serverType = ServerType.WebServer;
                }

                if (serverType.HasValue)
                {
                    var license = LicenseFileManager.LoadSingleLicense(serverConfig.LicenseId);
                    if (license is ServerLicense serverLicense)
                    {
                        matchedServerLicenses.Add((serverLicense, serverType.Value));
                    }
                }
            }

            return matchedServerLicenses;
        }



        private static bool IsMatchingLoadBalancedDevices(
            List<DeviceInstanceConfigurationData>? loadBalancedDevices,
            string deviceName,
            string deviceIp)
        {
            if (loadBalancedDevices == null) return false;

            return loadBalancedDevices.Any(device =>
                device.DeviceData?.IsMatch(deviceName, deviceIp) == true);
        }

        private static IEnumerable<DeviceLicense> FindMatchingDeviceLicenses(
            string deviceName,
            string deviceIp,
            List<DeviceConfigurationData> deviceConfigs)
        {
            var deviceLicenses = new List<DeviceLicense>();

            foreach (var deviceConfig in deviceConfigs)
            {
                foreach (var instance in deviceConfig.InstanceList)
                {
                    if (instance.DeviceData?.IsMatch(deviceName, deviceIp) == true)
                    {
                        var license = LicenseFileManager.LoadSingleLicense(deviceConfig.LicenseId);
                        if (license is DeviceLicense deviceLicense)
                        {
                            deviceLicenses.Add(deviceLicense);
                        }
                    }
                }
            }

            return deviceLicenses;
        }

        public static (List<ServerLicense> validServerLicenses, List<DeviceLicense> validDeviceLicenses) ValidateDbConnectionsWithReasons(
            List<ServerLicense> serverLicenses,
            List<DeviceLicense> deviceLicenses,
            string dbConnection,
            LicenseConfigurationData configData,
            List<InvalidLicenseDetail> invalidLicenses)
            {
                var validServerLicenses = new List<ServerLicense>();
                var validDeviceLicenses = new List<DeviceLicense>();

                foreach (var serverLicense in serverLicenses)
                {
                    var serverConfig = configData.ServerList.FirstOrDefault(sc => sc.LicenseId == serverLicense.Id);
                    if (serverConfig != null && IsDbConnectionValid(serverConfig, dbConnection))
                    {
                        validServerLicenses.Add(serverLicense);
                    }
                    else
                    {
                        invalidLicenses.Add(new InvalidLicenseDetail
                        {
                            LicenseId = serverLicense.Id,
                            Reason = "Invalid database connection.",
                            LicenseType = "Server"
                        });
                    }
                }

                foreach (var deviceLicense in deviceLicenses)
                {
                    if (IsDbConnectionLocal(dbConnection) || IsDbConnectionExists(dbConnection))
                    {
                        validDeviceLicenses.Add(deviceLicense);
                    }
                    else
                    {
                        invalidLicenses.Add(new InvalidLicenseDetail
                        {
                            LicenseId = deviceLicense.Id,
                            Reason = "Invalid database connection.",
                            LicenseType = "Device"
                        });
                    }
                }

                return (validServerLicenses, validDeviceLicenses);
            }

        public static (List<ServerLicense> validServerLicenses, List<DeviceLicense> validDeviceLicenses) ValidateAndFilterLicensesByTypeWithReasons(
    LicenseRequest request,
    List<ServerLicense> serverLicenses,
    List<DeviceLicense> deviceLicenses,
    List<ServerType> serverTypes,
    List<InvalidLicenseDetail> invalidLicenses)
        {
            var validServerLicenses = new List<ServerLicense>();
            var validDeviceLicenses = new List<DeviceLicense>();

            for (int i = 0; i < serverLicenses.Count; i++)
            {
                var serverLicense = serverLicenses[i];
                var serverType = serverTypes[i];

                if (IsServerLicenseValidForTypeAndServerType(request.ClientType, serverLicense, serverType))
                {
                    validServerLicenses.Add(serverLicense);
                }
                else
                {
                    invalidLicenses.Add(new InvalidLicenseDetail
                    {
                        LicenseId = serverLicense.Id,
                        Reason = $"Client type mismatch for server type: {serverType}.",
                        LicenseType = "Server"
                    });
                }
            }

            foreach (var deviceLicense in deviceLicenses)
            {
                if (IsDeviceLicenseValidForType(request.ClientType, deviceLicense))
                {
                    validDeviceLicenses.Add(deviceLicense);
                }
                else
                {
                    invalidLicenses.Add(new InvalidLicenseDetail
                    {
                        LicenseId = deviceLicense.Id,
                        Reason = "Client type mismatch.",
                        LicenseType = "Device"
                    });
                }
            }

            return (validServerLicenses, validDeviceLicenses);
        }

        private static bool IsServerLicenseValidForTypeAndServerType(ClientTypeEnum clientType, ServerLicense serverLicense, ServerType serverType)
        {
            return clientType switch
            {
                ClientTypeEnum.Web => serverType == ServerType.WebServer && serverLicense.LicenseData.HasWebsite,
                ClientTypeEnum.WindowsRemotingServer => serverType == ServerType.WindowsServer && serverLicense.LicenseData.HasWindowsHost,
                ClientTypeEnum.WindowsClientWithoutDesign =>
                    (serverType == ServerType.WebServer || serverType == ServerType.WindowsServer) &&
                    (serverLicense.LicenseData.HasWebsite || serverLicense.LicenseData.HasWindowsHost),
                ClientTypeEnum.WindowsClientWithDesign => true, // Matches any server license
                ClientTypeEnum.ServiceApp => true, // Matches any server license
                _ => false // Other types do not match a ServerLicense
            };
        }

        private static bool IsDeviceLicenseValidForType(ClientTypeEnum clientType, DeviceLicense deviceLicense)
        {
            return clientType switch
            {
                ClientTypeEnum.Web => deviceLicense.DeviceCount > 0,
                ClientTypeEnum.WindowsRemotingServer => deviceLicense.DeviceCount > 0,
                ClientTypeEnum.WindowsClientWithoutDesign => deviceLicense.DeviceCount > 0,
                ClientTypeEnum.WindowsClientWithDesign => deviceLicense.DeviceCount > 0,
                _ => false // Other types do not match a DeviceLicense
            };
        }


        public static void ValidateLicensesTimeWithReasons(
            List<ServerLicense> serverLicenses,
            List<DeviceLicense> deviceLicenses,
            LicenseRequest request,
            List<InvalidLicenseDetail> invalidLicenses)
        {
            foreach (var license in serverLicenses.ToList())
            {
                if (!IsLicenseTimeValid(license.LicenseData, request))
                {
                    serverLicenses.Remove(license);
                    invalidLicenses.Add(new InvalidLicenseDetail
                    {
                        LicenseId = license.Id,
                        Reason = "License has expired.",
                        LicenseType = "Server"
                    });
                }
            }

            foreach (var license in deviceLicenses.ToList())
            {
                if (!IsLicenseTimeValid(license.LicenseData, request))
                {
                    deviceLicenses.Remove(license);
                    invalidLicenses.Add(new InvalidLicenseDetail
                    {
                        LicenseId = license.Id,
                        Reason = "License has expired.",
                        LicenseType = "Device"
                    });
                }
            }
        }

        public static bool IsLicenseTimeValid(LicenseData licenseData, LicenseRequest request)
        {
            // Check if the license start and end dates are valid
            if (licenseData.StartDate.HasValue && licenseData.StartDate.Value > DateTime.UtcNow)
            {
                return false; // License is not yet valid
            }

            if (licenseData.EndDate.HasValue && licenseData.EndDate.Value < DateTime.UtcNow)
            {
                return false; // License has expired
            }

            return true; // License is valid
        }



        public static Dictionary<string, List<string>> GetLicenseFeaturesWithNames(
            LicenseRequest request,
            List<ServerLicense> serverLicenses,
            List<DeviceLicense> deviceLicenses)
        {
            var featureMap = new Dictionary<string, List<string>>();
            DateTime currentTime = DateTime.UtcNow;

            void AddLicenseFeatures(LicenseBase license)
            {
                var validFeatures = license.LicenseData.FeaturesList
                    .Where(f => IsFeatureValid(f, currentTime))
                    .Select(f => f.Code)
                    .ToList();

                if (validFeatures.Any())
                {
                    featureMap[license.LicenseName] = validFeatures;
                }
            }

            switch (request.ClientType)
            {
                case ClientTypeEnum.Web:
                case ClientTypeEnum.WindowsRemotingServer:
                    if (serverLicenses.Any())
                    {
                        AddLicenseFeatures(serverLicenses.First());
                    }
                    else if (deviceLicenses.Any())
                    {
                        AddLicenseFeatures(deviceLicenses.First());
                    }
                    break;

                case ClientTypeEnum.WindowsRemotingClient:
                case ClientTypeEnum.WindowsClientWithDesign:
                    if (deviceLicenses.Any())
                    {
                        AddLicenseFeatures(deviceLicenses.First());
                    }
                    else if (serverLicenses.Any())
                    {
                        AddLicenseFeatures(serverLicenses.First());
                    }
                    break;

                case ClientTypeEnum.WindowsClientWithoutDesign:
                    if (deviceLicenses.Any())
                    {
                        AddLicenseFeatures(deviceLicenses.First());
                    }
                    else if (serverLicenses.Any())
                    {
                        AddLicenseFeatures(serverLicenses.First());
                    }
                    break;

                case ClientTypeEnum.ServiceApp:
                    if (deviceLicenses.Any())
                    {
                        AddLicenseFeatures(deviceLicenses.First());
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported client type: {request.ClientType}");
            }

            return featureMap;
        }



        private static void AddValidFeatures(HashSet<string> featureSet, List<LicenseFeature> features, DateTime currentTime)
        {
            foreach (var feature in features)
            {
                if (IsFeatureValid(feature, currentTime))
                {
                    featureSet.Add(feature.Code);
                }
            }
        }

        private static bool IsFeatureValid(LicenseFeature feature, DateTime currentTime)
        {
            if (feature.StartDate.HasValue && feature.StartDate.Value > currentTime)
                return false;
            if (feature.EndDate.HasValue && feature.EndDate.Value < currentTime)
                return false;

            return true;
        }



        public static List<string> GetLicenseFeatures(List<ServerLicense> serverLicenses, List<DeviceLicense> deviceLicenses)
        {
            var featureSet = new HashSet<string>();

            foreach (var serverLicense in serverLicenses)
            {
                foreach (var feature in serverLicense.LicenseData.FeaturesList)
                {
                    if (IsFeatureValid(feature))
                    {
                        featureSet.Add(feature.Code);
                    }
                }
            }

            foreach (var deviceLicense in deviceLicenses)
            {
                foreach (var feature in deviceLicense.LicenseData.FeaturesList)
                {
                    if (IsFeatureValid(feature))
                    {
                        featureSet.Add(feature.Code);
                    }
                }
            }

            return featureSet.ToList();
        }

        // Helper to check if a feature is valid based on time
        private static bool IsFeatureValid(LicenseFeature feature)
        {
            var now = DateTime.UtcNow;

            return (!feature.StartDate.HasValue || feature.StartDate.Value <= now) &&
                   (!feature.EndDate.HasValue || feature.EndDate.Value >= now);
        }



        //---------------------------------------------------------------------------------

        public static (List<ServerLicense> serverLicenses, List<DeviceLicense> deviceLicenses) LoadValidLicenses(
            string name,
            string ip,
            string dbConnection,
            LicenseConfigurationData configData)
        {
            var serverLicenses = new List<ServerLicense>();
            var deviceLicenses = new List<DeviceLicense>();

            serverLicenses.AddRange(FindMatchingServerLicenses(name, ip, dbConnection, configData.ServerList));

            deviceLicenses.AddRange(FindMatchingDeviceLicenses(name, ip, dbConnection, configData.DeviceList));

            return (serverLicenses, deviceLicenses);
        }

        public static IEnumerable<ServerLicense> FindMatchingServerLicenses(
            string name,
            string ip,
            string dbConnection,
            List<ServerConfigurationData> serverConfigs)
        {
            var serverLicenses = new List<ServerLicense>();

            foreach (var serverConfig in serverConfigs)
            {
                if (IsDbConnectionValid(serverConfig, dbConnection))
                {
                    if (IsMatchingServer(serverConfig, name, ip) || IsMatchingLoadBalancedLicenses(serverConfig, name, ip))
                    {
                        var license = LicenseFileManager.LoadSingleLicense(serverConfig.LicenseId);
                        if (license is ServerLicense serverLicense)
                        {
                            serverLicenses.Add(serverLicense);
                        }
                    }
                }
            }

            return serverLicenses;
        }

        private static bool IsMatchingLoadBalancedLicenses(
            ServerConfigurationData serverConfig,
            string deviceName,
            string deviceIp)
        {
            foreach (var loadBalancedDevice in serverConfig.WebServerLoadBalanceDeviceList)
            {
                if (loadBalancedDevice.DeviceData.IsMatch(deviceName, deviceIp))
                {
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<DeviceLicense> FindMatchingDeviceLicenses(
            string deviceName,
            string deviceIp,
            string dbConnection,
            List<DeviceConfigurationData> deviceConfigs)
        {
            var deviceLicenses = new List<DeviceLicense>();
            if (IsDbConnectionLocal(dbConnection) || IsDbConnectionExists(dbConnection))
            {
                foreach (var deviceConfig in deviceConfigs)
                {
                    foreach (var deviceInstance in deviceConfig.InstanceList)
                    {
                        if (deviceInstance.DeviceData.IsMatch(deviceName, deviceIp))
                        {
                            var license = LicenseFileManager.LoadSingleLicense(deviceConfig.LicenseId);
                            if (license is DeviceLicense deviceLicense)
                            {
                                deviceLicenses.Add(deviceLicense);
                            }
                        }

                    }
                }
            }

            return deviceLicenses;
        }

       


        //public static bool ValidateLicenseType(LicenseRequest request, ServerLicense? relevantServerLicense, IEnumerable<DeviceLicense>? relevantDeviceLicenses)
        //{
        //    if (request == null) return false;

        //    switch (request.ClientType)
        //    {
        //        case "Web":
        //            // Can be either a Web Server or a Device
        //            return (relevantServerLicense?.LicenseData.HasWebsite == true) ||
        //                   (relevantDeviceLicenses?.Any(dl => dl.DeviceCount > 0) == true);

        //        case "WindowsRemotingServer":
        //            // Must be a Windows Server
        //            return relevantServerLicense?.LicenseData.HasWindowsHost == true;

        //        case "WindowsRemotingClient":
        //            // Skip validation for this type (* - no checks required)
        //            return true;

        //        case "WindowsClientWithoutDesign":
        //            // Must be a Device, a Web Server, or a Windows Server
        //            return (relevantDeviceLicenses?.Any(dl => dl.DeviceCount > 0) == true) ||
        //                   (relevantServerLicense?.LicenseData.HasWebsite == true) ||
        //                   (relevantServerLicense?.LicenseData.HasWindowsHost == true);

        //        case "WindowsClientWithDesign":
        //            // Must be a Device
        //            return relevantDeviceLicenses?.Any(dl => dl.DeviceCount > 0) == true;

        //        case "ServiceApp":
        //            // Skip validation for this type (* - no checks required)
        //            return true;

        //        default:
        //            // Invalid/unsupported ClientType
        //            return false;
        //    }
        //}


        //public static bool ValidateLicenseType(LicenseRequest request, ServerLicense? relevantLicense)
        //{
        //    if (relevantLicense is null) { return false; }
        //    if (request.ClientType == "web" && !relevantLicense.LicenseData.HasWebsite) return false;
        //    else if (request.ClientType == "windows remoting" && !relevantLicense.LicenseData.HasWindowsHost) return false;
        //    else return true;
        //}

        public static bool ValidateLicensesTime(List<ServerLicense> serverLicenses, List<DeviceLicense> deviceLicenses, LicenseRequest request)
        {
            foreach (var license in serverLicenses.Concat<LicenseBase>(deviceLicenses))
            {
                if (license.LicenseData?.EndDate < DateTime.UtcNow)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsMatchingServer(ServerConfigurationData serverConfig, string deviceName, string deviceIp)
        {
            return 
                serverConfig.WinServerDeviceData.IsMatch(deviceName, deviceIp) || 
                serverConfig.WebServerDeviceData.IsMatch(deviceName, deviceIp);
        }


        private static bool IsDbConnectionLocal(string dbConnection)
        {
            return dbConnection.Contains("localhost") || dbConnection.Contains("127.0.0.1");
        }

        public static bool IsDbConnectionExists(string dbConnection)
        {
            var allConfigurations = LicenseFileManager.LoadAllConfigurations();

            if (allConfigurations?.ServerList == null)
                return false;

            return allConfigurations.ServerList.Any(server =>
                string.Equals(server.DbConnection, dbConnection, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDbConnectionValid(ServerConfigurationData serverConfig, string dbConnection)
        {
            return string.Equals(dbConnection, serverConfig.DbConnection, StringComparison.OrdinalIgnoreCase);
        }


        public static string SignNonce(string nonce)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(nonce));
        }
        public static bool ValidateNonce(string nonce)
        {
            lock (nonceLock)
            {
                if (usedNonces.Contains(nonce))
                {
                    return false;
                }

                usedNonces.Add(nonce);
                return true;
            }
        }

       


        public static DateTime CalculateNextCheckTime(DateTime licenseExpiryDate)
        {
            // Return the minimum of the license expiry date or the constant control interval
            DateTime nextCheck = DateTime.UtcNow.Add(CheckInterval);
            return nextCheck < licenseExpiryDate ? nextCheck.Add(GracePeriod) : licenseExpiryDate.Add(GracePeriod);
        }


        //----------------encrypton methods---------------------------

        public static byte[] DecryptWithPrivateKey(string encryptedAESKey, string privateKey)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedAESKey);
            using (var rsa = RSA.Create())
            {
                rsa.ImportFromPem(privateKey.ToCharArray());
                return rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        public static bool VerifySignature(string data, string signature, string publicKey)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportFromPem(publicKey.ToCharArray());

                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] signatureBytes = Convert.FromBase64String(signature);

                return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        public static string DecryptWithAES(string encryptedData, byte[] aesKey, string aesIV)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
            byte[] iv = Convert.FromBase64String(aesIV);

            using (Aes aes = Aes.Create())
            {
                aes.Key = aesKey;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var msDecrypt = new MemoryStream(encryptedBytes))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }

        //--------------------------------------------------------

        public static (T request, string errorMessage) DecryptAndValidatePayload<T>(JsonElement encryptedPayload)
        {
            try
            {
                if (!encryptedPayload.TryGetProperty("EncryptedData", out JsonElement encryptedDataElement) ||
                    !encryptedPayload.TryGetProperty("EncryptedAESKey", out JsonElement encryptedAESKeyElement) ||
                    !encryptedPayload.TryGetProperty("AesIV", out JsonElement aesIvElement) ||
                    !encryptedPayload.TryGetProperty("Signature", out JsonElement signatureElement))
                {
                    return (default(T), "Invalid payload format.");
                }

                string encryptedData = encryptedDataElement.GetString();
                string encryptedAESKey = encryptedAESKeyElement.GetString();
                string aesIV = aesIvElement.GetString();
                string signature = signatureElement.GetString();

                byte[] aesKey;
                try
                {
                    aesKey = LicenseManagerUtil.DecryptWithPrivateKey(encryptedAESKey, LsPrivateKey);
                }
                catch (CryptographicException ex)
                {
                    return (default(T), "Failed to decrypt AES key: " + ex.Message);
                }

                // Decrypt the encrypted data using AES
                string decryptedJson;
                try
                {
                    decryptedJson = LicenseManagerUtil.DecryptWithAES(encryptedData, aesKey, aesIV);
                }
                catch (CryptographicException ex)
                {
                    return (default(T), "Failed to decrypt data: " + ex.Message);
                }

                // Verify the signature
                bool isSignatureValid;
                try
                {
                    isSignatureValid = LicenseManagerUtil.VerifySignature(decryptedJson, signature, BarsaPublicKey);
                }
                catch (CryptographicException ex)
                {
                    return (default(T), "Failed to verify signature: " + ex.Message);
                }

                if (!isSignatureValid)
                {
                    return (default(T), "Invalid signature. The data has been tampered with.");
                }

                T request;
                try
                {
                    request = JsonConvert.DeserializeObject<T>(decryptedJson);
                }
                catch (JsonException ex)
                {
                    return (default(T), "Failed to parse the JSON: " + ex.Message);
                }

                return (request, null);
            }
            catch (Exception ex)
            {
                return (default(T), "An internal error occurred: " + ex.Message);
            }
        }

    }

}