using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public class LicenseTreeData
{
    public string CustomerName { get; set; }
    public string ScopeName { get; set; }
    public List<LicenseTreeServer> ServerLicenses { get; set; }
    public List<LicenseTreeDevice> DeviceLicenses { get; set; }
}

public class LicenseTreeServer
{
    public string Id { get; set; }
    public string LicenseName { get; set; }
    public string ServerType { get; set; }
    public bool HasLoadBalancing { get; set; }
    public int LoadBalancerServerCount { get; set; }
    //public bool HasWindowsHost { get; set; }
    //public bool HasWebsite { get; set; }
    public DeviceData? WebServerDeviceData { get; set; }
    public DeviceData? WinServerDeviceData { get; set; }
    public string DbConnection { get; set; }
    public List<LoadBalance> LoadBalance { get; set; }
}

public class LoadBalance
{
    public int Index { get; set; }
    public DeviceData? DeviceData { get; set; }
    //public string? Name { get; set; }
    //public string? IP { get; set; }
}

public class LicenseTreeDevice
{
    public string Id { get; set; }
    public string LicenseName { get; set; }
    public int DeviceCount { get; set; }
    public List<DeviceInstance> Devices { get; set; }
}

public class DeviceInstance
{
    public int Index { get; set; }
    public DeviceData? DeviceData { get; set; }
    //public string? Name { get; set; }
    //public string? IP { get; set; }
}
