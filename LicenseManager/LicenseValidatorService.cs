//using LicenseManager;

//public class LicenseValidatorService
//{
//    private readonly IDeviceMatchingLayer _deviceMatchingLayer;
//    private readonly IDbValidationLayer _dbValidationLayer;
//    private readonly ILicenseTypeValidationLayer _licenseTypeValidationLayer;
//    private readonly ILicenseTimingValidationLayer _licenseTimingValidationLayer;

//    public LicenseValidatorService(
//        IDeviceMatchingLayer deviceMatchingLayer,
//        IDbValidationLayer dbValidationLayer,
//        ILicenseTypeValidationLayer licenseTypeValidationLayer,
//        ILicenseTimingValidationLayer licenseTimingValidationLayer)
//    {
//        _deviceMatchingLayer = deviceMatchingLayer;
//        _dbValidationLayer = dbValidationLayer;
//        _licenseTypeValidationLayer = licenseTypeValidationLayer;
//        _licenseTimingValidationLayer = licenseTimingValidationLayer;
//    }

//    public LicenseValidationResult Validate(LicenseRequest request)
//    {
//        var result = new LicenseValidationResult();

//        // Device/Server Matching
//        var matchingResult = _deviceMatchingLayer.Match(request);
//        if (!matchingResult.IsValid)
//        {
//            result.AddRejectionReason("Device or Server mismatch");
//            return result;
//        }

//        // DB Connection Validation
//        var dbResult = _dbValidationLayer.Validate(request, matchingResult.MatchedLicenses);
//        if (!dbResult.IsValid)
//        {
//            result.AddRejectionReason("Database validation failed");
//            return result;
//        }

//        // License Type Validation
//        var typeResult = _licenseTypeValidationLayer.Validate(request, dbResult.MatchedLicenses);
//        if (!typeResult.IsValid)
//        {
//            result.AddRejectionReason("License type mismatch");
//            return result;
//        }

//        // License Timing Validation
//        var timingResult = _licenseTimingValidationLayer.Validate(request, typeResult.MatchedLicenses);
//        if (!timingResult.IsValid)
//        {
//            result.AddRejectionReason("License timing invalid");
//            return result;
//        }

//        // Success
//        result.IsAuthorized = true;
//        result.FeatureList = timingResult.FeatureList;
//        result.NextCheckTime = timingResult.NextCheckTime;
//        return result;
//    }
//}
