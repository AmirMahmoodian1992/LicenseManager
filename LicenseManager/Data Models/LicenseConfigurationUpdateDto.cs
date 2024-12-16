using System.ComponentModel.DataAnnotations;

public class UpdateLicenseTreeNodeRequest
{
    public int Index { get; set; }                        
    public string Id { get; set; }                        
    public object NodeToUpdate { get; set; }
    public requetType requetType { get; set; }
}

public enum requetType { WindowsServer = 1, WebServer = 2, LoadBalancer = 3, DeviceInstance = 4 }
public enum ServerType { WindowsServer = 1, WebServer = 2 }

public enum ClientTypeEnum
{
    Web,
    WindowsRemotingServer,
    WindowsRemotingClient,
    WindowsClientWithDesign,
    WindowsClientWithoutDesign,
    ServiceApp
}

public class LicenseRequest
{
    //[Required(ErrorMessage = "IP is required", AllowEmptyStrings = false)]
    //[RegularExpression(@"^(\d{1,3}\.){3}\d{1,3}$", ErrorMessage = "Invalid IP address format")]
    public string Ip { get; set; }

    [Required(ErrorMessage = "Machine name is required", AllowEmptyStrings = false)]
    [StringLength(50, ErrorMessage = "Machine name length can't exceed 50 characters")]
    public string MachineName { get; set; }

    [Required(ErrorMessage = "Client type is required")]
    public ClientTypeEnum ClientType { get; set; }

    [Required(ErrorMessage = "Database connection is required", AllowEmptyStrings = false)]
    [StringLength(255, ErrorMessage = "Database connection string too long")]
    public string DbConnection { get; set; }

    [Required(ErrorMessage = "Hardware data is required", AllowEmptyStrings = false)]
    public string HardwareData { get; set; }

    [Required]
    [DataType(DataType.DateTime, ErrorMessage = "Invalid date-time format")]
    public DateTime NowTime { get; set; }

    [Required(ErrorMessage = "Nonce is required", AllowEmptyStrings = false)]
    [StringLength(50, ErrorMessage = "Nonce can't exceed 50 characters")]
    public string Nonce { get; set; }
}



public class StatusAndResponse
{
    public int StatusCode { get; set; }
    public LicenseResponse licenseResponse { get; set; }
}

public class LicenseResponse
{
    public bool Authorized { get; set; }
    public DateTime? NextCheckTime { get; set; }
    public List<string>? FeatureList { get; set; }
    public string? LicenseName { get; set; } // "Server" or "Device"

    public string? LicenseType { get; set; } // "Server" or "Device"
    public string? Reason { get; set; } // Explanation for unauthorized cases
    public List<InvalidLicenseDetail>? InvalidLicenses { get; set; } // Details of invalid licenses
    public string? SignedNonce { get; set; } // Signed nonce for the client
}

public class InvalidLicenseDetail
{
    public string LicenseId { get; set; }
    public string Reason { get; set; }
    public string LicenseType { get; set; } // "Server" or "Device"
}





public class ActivationCodeRequest
{
    public string ActivationCodeFromBarsa { get; set; }
}

public class Payload
{
    public string EncryptedData { get; set; }
    public string EncryptedAESKey { get; set; }
    public string AesIV { get; set; }
    public string Signature { get; set; }
}

public class ActivationResult
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public bool IsActivated { get; set; } 
}
