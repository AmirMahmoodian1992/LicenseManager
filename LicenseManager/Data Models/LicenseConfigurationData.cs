
public class LicenseConfigurationData
{
    public List<ServerConfigurationData> ServerList { get; set; }
    public List<DeviceConfigurationData> DeviceList { get; set; }
}
public class ServerConfigurationData
{
    public string LicenseId { get; set; }
    public string? DbConnection { get; set; }
    public DeviceData? WebServerDeviceData { get; set; }
    public DeviceData? WinServerDeviceData { get; set; }
    public List<DeviceInstanceConfigurationData> WebServerLoadBalanceDeviceList { get; set; }
}

public class DeviceConfigurationData
{
    public string LicenseId { get; set; }
    public List<DeviceInstanceConfigurationData> InstanceList { get; set; }
}
public class DeviceInstanceConfigurationData
{
    public int Index { get; set; }
    public DeviceData? DeviceData { get; set; }
}

public enum DeviceInfoType { DeviceName, Ip }
public class DeviceData
{
    public DeviceInfoType DeviceInfoType { get; set; } = DeviceInfoType.DeviceName;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIp { get; set; } = string.Empty;
    public bool IsMatch(string deviceName, string deviceIp)
    {
        return
            (DeviceInfoType == DeviceInfoType.DeviceName &&
             string.Equals(DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)) ||
            (DeviceInfoType == DeviceInfoType.Ip && DeviceIp == deviceIp);
    }


    internal bool IsSameAs(DeviceData data)
    {
        if (data == null)
            return false; // Or handle null as per your application's logic.

        if (this.DeviceInfoType != data.DeviceInfoType)
            return false;

        if (this.DeviceInfoType == DeviceInfoType.DeviceName &&
            (!string.Equals(DeviceName,data.DeviceName, StringComparison.OrdinalIgnoreCase) || this.DeviceName.IsNullOrEmpty()))
            return false;

        if (this.DeviceInfoType == DeviceInfoType.Ip &&
            (this.DeviceIp != data.DeviceIp || this.DeviceIp.IsNullOrEmpty()))
            return false;

        return true;
    }

}

public static class StringHelper
{
    public static bool IsNullOrEmpty(this string value)
    {
        return string.IsNullOrEmpty(value);
    }
}