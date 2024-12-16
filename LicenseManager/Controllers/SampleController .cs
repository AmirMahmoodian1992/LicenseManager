using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using JsonException = Newtonsoft.Json.JsonException;

namespace LicenseManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SampleController : ControllerBase
    {
        private readonly ILogger<SampleController> _logger;
        private readonly ActivationManager _activationManager;
        private readonly ActivationState _activationState;

        public SampleController(ILogger<SampleController> logger, ActivationManager activationManager, ActivationState activationState)
        {
            _logger = logger;
            _activationManager = activationManager;
            _activationState = activationState;
        }

        [HttpGet("generate-secret-key")]
        [LocalOnly]

        public ActionResult<string> GenerateSecretKey()
        {
            try
            {
                _logger.LogInformation("Generating secret key.");
                var secretKey = TotpActivation.TotpManager.GenerateRandomNineDigitNumber();
                _logger.LogInformation("Secret key generated successfully.");
                return Ok(secretKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secret key.");
                return StatusCode(500, "An error occurred while generating the secret key.");
            }
        }

        [HttpGet("generate-reactivation-key")]
        [LocalOnly]

        public ActionResult<string> GenerateReactivationKey()
        {
            try
            {
                _logger.LogInformation("Generating reactivation key.");
                bool isReactivated = true;
                var reactivationKey = TotpActivation.TotpManager.GenerateRandomNineDigitNumber(isReactivated);
                _logger.LogInformation("Reactivation key generated successfully.");
                return Ok(reactivationKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reactivation key.");
                return StatusCode(500, "An error occurred while generating the reactivation key.");
            }
        }

        [HttpPost("first-time-activation")]
        [LocalOnly]

        public ActionResult<ActivationResult> FirstTimeActivation([FromBody] ActivationCodeRequest request)
        {
            try
            {
                _logger.LogInformation("Starting first-time activation process.");
                var licenseScopeId = LicenseFileManager
                    .LoadSystemRules()
                    .CustomerList
                    .FirstOrDefault()?
                    .LicenseScopeList
                    .FirstOrDefault()?
                    .Id;

                var secretKey = System.IO.File.ReadAllText("SecretKey.txt");
                if (string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("Secret key not found. Activation cannot proceed.");
                    return BadRequest("Secret key not generated. Please generate the secret key first.");
                }

                var result = _activationManager.FirstTimeActivation(request.ActivationCodeFromBarsa, secretKey, licenseScopeId);
                _logger.LogInformation("First-time activation process completed with StatusCode: {StatusCode} and Message {message}", result.StatusCode, result.Message);

                return result.StatusCode switch
                {
                    2000 => Ok(result),
                    4001 => BadRequest(result),
                    4002 => BadRequest(result),
                    4003 => BadRequest(result),
                    4004 => BadRequest(result),
                    4005 => StatusCode(500, result),
                    _ => StatusCode(500, new ActivationResult { StatusCode = 5000, Message = "Unknown error." })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during first-time activation.");
                return StatusCode(500, new ActivationResult { StatusCode = 5001, Message = "An error occurred during activation." });
            }
        }

        [HttpPost("reactivate")]
        [LocalOnly]

        public ActionResult<ActivationResult> Reactivate([FromBody] ActivationCodeRequest request)
        {
            try
            {
                _logger.LogInformation("Starting reactivation process.");
                var licenseScopeId = LicenseFileManager
                    .LoadSystemRules()
                    .CustomerList
                    .FirstOrDefault()?
                    .LicenseScopeList
                    .FirstOrDefault()?
                    .Id;

                var secretKey = System.IO.File.ReadAllText("SecretKey.txt");
                if (string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("Secret key not found. Reactivation cannot proceed.");
                    return BadRequest("Secret key not generated. Please generate the secret key first.");
                }

                var result = _activationManager.Reactivation(request.ActivationCodeFromBarsa, secretKey, licenseScopeId);
                _logger.LogInformation("Reactivation process completed with StatusCode: {StatusCode}", result.StatusCode);

                return result.StatusCode switch
                {
                    2001 => Ok(result),
                    4006 => BadRequest(result),
                    4007 => BadRequest(result),
                    5002 => StatusCode(500, result),
                    _ => StatusCode(500, new ActivationResult { StatusCode = 5000, Message = "Unknown error." })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reactivation.");
                return StatusCode(500, new ActivationResult { StatusCode = 5003, Message = "An error occurred during reactivation." });
            }
        }

        [HttpGet("check-activation-status")]
        [LocalOnly]

        public ActionResult<ActivationResult> CheckActivationStatus()
        {
            try
            {
                _logger.LogInformation("Checking activation status.");
                var activationState = _activationState.LoadFromFile();

                if (activationState.IsActivated)
                {
                    _logger.LogInformation("System is currently activated.");
                    return Ok(new ActivationResult
                    {
                        StatusCode = 2004,
                        Message = "System is currently activated.",
                        IsActivated = true
                    });
                }
                else
                {
                    _logger.LogInformation("System is not activated.");
                    return Ok(new ActivationResult
                    {
                        StatusCode = 2005,
                        Message = "System is not activated.",
                        IsActivated = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check activation status.");
                return StatusCode(500, new ActivationResult
                {
                    StatusCode = 5004,
                    Message = "Failed to check activation status.",
                    IsActivated = false
                });
            }
        }

        [HttpDelete("clearconfigs")]
        [LocalOnly]

        public IActionResult ClearAllConfigs()
        {
            _logger.LogInformation("Starting ClearAllConfigs request.");

            try
            {
                _logger.LogInformation("Clearing all configurations.");
                LicenseFileManager.ClearAllConfigurations();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while clearing configurations.");
                return StatusCode(500, new { Message = "Failed to clear configurations due to an internal error." });
            }

            ActivationResult result;
            try
            {
                _logger.LogInformation("Attempting system deactivation.");
                result = _activationManager.Deactivate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during system deactivation.");
                return StatusCode(500, new { Message = "Deactivation failed due to an internal error." });
            }

            _logger.LogInformation("Processing deactivation result with StatusCode: {StatusCode}", result.StatusCode);
            return result.StatusCode switch
            {
                2000 => Ok(result),
                2002 => Ok(new ActivationResult { StatusCode = 2002, Message = "System is already deactivated.", IsActivated = false }),
                2003 => Ok(new ActivationResult { StatusCode = 2003, Message = "Deactivation successful.", IsActivated = false }),
                4001 => BadRequest(result),
                4002 => BadRequest(result),
                4003 => BadRequest(result),
                4004 => BadRequest(result),
                4005 => StatusCode(500, result),
                _ => StatusCode(500, new ActivationResult { StatusCode = 5000, Message = "Unknown error." })
            };
        }

        [HttpGet("GetTreeView")]
        [LocalOnly]
        public IActionResult GetAllLicensesForTreeView()
        {
            _logger.LogInformation("Starting GetAllLicensesForTreeView request.");

            try
            {
                var systemRules = LicenseFileManager.LoadSystemRules();
                if (systemRules?.CustomerList == null || !systemRules.CustomerList.Any())
                {
                    _logger.LogWarning("No licenses found in system rules.");
                    return NotFound("No licenses found.");
                }

                var firstCustomer = systemRules.CustomerList.FirstOrDefault();
                if (firstCustomer?.LicenseScopeList == null || !firstCustomer.LicenseScopeList.Any())
                {
                    _logger.LogWarning("No licenses found for the first customer.");
                    return NotFound("No licenses found for the first customer.");
                }

                var licenseScope = firstCustomer.LicenseScopeList.FirstOrDefault();
                _logger.LogInformation("Loaded first customer and license scope.");

                var configData = LicenseFileManager.LoadAllConfigurations();
                var serverList = configData?.ServerList ?? new List<ServerConfigurationData>();
                var deviceList = configData?.DeviceList ?? new List<DeviceConfigurationData>();

                var licenseTreeData = new LicenseTreeData
                {
                    CustomerName = firstCustomer.Name,
                    ScopeName = licenseScope.Name,
                };

                _logger.LogInformation("Populating server licenses.");
                licenseTreeData.ServerLicenses = licenseScope?.ServerLicenseList?.Select(sl =>
                {
                    try
                    {
                        var serverData = serverList.FirstOrDefault(sc => sc.LicenseId == sl.Id);
                        _logger.LogInformation("Processing server license ID: {LicenseId}", sl.Id);

                        var licenseTreeServer = new LicenseTreeServer
                        {
                            Id = sl.Id,
                            LicenseName = sl.LicenseName,
                            ServerType = sl.ServerType.ToString(),
                            HasLoadBalancing = sl.HasLoadBalancing,
                            LoadBalancerServerCount = sl.LoadBalancerServerCount,
                            DbConnection = serverData?.DbConnection,
                            WinServerDeviceData = serverData?.WinServerDeviceData,
                            WebServerDeviceData = serverData?.WebServerDeviceData,
                        };

                        if (sl.HasLoadBalancing)
                        {
                            _logger.LogInformation("Setting up load balancing devices for server license ID: {LicenseId}", sl.Id);
                            licenseTreeServer.LoadBalance = Enumerable.Range(0, sl.LoadBalancerServerCount).Select(i =>
                            {
                                var lbDevice = serverData?.WebServerLoadBalanceDeviceList?.ElementAtOrDefault(i);
                                return new LoadBalance
                                {
                                    Index = i,
                                    DeviceData = lbDevice?.DeviceData
                                };
                            }).ToList();
                        }
                        else
                        {
                            licenseTreeServer.LoadBalance = new List<LoadBalance>();
                        }

                        return licenseTreeServer;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing server license ID: {LicenseId}", sl.Id);
                        return null;
                    }
                }).Where(s => s != null).ToList() ?? new List<LicenseTreeServer>();

                _logger.LogInformation("Populating device licenses.");
                licenseTreeData.DeviceLicenses = licenseScope?.DeviceLicenseList?.Select(dl =>
                {
                    try
                    {
                        var deviceData = deviceList?.FirstOrDefault(dc => dc.LicenseId == dl.Id);
                        _logger.LogInformation("Processing device license ID: {LicenseId}", dl.Id);

                        var licenseTreeDevice = new LicenseTreeDevice
                        {
                            Id = dl.Id,
                            LicenseName = dl.LicenseName,
                            DeviceCount = dl.DeviceCount,
                            Devices = Enumerable.Range(0, dl.DeviceCount).Select(i =>
                            {
                                var deviceInstance = deviceData?.InstanceList?.ElementAtOrDefault(i);
                                return new DeviceInstance
                                {
                                    Index = i,
                                    DeviceData = deviceInstance?.DeviceData
                                };
                            }).ToList()
                        };

                        return licenseTreeDevice;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing device license ID: {LicenseId}", dl.Id);
                        return null;
                    }
                }).Where(d => d != null).ToList() ?? new List<LicenseTreeDevice>();

                var json = JsonConvert.SerializeObject(licenseTreeData, Formatting.Indented);
                _logger.LogInformation("GetAllLicensesForTreeView request processed successfully.");
                return Ok(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing GetAllLicensesForTreeView.");
                return StatusCode(500, "An internal error occurred.");
            }
        }

        [HttpPost("updateLicenseTreeData")]
        [LocalOnly]
        public IActionResult UpdateLicenseTreeData([FromBody] LicenseTreeData licenseTreeData)
        {
            _logger.LogInformation("Starting UpdateLicenseTreeData request.");

            if (licenseTreeData == null)
            {
                _logger.LogWarning("Received null LicenseTreeData.");
                return BadRequest("LicenseTreeData cannot be null.");
            }

            try
            {
                _logger.LogInformation("Loading existing configurations.");
                var systemConfig = LicenseFileManager.LoadAllConfigurations();

                _logger.LogInformation("Processing server licenses.");
                foreach (var licenseTreeServer in licenseTreeData.ServerLicenses)
                {
                    try
                    {
                        _logger.LogInformation("Updating configuration for server license ID: {LicenseId}", licenseTreeServer.Id);

                        var newServerConfigurationData = new ServerConfigurationData
                        {
                            LicenseId = licenseTreeServer.Id,
                            DbConnection = licenseTreeServer.DbConnection,
                            WinServerDeviceData = licenseTreeServer.WinServerDeviceData,
                            WebServerDeviceData = licenseTreeServer.WebServerDeviceData,
                        };

                        LicenseFileManager.UpdateOrCreateServerConfiguration(newServerConfigurationData);
                        _logger.LogInformation("Server configuration for license ID {LicenseId} updated or created.", licenseTreeServer.Id);

                        if (licenseTreeServer.HasLoadBalancing && licenseTreeServer.LoadBalance != null)
                        {
                            _logger.LogInformation("Processing load balancing devices for server license ID: {LicenseId}", licenseTreeServer.Id);

                            foreach (var lbDevice in licenseTreeServer.LoadBalance)
                            {
                                try
                                {
                                    var lbDeviceConfig = new DeviceInstanceConfigurationData
                                    {
                                        Index = lbDevice.Index,
                                        DeviceData = lbDevice.DeviceData
                                    };

                                    LicenseFileManager.UpdateLoadBalancerInstance(licenseTreeServer.Id, lbDeviceConfig);
                                    _logger.LogInformation("Load balancer device (Index: {Index}) for license ID {LicenseId} updated.", lbDevice.Index, licenseTreeServer.Id);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error updating load balancer device (Index: {Index}) for license ID: {LicenseId}.", lbDevice.Index, licenseTreeServer.Id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing server license ID: {LicenseId}.", licenseTreeServer.Id);
                    }
                }

                _logger.LogInformation("Processing device licenses.");
                foreach (var licenseTreeDevice in licenseTreeData.DeviceLicenses)
                {
                    try
                    {
                        _logger.LogInformation("Updating devices for license ID: {LicenseId}", licenseTreeDevice.Id);

                        foreach (var deviceInstance in licenseTreeDevice.Devices)
                        {
                            try
                            {
                                var deviceConfig = new DeviceInstanceConfigurationData
                                {
                                    Index = deviceInstance.Index,
                                    DeviceData = deviceInstance.DeviceData
                                };

                                LicenseFileManager.UpdateDeviceInstance(licenseTreeDevice.Id, deviceConfig);
                                _logger.LogInformation("Device (Index: {Index}) for license ID {LicenseId} updated.", deviceInstance.Index, licenseTreeDevice.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error updating device (Index: {Index}) for license ID: {LicenseId}.", deviceInstance.Index, licenseTreeDevice.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing device license ID: {LicenseId}.", licenseTreeDevice.Id);
                    }
                }

                _logger.LogInformation("UpdateLicenseTreeData request processed successfully.");
                return Ok("LicenseTreeData processed and updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating LicenseTreeData.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }
        // POST: api/sample/uploadEncryptedJson
        [HttpPost("uploadEncryptedJson")]
        [LocalOnly]

        public IActionResult UploadEncryptedJson([FromBody] JsonElement encryptedPayload)
        {
            _logger.LogInformation("Starting UploadEncryptedJson request.");

            if (encryptedPayload.ValueKind == JsonValueKind.Undefined)
            {
                _logger.LogWarning("Received undefined encryptedPayload.");
                return BadRequest("Encrypted payload cannot be undefined.");
            }

            try
            {
                _logger.LogInformation("Loading existing system rules and configurations.");
                var existingRules = LicenseFileManager.LoadSystemRules();
                var existingConfigs = LicenseFileManager.LoadAllConfigurations();

                _logger.LogInformation("Attempting to decrypt and validate the encrypted payload.");
                var (licenseData, errorMessage) = LicenseManagerUtil.DecryptAndValidatePayload<LicenseRuleData>(encryptedPayload);

                if (licenseData == null)
                {
                    _logger.LogWarning("Decryption and validation failed: {ErrorMessage}", errorMessage);
                    return BadRequest(errorMessage);
                }

                _logger.LogInformation("Saving updated system rules.");
                LicenseFileManager.SaveSystemRules(licenseData);
                _logger.LogInformation("System rules saved successfully.");

                var newRules = LicenseFileManager.LoadSystemRules();

                _logger.LogInformation("Payload decrypted and validated successfully.");

                //if (!LicenseFileManager.IsConfigConsistentWithRules(existingConfigs, newRules))
                //{
                    _logger.LogInformation("Inconsistencies detected between existing configurations and license rules. Updating configurations.");
                    LicenseFileManager.UpdateConfigurationBasedOnLicenseRules(newRules, existingConfigs);
                    _logger.LogInformation("Configurations updated based on new license rules.");
                //}
                //else
                //{
                //    _logger.LogInformation("Existing configurations are consistent with license rules. No updates needed.");
                //}

               

                _logger.LogInformation("UploadEncryptedJson request processed successfully.");
                return Ok("Licenses uploaded and verified successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An internal error occurred while processing UploadEncryptedJson request.");
                return StatusCode(500, "An internal error occurred: " + ex.Message);
            }
        }

        [HttpPost("check-license")]
        public IActionResult CheckLicense([FromBody] JsonElement encryptedPayload)
        {
            try
            {
                _logger.LogInformation("Received a request to check license");

                // Step 1: Decrypt and validate payload
                var (request, errorMessage) = LicenseManagerUtil.DecryptAndValidatePayload<LicenseRequest>(encryptedPayload);

                if (request == null)
                {
                    _logger.LogWarning("Invalid request payload.");
                    return RespondWithBadRequest(null, "Invalid request payload.");
                }

                if (!LicenseManagerUtil.ValidateNonce(request.Nonce))
                {
                    _logger.LogWarning("Invalid nonce.");
                    return RespondWithBadRequest(request.Nonce, "Invalid nonce.");
                }

                if (!_activationState.LoadFromFile().IsActivated)
                {
                    _logger.LogWarning("System is not activated.");
                    return RespondWithUnauthorized(request?.Nonce, "The system is not activated.");
                }

                var configData = LicenseFileManager.LoadAllConfigurations();

                var (potentialServerLicenses, potentialDeviceLicenses, serverTypes) = LicenseManagerUtil.LoadPotentialLicenses(
                    request.MachineName,
                    request.Ip,
                    configData
                );

                if (!potentialServerLicenses.Any() && !potentialDeviceLicenses.Any())
                {
                    _logger.LogWarning("No matching licenses found.");
                    return RespondWithUnauthorized(request.Nonce, "No matching licenses found.");
                }

                var invalidLicenses = new List<InvalidLicenseDetail>();

                var (validTypeServerLicenses, validTypeDeviceLicenses) = LicenseManagerUtil.ValidateAndFilterLicensesByTypeWithReasons(
                    request,
                    potentialServerLicenses,
                    potentialDeviceLicenses,
                    serverTypes,
                    invalidLicenses
                );

                var (validServerLicenses, validDeviceLicenses) = LicenseManagerUtil.ValidateDbConnectionsWithReasons(
                    validTypeServerLicenses,
                    validTypeDeviceLicenses,
                    request.DbConnection,
                    configData,
                    invalidLicenses
                );

                LicenseManagerUtil.ValidateLicensesTimeWithReasons(validServerLicenses, validDeviceLicenses, request, invalidLicenses);

                if (!validServerLicenses.Any() && !validDeviceLicenses.Any())
                {
                    _logger.LogWarning("No valid licenses found.");
                    return RespondWithUnauthorized(request.Nonce, "No valid licenses found.", invalidLicenses);
                }

                var allLicenses = validTypeServerLicenses
                    .Cast<LicenseBase>()
                    .Concat(validTypeDeviceLicenses.Cast<LicenseBase>())
                    .ToList();



                var featureList = LicenseManagerUtil.GetLicenseFeaturesWithNames(request ,validTypeServerLicenses, validTypeDeviceLicenses);


                var licenceName = featureList.Keys.FirstOrDefault();

                var licenseType = validTypeServerLicenses.Any() ? "Server" : "Device";

                var featureLicst = featureList.Values.FirstOrDefault();

                var nextCheckTime = LicenseManagerUtil.CalculateNextCheckTime(
                    allLicenses
                        .Where(license => license.LicenseData.EndDate.HasValue)
                        .Select(license => license.LicenseData.EndDate.Value)
                        .DefaultIfEmpty(DateTime.MaxValue)
                        .Min()
                );

                var successResponse = new LicenseResponse
                {
                    Authorized = true,
                    NextCheckTime = nextCheckTime,
                    LicenseName = licenceName,
                    FeatureList = featureLicst,
                    LicenseType = licenseType,
                    SignedNonce = LicenseManagerUtil.SignNonce(request.Nonce)
                };

                _logger.LogInformation("License validation successful.");
                return EncryptAndRespond(HttpStatusCode.OK, successResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking the license.");
                return RespondWithInternalServerError("An unexpected error occurred.");
            }
        }

        

        private IActionResult RespondWithUnauthorized(string? nonce, string reason, List<InvalidLicenseDetail>? details = null)
        {
            var errorResponse = new LicenseResponse
            {
                Authorized = false,
                Reason = reason,
                InvalidLicenses = details,
                SignedNonce = nonce != null ? LicenseManagerUtil.SignNonce(nonce) : null
            };

            return EncryptAndRespond(HttpStatusCode.Unauthorized, errorResponse);
        }
        private IActionResult RespondWithInternalServerError(string errorMessage)
        {
            var errorResponse = new LicenseResponse
            {
                Authorized = false,
                Reason = errorMessage,
                SignedNonce = null // No signed nonce for internal server errors
            };

            return EncryptAndRespond(HttpStatusCode.InternalServerError, errorResponse);
        }
        private IActionResult RespondWithBadRequest(string? nonce, string errorMessage)
        {
            var errorResponse = new LicenseResponse
            {
                Authorized = false,
                Reason = errorMessage,
                SignedNonce = nonce != null ? LicenseManagerUtil.SignNonce(nonce) : null
            };

            return EncryptAndRespond(HttpStatusCode.BadRequest, errorResponse);
        }
        private IActionResult RespondWithUnauthorized(string? nonce, string reason)
        {
            var errorResponse = new LicenseResponse
            {
                Authorized = false,
                Reason = reason,
                SignedNonce = nonce != null ? LicenseManagerUtil.SignNonce(nonce) : null
            };

            return EncryptAndRespond(HttpStatusCode.Unauthorized, errorResponse);
        }







        //[HttpPost("check-license")]
        //public IActionResult CheckLicense([FromBody] JsonElement encryptedPayload)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Received a request to check license");

        //        var (request, errorMessage) = LicenseManagerUtil.DecryptAndValidatePayload<LicenseRequest>(encryptedPayload);

        //        if (request == null || !LicenseManagerUtil.ValidateNonce(request.Nonce) || !ModelState.IsValid)
        //        {
        //            return RespondWithBadRequest(request?.Nonce);
        //        }

        //        if (!_activationState.LoadFromFile().IsActivated)
        //        {
        //            return RespondWithUnauthorized(request?.Nonce);
        //        }

        //        // Step 1: Load all potential licenses (without DB validation yet)
        //        var configData = LicenseFileManager.LoadAllConfigurations();
        //        var (potentialServerLicenses, potentialDeviceLicenses) =
        //            LicenseManagerUtil.LoadPotentialLicenses(request.MachineName, request.Ip, configData);

        //        if (!potentialServerLicenses.Any() && !potentialDeviceLicenses.Any())
        //        {
        //            return RespondWithUnauthorized(request.Nonce);
        //        }

        //        var (validServerLicenses, validDeviceLicenses) =
        //            LicenseManagerUtil.ValidateDbConnections(potentialServerLicenses, potentialDeviceLicenses, request.DbConnection, configData);

        //        if (!validServerLicenses.Any() && !validDeviceLicenses.Any())
        //        {
        //            return RespondWithUnauthorized(request.Nonce);
        //        }

        //        var (validTypeServerLicenses, validTypeDeviceLicenses) =
        //            LicenseManagerUtil.ValidateAndFilterLicensesByType(
        //                request,
        //                validServerLicenses,
        //                validDeviceLicenses
        //            );

        //        if (!validServerLicenses.Any() && !validDeviceLicenses.Any())
        //        {
        //            return RespondWithUnauthorized(request.Nonce);
        //        }



        //        if (!LicenseManagerUtil.ValidateLicensesTime(validTypeServerLicenses, validTypeDeviceLicenses, request))
        //        {
        //            return RespondWithDelayedUnauthorized(request.Nonce);
        //        }

        //        var minEndDate = validServerLicenses
        //            .Where(sl => sl.LicenseData.EndDate.HasValue)
        //            .Select(sl => sl.LicenseData.EndDate.Value)
        //            .Concat(validDeviceLicenses
        //                .Where(dl => dl.LicenseData.EndDate.HasValue)
        //                .Select(dl => dl.LicenseData.EndDate.Value))
        //            .Min();

        //        DateTime nextCheckTime = LicenseManagerUtil.CalculateNextCheckTime(minEndDate);
        //        var successResponse = new LicenseResponse
        //        {
        //            Authorized = true,
        //            NextCheckTime = nextCheckTime,
        //            FeatureList = LicenseManagerUtil.GetLicenseFeatures(validServerLicenses, validDeviceLicenses),
        //            SignedNonce = LicenseManagerUtil.SignNonce(request.Nonce)
        //        };

        //        return EncryptAndRespond(HttpStatusCode.OK, successResponse);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An error occurred while checking the license.");
        //        return RespondWithInternalServerError();
        //    }
        //}


        private IActionResult RespondWithBadRequest(string? nonce)
        {
            return EncryptAndRespond(HttpStatusCode.BadRequest, new LicenseResponse
            {
                Authorized = false,
                NextCheckTime = DateTime.MinValue,
                FeatureList = new List<string>(),
                SignedNonce = LicenseManagerUtil.SignNonce(nonce)
            });
        }

        private IActionResult RespondWithUnauthorized(string? nonce)
        {
            return EncryptAndRespond(HttpStatusCode.Unauthorized, new LicenseResponse
            {
                Authorized = false,
                NextCheckTime = DateTime.MinValue,
                FeatureList = new List<string>(),
                SignedNonce = LicenseManagerUtil.SignNonce(nonce)
            });
        }



        private IActionResult RespondWithInternalServerError()
        {
            return EncryptAndRespond(HttpStatusCode.InternalServerError, new LicenseResponse
            {
                Authorized = false,
                NextCheckTime = DateTime.MinValue,
                FeatureList = new List<string>(),
                SignedNonce = string.Empty
            });
        }
        private IActionResult RespondWithDelayedUnauthorized(string? nonce, string reason)
        {
            var errorResponse = new LicenseResponse
            {
                Authorized = false,
                Reason = reason,
                SignedNonce = nonce != null ? LicenseManagerUtil.SignNonce(nonce) : null
            };

            return EncryptAndRespond(HttpStatusCode.Forbidden, errorResponse);
        }



        //[HttpPost("check-license")]
        //public IActionResult CheckLicense([FromBody] JsonElement encryptedPayload)
        //{

        //    try
        //    {
        //        _logger.LogInformation("Received a request to check license");


        //        var ipAddress = this.HttpContext.Connection.RemoteIpAddress;

        //        var (request, errorMessage) = LicenseManagerUtil.DecryptAndValidatePayload<LicenseRequest>(encryptedPayload);
        //        request.Ip = ipAddress.ToString();
        //        if (request == null || !LicenseManagerUtil.ValidateNonce(request.Nonce) || !ModelState.IsValid)
        //        {
        //            return EncryptAndRespond(HttpStatusCode.BadRequest, new LicenseResponse
        //            {
        //                Authorized = false,
        //                NextCheckTime = DateTime.MinValue,
        //                FeatureList = new List<string>(),
        //                SignedNonce = LicenseManagerUtil.SignNonce(request?.Nonce)
        //            });
        //        }

        //        if (!_activationState.LoadFromFile().IsActivated)
        //        {
        //            return EncryptAndRespond(HttpStatusCode.Unauthorized, new LicenseResponse
        //            {
        //                Authorized = false,
        //                NextCheckTime = DateTime.MinValue,
        //                FeatureList = new List<string>(),
        //                SignedNonce = LicenseManagerUtil.SignNonce(request.Nonce)
        //            });
        //        }

        //        var (serverLicenses, deviceLicenses) = LicenseManagerUtil.LoadValidLicenses(request.MachineName, request.Ip, request.DbConnection, LicenseFileManager.LoadAllConfigurations());

        //        if (!serverLicenses.Any() && !deviceLicenses.Any())
        //        {
        //            return EncryptAndRespond(HttpStatusCode.Unauthorized, new LicenseResponse
        //            {
        //                Authorized = false,
        //                NextCheckTime = DateTime.MinValue,
        //                FeatureList = new List<string>(),
        //                SignedNonce = LicenseManagerUtil.SignNonce(request.Nonce)
        //            });
        //        }

        //        if (!LicenseManagerUtil.ValidateLicenseType(request, serverLicenses.FirstOrDefault()) ||
        //            !LicenseManagerUtil.ValidateLicensesTime(serverLicenses, deviceLicenses, request))
        //        {
        //            return EncryptAndRespond(HttpStatusCode.Unauthorized, new LicenseResponse
        //            {
        //                Authorized = false,
        //                NextCheckTime = DateTime.UtcNow.AddMinutes(10),
        //                FeatureList = new List<string>(),
        //                SignedNonce = LicenseManagerUtil.SignNonce(request.Nonce)
        //            });
        //        }

        //        var minEndDate = serverLicenses
        //                                .Where(sl => sl.LicenseData.EndDate.HasValue)
        //                                .Select(sl => sl.LicenseData.EndDate.Value)
        //                                .Concat(deviceLicenses
        //                                    .Where(dl => dl.LicenseData.EndDate.HasValue)
        //                                    .Select(dl => dl.LicenseData.EndDate.Value))
        //                                .Min();

        //        DateTime nextCheckTime = LicenseManagerUtil.CalculateNextCheckTime(minEndDate);
        //        var successResponse = new LicenseResponse
        //        {
        //            Authorized = true,
        //            NextCheckTime = nextCheckTime,
        //            FeatureList = LicenseManagerUtil.GetLicenseFeatures(serverLicenses, deviceLicenses),
        //            SignedNonce = LicenseManagerUtil.SignNonce(request.Nonce)
        //        };

        //        return EncryptAndRespond(HttpStatusCode.OK, successResponse);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An error occurred while checking the license.");
        //        return EncryptAndRespond(HttpStatusCode.InternalServerError, new LicenseResponse
        //        {
        //            Authorized = false,
        //            NextCheckTime = DateTime.MinValue,
        //            FeatureList = new List<string>(),
        //            SignedNonce = string.Empty
        //        });
        //    }
        //}






        //[HttpPost("updateconfig")]
        //public IActionResult UpdateLicenseTree([FromBody] UpdateLicenseTreeNodeRequest request)
        //{
        //    if (request == null)
        //    {
        //        return BadRequest("Request cannot be null.");
        //    }

        //    var nodeAsString = request.NodeToUpdate.ToString();

        //    if (request.requetType is requetType.WindowsServer || request.requetType is requetType.WebServer)
        //    {
        //        var licenseTreeServer = JsonConvert.DeserializeObject<LicenseTreeServer>(nodeAsString);
        //        ServerConfigurationData newServerConfigurationData = new ServerConfigurationData
        //        {
        //            LicenseId = licenseTreeServer.Id,
        //            DbConnection = licenseTreeServer.DbConnection,
        //            WinServerDeviceData = licenseTreeServer.WinServerDeviceData,
        //            WebServerDeviceData = licenseTreeServer.WebServerDeviceData,
        //        };
        //        //LicenseFileManager.UpdateServerConfiguration(newServerConfigurationData, request.requetType);
        //    }
        //    else if (request.requetType is requetType.LoadBalancer)
        //    {
        //        var licenseTreeServer = JsonConvert.DeserializeObject<LoadBalance>(nodeAsString);

        //        DeviceInstanceConfigurationData newDeviceInstanceConfigurationData = new DeviceInstanceConfigurationData()
        //        {
        //            Index = licenseTreeServer.Index,
        //            DeviceName = licenseTreeServer.Name,
        //            DeviceIp = licenseTreeServer.IP,
        //        };
        //        LicenseFileManager.UpdateLoadBalancerInstance(request.Id, newDeviceInstanceConfigurationData);
        //    }
        //    else if (request.requetType is requetType.DeviceInstance)
        //    {
        //        var licenseTreeServer = JsonConvert.DeserializeObject<DeviceInstance>(nodeAsString);

        //        DeviceInstanceConfigurationData newDeviceInstanceConfigurationData = new DeviceInstanceConfigurationData()
        //        {
        //            Index = licenseTreeServer.Index,
        //            DeviceName = licenseTreeServer.Name,
        //            DeviceIp = licenseTreeServer.IP,
        //        };
        //        LicenseFileManager.UpdateDeviceInstance(request.Id, newDeviceInstanceConfigurationData);
        //    }
        //    return Ok(request);
        //}

        [HttpGet()]
        [LocalOnly]

        public IActionResult GetAllLicenses()
        {
            var systemRules = LicenseFileManager.LoadSystemRules();
            return Ok(systemRules);
        }
        [HttpGet("getconfig")]
        [LocalOnly]

        public IActionResult GetAllConfigs()
        {
            var configs = LicenseFileManager.LoadAllConfigurations();
            return Ok(configs);
        }

        [HttpDelete("deleteconfig/{licenseId}")]
        public IActionResult DeleteConfig(string licenseId)
        {
            // Load current configurations
            var configs = LicenseFileManager.LoadAllConfigurations();

            if (configs == null)
            {
                return NotFound("No configurations found.");
            }

            configs.ServerList.RemoveAll(s => s.LicenseId == licenseId);
            configs.DeviceList.RemoveAll(d => d.LicenseId == licenseId);

            LicenseFileManager.SaveAllConfigurations(configs);

            return Ok("Configuration deleted successfully.");
        }



        // DELETE: api/sample/{id}
        [HttpDelete("{id}")]
        [LocalOnly]

        public IActionResult DeleteLicense(string id)
        {
            try
            {
                var systemConfig = LicenseFileManager.LoadSystemRules();
                var customer = systemConfig.CustomerList
                    .FirstOrDefault(c => c.LicenseScopeList
                        .Any(ls => ls.ServerLicenseList.Any(l => l.Id == id) || ls.DeviceLicenseList.Any(l => l.Id == id)));

                if (customer != null)
                {
                    var scope = customer.LicenseScopeList
                        .FirstOrDefault(ls => ls.ServerLicenseList.Any(l => l.Id == id) || ls.DeviceLicenseList.Any(l => l.Id == id));

                    if (scope != null)
                    {
                        var serverLicense = scope.ServerLicenseList.FirstOrDefault(l => l.Id == id);
                        if (serverLicense != null)
                        {
                            scope.ServerLicenseList.Remove(serverLicense);
                            LicenseFileManager.SaveSystemRules(systemConfig);
                            return Ok("Server license deleted successfully.");
                        }

                        var deviceLicense = scope.DeviceLicenseList.FirstOrDefault(l => l.Id == id);
                        if (deviceLicense != null)
                        {
                            scope.DeviceLicenseList.Remove(deviceLicense);
                            LicenseFileManager.SaveSystemRules(systemConfig);
                            return Ok("Device license deleted successfully.");
                        }
                    }
                }

                return NotFound("License not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An internal error occurred: " + ex.Message);
            }
        }


        private IActionResult EncryptAndRespond(HttpStatusCode statusCode, LicenseResponse response)
        {
            // Combine status code and LicenseResponse into a single object
            var combinedResponse = new StatusAndResponse()
            {
                StatusCode = (int)statusCode,
                licenseResponse = response
            };

            var responseJson = System.Text.Json.JsonSerializer.Serialize(combinedResponse);

            var (encryptedData, encryptedAESKey, aesIV, signature) = EncryptionHelper.PrepareDataForAPI(responseJson, LicenseManagerUtil.LsPrivateKey, LicenseManagerUtil.BarsaPublicKey);

            var encryptedPayloadResponse = new Payload
            {
                EncryptedData = Convert.ToBase64String(encryptedData),
                EncryptedAESKey = Convert.ToBase64String(encryptedAESKey),
                AesIV = Convert.ToBase64String(aesIV),
                Signature = Convert.ToBase64String(signature)
            };

            return Ok(encryptedPayloadResponse);
        }

        //---------------------todo methods----------------------------------------------

        //[HttpGet]
        //public IActionResult GetAllLicenses()
        //{
        //    var systemRules = LicenseFileManager.LoadSystemRules();

        //    if (systemRules?.CustomerList == null || !systemRules.CustomerList.Any())
        //    {
        //        return NotFound("No licenses found.");
        //    }

        //    var firstCustomer = systemRules.CustomerList.FirstOrDefault();
        //    if (firstCustomer?.LicenseScopeList == null || !firstCustomer.LicenseScopeList.Any())
        //    {
        //        return NotFound("No licenses found for the first customer.");
        //    }

        //    var licenseScope = firstCustomer.LicenseScopeList.FirstOrDefault();

        //    // Map server data to client model
        //    var clientLicenseData = new ClientLicenseData
        //    {
        //        ServerLicenseList = licenseScope.ServerLicenseList.Select(sl => new ClientServerLicense
        //        {
        //            Id = sl.Id,
        //            LicenseName = sl.LicenseName,
        //            ServerType = sl.ServerType,
        //            HasLoadBalancing = sl.HasLoadBalancing,
        //            LoadBalancerServerCount = sl.LoadBalancerServerCount,
        //            HasWindowsHost = sl.LicenseData?.HasWindowsHost ?? false, // Null check for LicenseData
        //            HasWebsite = sl.LicenseData?.HasWebsite ?? false // Null check for LicenseData
        //        }).ToList(),
        //        DeviceLicenseList = licenseScope.DeviceLicenseList.Select(dl => new ClientDeviceLicense
        //        {
        //            Id = dl.Id,
        //            LicenseName = dl.LicenseName,
        //            DeviceCount = dl.DeviceCount,
        //            HasWindowsHost = dl.LicenseData?.HasWindowsHost ?? false, // Null check for LicenseData
        //            HasWebsite = dl.LicenseData?.HasWebsite ?? false // Null check for LicenseData
        //        }).ToList()
        //    };


        //    return Ok(clientLicenseData);
        //}





        // GET: api/sample/{id}
        [HttpGet("{id}")]
        public IActionResult GetLicense(string id)
        {
            try
            {
                var license = LicenseFileManager.LoadSingleLicense(id);
                return Ok(license);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
        // GET: api/sample/configurations
        [HttpGet("configurations")]
        public IActionResult GetAllConfigurations()
        {
            try
            {
                var configurations = LicenseFileManager.LoadAllConfigurations();

                if (configurations == null ||
                    configurations.ServerList == null && configurations.DeviceList == null)
                {
                    // Initialize empty lists if null
                    configurations = new LicenseConfigurationData
                    {
                        ServerList = new List<ServerConfigurationData>(),
                        DeviceList = new List<DeviceConfigurationData>()
                    };
                }

                return Ok(configurations); // Return the configuration data as JSON
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during the loading process
                return StatusCode(500, "An internal error occurred: " + ex.Message);
            }
        }

        // GET: api/sample/configurations
        //[HttpGet("configurations")]
        //public IActionResult GetAllConfigurations()
        //{
        //    try
        //    {
        //        var configurations = LicenseFileManager.LoadAllConfigurations();

        //        //var systemRules = LicenseFileManager.LoadSystemRules();

        //        //var configurations = new List<LicenseConfigurationData>();
        //        //foreach (var customer in systemRules.CustomerList)
        //        //{
        //        //    foreach (var licenseScope in customer.LicenseScopeList)
        //        //    {
        //        //        foreach (var serverLicense in licenseScope.ServerLicenseList)
        //        //        {
        //        //            var config = LicenseFileManager.LoadLicenseConfiguration(serverLicense.Id);
        //        //            if (config != null)
        //        //            {
        //        //                configurations.Add(config);
        //        //            }
        //        //        }

        //        //        foreach (var deviceLicense in licenseScope.DeviceLicenseList)
        //        //        {
        //        //            var config = LicenseFileManager.LoadLicenseConfiguration(deviceLicense.Id);
        //        //            if (config != null)
        //        //            {
        //        //                configurations.Add(config);
        //        //            }
        //        //        }
        //        //    }
        //        //}

        //        return Ok(configurations);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, "An internal error occurred: " + ex.Message);
        //    }
        //}

        // GET: api/sample/{licenseId}/configuration
        [HttpGet("{licenseId}/configuration")]
        public IActionResult GetLicenseConfiguration(string licenseId)
        {
            try
            {
                // Retrieve the license configuration using the license ID
                var config = new LicenseConfigurationData();// LicenseFileManager.LoadLicenseConfiguration(licenseId);

                if (config == null)
                {
                    return NotFound($"No configuration found for license ID: {licenseId}");
                }

                return Ok(config); // Return the configuration data
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An internal error occurred: " + ex.Message);
            }
        }

        [HttpPost("updateallconfigurations")]
        public IActionResult UpdateAllConfigurations([FromBody] List<LicenseConfigurationData> configurations)
        {
            if (configurations == null || !configurations.Any())
            {
                return BadRequest("No configuration data provided.");
            }

            foreach (var configData in configurations)
            {
                configData.ServerList = configData.ServerList ?? new List<ServerConfigurationData>();
                configData.DeviceList = configData.DeviceList ?? new List<DeviceConfigurationData>();

                //LicenseFileManager.SaveLicenseConfiguration(configData);
            }

            return Ok("All configurations updated successfully.");
        }

        [HttpPost("updatesingleconfiguration")]
        public IActionResult UpdateSingleConfiguration([FromBody] object updateDto)
        {
            if (updateDto == null)
            {
                return BadRequest("No configuration data provided.");
            }

            LicenseConfigurationData configData = new LicenseConfigurationData
            {
                ServerList = new List<ServerConfigurationData>(),
                DeviceList = new List<DeviceConfigurationData>()
            };

            if (updateDto is ServerConfigurationData serverConfig)
            {
                configData.ServerList.Add(serverConfig);
            }
            else if (updateDto is DeviceConfigurationData deviceConfig)
            {
                configData.DeviceList.Add(deviceConfig);
            }
            else
            {
                return BadRequest("Invalid configuration data provided.");
            }

            // LicenseFileManager.SaveLicenseConfiguration(configData);

            return Ok("Single configuration updated successfully.");
        }
        //[HttpPost("UpdateLicenseTreeNode")]
        //public IActionResult UpdateLicenseTreeNode([FromBody] UpdateLicenseTreeNodeRequest request)
        //{
        //    // Extract request data
        //    var licenseTreeData = request.LicenseTreeData;
        //    var index = request.Index;
        //    var id = request.Id;
        //    var nodeToUpdate = request.NodeToUpdate;

        //    // Your existing logic to update the node
        //    // Try to find if it's a server license that needs to be updated
        //    var serverToUpdate = licenseTreeData.ServerLicenses?.Find(server => server.Id == id);
        //    if (serverToUpdate != null)
        //    {
        //        if (index == 0 && nodeToUpdate is LicenseTreeServer)
        //        {
        //            // Update the root server node
        //            int serverIndex = licenseTreeData.ServerLicenses.FindIndex(s => s.Id == id);
        //            licenseTreeData.ServerLicenses[serverIndex] = (LicenseTreeServer)nodeToUpdate;
        //        }
        //        else if (index > 0 && nodeToUpdate is LoadBalance)
        //        {
        //            // Update the load balancer device within the server node
        //            int deviceIndex = serverToUpdate.LoadBalance.FindIndex(d => d.index == index);
        //            if (deviceIndex >= 0)
        //            {
        //                serverToUpdate.LoadBalance[deviceIndex] = (LoadBalance)nodeToUpdate;
        //            }
        //        }
        //    }

        //    // Try to find if it's a device license that needs to be updated
        //    var deviceToUpdate = licenseTreeData.DeviceLicenses?.Find(device => device.Id == id);
        //    if (deviceToUpdate != null)
        //    {
        //        if (index == 0 && nodeToUpdate is LicenseTreeDevice)
        //        {
        //            // Update the root device node
        //            int deviceIndex = licenseTreeData.DeviceLicenses.FindIndex(d => d.Id == id);
        //            licenseTreeData.DeviceLicenses[deviceIndex] = (LicenseTreeDevice)nodeToUpdate;
        //        }
        //        else if (index > 0 && nodeToUpdate is DeviceInstance)
        //        {
        //            // Update the device instance within the device node
        //            int instanceIndex = deviceToUpdate.Devices.FindIndex(di => di.index == index);
        //            if (instanceIndex >= 0)
        //            {
        //                deviceToUpdate.Devices[instanceIndex] = (DeviceInstance)nodeToUpdate;
        //            }
        //        }
        //    }

        //    return Ok();
        //}
        [HttpPost("savelicense")]
        public IActionResult SaveLicense([FromBody] JObject licenseEntity)
        {
            var systemConfig = LicenseFileManager.LoadSystemRules();
            if (systemConfig == null || systemConfig.CustomerList == null || !systemConfig.CustomerList.Any())
            {
                return NotFound("System configuration or customers not found.");
            }

            var firstCustomer = systemConfig.CustomerList.FirstOrDefault();
            if (firstCustomer?.LicenseScopeList == null || !firstCustomer.LicenseScopeList.Any())
            {
                return NotFound("License scope not found for the first customer.");
            }

            var licenseScope = firstCustomer.LicenseScopeList.FirstOrDefault();
            if (licenseScope == null)
            {
                return NotFound("License scope not found.");
            }

            // Determine the type of the incoming license entity based on its content
            if (licenseEntity["ServerType"] != null) // Indicates a ServerLicense
            {
                var serverLicense = licenseEntity.ToObject<ServerLicense>();

                var existingServerLicense = licenseScope.ServerLicenseList.FirstOrDefault(sl => sl.Id == serverLicense.Id);
                if (existingServerLicense != null)
                {
                    // Update the existing server license
                    existingServerLicense.LicenseName = serverLicense.LicenseName;
                    existingServerLicense.ServerType = serverLicense.ServerType;
                    existingServerLicense.HasLoadBalancing = serverLicense.HasLoadBalancing;
                    existingServerLicense.LoadBalancerServerCount = serverLicense.LoadBalancerServerCount;
                    existingServerLicense.LicenseData = serverLicense.LicenseData; // Update LicenseData
                }
                else
                {
                    // Add new server license
                    licenseScope.ServerLicenseList.Add(serverLicense);
                }
            }
            else if (licenseEntity["DeviceCount"] != null) // Indicates a DeviceLicense
            {
                var deviceLicense = licenseEntity.ToObject<DeviceLicense>();

                var existingDeviceLicense = licenseScope.DeviceLicenseList.FirstOrDefault(dl => dl.Id == deviceLicense.Id);
                if (existingDeviceLicense != null)
                {
                    // Update the existing device license
                    existingDeviceLicense.LicenseName = deviceLicense.LicenseName;
                    existingDeviceLicense.DeviceCount = deviceLicense.DeviceCount;
                    existingDeviceLicense.LicenseData = deviceLicense.LicenseData; // Update LicenseData
                }
                else
                {
                    // Add new device license
                    licenseScope.DeviceLicenseList.Add(deviceLicense);
                }
            }
            else if (licenseEntity["SomePropertyForLicenseData"] != null) // Indicates LicenseData
            {
                var licenseData = licenseEntity.ToObject<LicenseData>();
                firstCustomer.DefaultLicenseData = licenseData;
            }
            else
            {
                return BadRequest("Unsupported license type.");
            }

            // Save the updated system configuration
            LicenseFileManager.SaveSystemRules(systemConfig);

            return Ok("License saved successfully.");
        }

        // POST: api/sample/updateconfigurations
        //[HttpPost("updateconfigurations")]
        //public IActionResult UpdateLicenseConfiguration([FromBody] LicenseTreeData updatedLicenseData)
        //{
        //    if (updatedLicenseData == null)
        //    {
        //        return BadRequest("Invalid license data.");
        //    }

        //    var systemConfig = LicenseFileManager.LoadSystemRules();
        //    if (systemConfig == null || systemConfig.CustomerList == null || !systemConfig.CustomerList.Any())
        //    {
        //        return NotFound("System configuration or customers not found.");
        //    }

        //    var firstCustomer = systemConfig.CustomerList.FirstOrDefault();
        //    if (firstCustomer?.LicenseScopeList == null || !firstCustomer.LicenseScopeList.Any())
        //    {
        //        return NotFound("License scope not found for the first customer.");
        //    }

        //    var licenseScope = firstCustomer.LicenseScopeList.FirstOrDefault();
        //    if (licenseScope == null)
        //    {
        //        return NotFound("License scope not found.");
        //    }

        //    // Update server licenses
        //    if (updatedLicenseData.ServerLicenses != null && updatedLicenseData.ServerLicenses.Any())
        //    {
        //        foreach (var updatedServerLicense in updatedLicenseData.ServerLicenses)
        //        {
        //            var existingServerLicense = licenseScope.ServerLicenseList.FirstOrDefault(sl => sl.Id == updatedServerLicense.Id);
        //            if (existingServerLicense != null)
        //            {
        //                // Update the existing server license with new values
        //                existingServerLicense.LicenseName = updatedServerLicense.LicenseName;
        //                existingServerLicense.ServerType = (ServerTypeEnum)Enum.Parse(typeof(ServerTypeEnum), updatedServerLicense.ServerType);
        //                existingServerLicense.HasLoadBalancing = updatedServerLicense.HasLoadBalancing;
        //                existingServerLicense.LoadBalancerServerCount = updatedServerLicense.LoadBalancerServerCount;

        //                // Update the configuration data for servers
        //                var serverConfig = LicenseFileManager.LoadAllConfigurations().ServerList?.FirstOrDefault(s => s.LicenseId == existingServerLicense.Id);
        //                if (serverConfig != null)
        //                {
        //                    serverConfig.WebServerDeviceName = updatedServerLicense.WebServerDeviceName;
        //                    serverConfig.WebServerDeviceIp = updatedServerLicense.WebServerDeviceIp;
        //                    serverConfig.WinServerDeviceName = updatedServerLicense.WinServerDeviceName;
        //                    serverConfig.WinServerDeviceIp = updatedServerLicense.WinServerDeviceIp;

        //                    // Update LoadBalancer devices
        //                    serverConfig.WebServerLoadBalanceDeviceList = updatedServerLicense.LoadBalance.Select(ld => new DeviceInstanceConfigurationData
        //                    {
        //                        DeviceName = ld.Name,
        //                        DeviceIp = ld.IP
        //                    }).ToList();
        //                }
        //            }
        //        }
        //    }

        //    // Update device licenses
        //    if (updatedLicenseData.DeviceLicenses != null && updatedLicenseData.DeviceLicenses.Any())
        //    {
        //        foreach (var updatedDeviceLicense in updatedLicenseData.DeviceLicenses)
        //        {
        //            var existingDeviceLicense = licenseScope.DeviceLicenseList.FirstOrDefault(dl => dl.Id == updatedDeviceLicense.Id);
        //            if (existingDeviceLicense != null)
        //            {
        //                // Update the existing device license with new values
        //                existingDeviceLicense.LicenseName = updatedDeviceLicense.LicenseName;
        //                existingDeviceLicense.DeviceCount = updatedDeviceLicense.DeviceCount;

        //                // Update the configuration data for devices
        //                var deviceConfig = LicenseFileManager.LoadAllConfigurations().DeviceList?.FirstOrDefault(d => d.LicenseId == existingDeviceLicense.Id);
        //                if (deviceConfig != null)
        //                {
        //                    deviceConfig.InstanceList = updatedDeviceLicense.Devices.Select(di => new DeviceInstanceConfigurationData
        //                    {
        //                        DeviceName = di.Name,
        //                        DeviceIp = di.IP
        //                    }).ToList();
        //                }
        //            }
        //        }
        //    }

        //    // Save the updated system configuration
        //    LicenseFileManager.SaveSystemRules(systemConfig);

        //    return Ok("License configuration updated successfully.");
        //}
        private void UpdateOrAddLicense(LicenseRuleData systemRules, LicenseBase newLicense)
        {
            if (systemRules == null)
            {
                systemRules = new LicenseRuleData
                {
                    CustomerList = new List<Customer>()
                };
            }

            if (!systemRules.CustomerList.Any())
            {
                var newCustomer = new Customer
                {
                    Name = "",
                    LicenseScopeList = new List<LicenseScope>()
                };

                systemRules.CustomerList.Add(newCustomer);
            }

            var customer = systemRules.CustomerList.First();

            if (customer.LicenseScopeList == null || !customer.LicenseScopeList.Any())
            {
                customer.LicenseScopeList = new List<LicenseScope>
        {
            new LicenseScope
            {
                Id = "",
                Name= "",
                ServerLicenseList = new List<ServerLicense>(),
                DeviceLicenseList = new List<DeviceLicense>()
            }
        };
            }

            var scope = customer.LicenseScopeList.First();

            if (newLicense is ServerLicense serverLicense)
            {
                if (scope.ServerLicenseList == null)
                {
                    scope.ServerLicenseList = new List<ServerLicense>();
                }

                var existingLicense = scope.ServerLicenseList.FirstOrDefault(l => l.Id == newLicense.Id);
                if (existingLicense != null)
                {
                    scope.ServerLicenseList.Remove(existingLicense);
                }

                scope.ServerLicenseList.Add(serverLicense);
            }
            else if (newLicense is DeviceLicense deviceLicense)
            {
                if (scope.DeviceLicenseList == null)
                {
                    scope.DeviceLicenseList = new List<DeviceLicense>();
                }

                var existingLicense = scope.DeviceLicenseList.FirstOrDefault(l => l.Id == newLicense.Id);
                if (existingLicense != null)
                {
                    scope.DeviceLicenseList.Remove(existingLicense);
                }

                scope.DeviceLicenseList.Add(deviceLicense);
            }
        }
    }
}