public class LicenseRuleData
{
    public required List<Customer> CustomerList { get; set; }
}

public class Customer
{
    public required string Name { get; set; }
    public required List<LicenseScope> LicenseScopeList { get; set; }
    public LicenseData? DefaultLicenseData { get; set; }
}

public class LicenseScope
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    public List<ServerLicense>? ServerLicenseList { get; set; }
    public List<DeviceLicense>? DeviceLicenseList { get; set; }

}

public class LicenseData
{
    public required int ConcurrentUsers { get; set; }
    public required bool HasWindowsHost { get; set; }
    public required bool HasWebsite { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public required List<LicenseFeature> FeaturesList { get; set; }
}

public class LicenseFeature
{
    public required string Code { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

}

public abstract class LicenseBase
{
    public required string Id { get; set; }
    public required string LicenseName { get; set; }
    public required LicenseData LicenseData { get; set; }
}

public enum ServerTypeEnum { Operational = 1, Test = 3, Development = 2 };

public class ServerLicense : LicenseBase
{
    public required ServerTypeEnum ServerType { get; set; }
    public required bool HasLoadBalancing { get; set; }
    public required int LoadBalancerServerCount { get; set; }
}

public class DeviceLicense : LicenseBase
{
    public required int DeviceCount { get; set; }

  
}