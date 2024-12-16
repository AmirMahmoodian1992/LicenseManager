using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using TotpActivation;

using System;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

public class ActivationState
{
    private readonly ILogger<ActivationState> _logger;

    public ActivationState(ILogger<ActivationState> logger)
    {
        _logger = logger;
    }

    public string HardwareInfo { get; set; }
    public string GUID { get; set; }
    public bool IsActivated { get; set; }
    public static ActivationState _cachedState { get; set; }
    private static readonly string DefaultFilePath = "ActivationData";

    public ActivationState LoadFromFile(string filePath = null)
    {
        filePath ??= DefaultFilePath;

        try
        {
            if (_cachedState != null)
            {
                return _cachedState;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Activation data file not found at path: {filePath}", filePath);
                return new ActivationState(_logger) { IsActivated = false };
            }

            var encryptedData = File.ReadAllText(filePath);

            if (string.IsNullOrWhiteSpace(encryptedData))
            {
                _logger.LogWarning("Activation data file is empty at path: {filePath}", filePath);
                return new ActivationState(_logger) { IsActivated = false };
            }

            try
            {
                var decryptedData = LicenseFileManager.Decrypt(encryptedData);
                _cachedState = JsonConvert.DeserializeObject<ActivationState>(decryptedData);

                if (_cachedState == null)
                {
                    throw new JsonException("Deserialized activation state is null.");
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to decrypt or deserialize activation data file at path: {filePath}", filePath);
                return new ActivationState(_logger) { IsActivated = false };
            }

            return _cachedState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading activation state from file.");
            return new ActivationState(_logger) { IsActivated = false };
        }
    }


    public void SaveToFile(string filePath = null)
    {
        filePath ??= DefaultFilePath;

        try
        {
            var jsonData = JsonConvert.SerializeObject(this);
            var encryptedData = LicenseFileManager.Encrypt(jsonData);
            File.WriteAllText(filePath, encryptedData);
            _cachedState = this;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving activation state to file");
        }
    }
}

public class ActivationManager
{
    private readonly ILogger<ActivationManager> _logger;
    private readonly ActivationState _activationState;

    public ActivationManager(ILogger<ActivationManager> logger, ActivationState activationState)
    {
        _logger = logger;
        _activationState = activationState;
    }

    public ActivationResult FirstTimeActivation(string activationCodeFromBarsa, string secretKey, string guid, string filePath = "ActivationData")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(activationCodeFromBarsa))
            {
                _logger.LogWarning("Activation code is required but missing.");
                return new ActivationResult { StatusCode = 4001, Message = "Activation code is required.", IsActivated = false };
            }

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogWarning("Secret key is required but missing.");
                return new ActivationResult { StatusCode = 4002, Message = "Secret key is required.", IsActivated = false };
            }

            if (string.IsNullOrWhiteSpace(guid))
            {
                _logger.LogWarning("GUID is required but missing.");
                return new ActivationResult { StatusCode = 4003, Message = "GUID is required.", IsActivated = false };
            }

            if (!TotpManager.ValidateTotpWithDelay(activationCodeFromBarsa, secretKey))
            {
                _logger.LogWarning("Invalid activation code provided.");
                return new ActivationResult { StatusCode = 4004, Message = "Invalid activation code.", IsActivated = false };
            }

            var hardwareInfo = HardwareInfo.GetHardwareInfo();
            if (string.IsNullOrEmpty(hardwareInfo))
            {
                _logger.LogWarning("Unable to retrieve hardware information.");
                return new ActivationResult { StatusCode = 4005, Message = "Unable to retrieve hardware information.", IsActivated = false };
            }

            _activationState.HardwareInfo = hardwareInfo;
            _activationState.GUID = guid;
            _activationState.IsActivated = true;
            _activationState.SaveToFile(filePath);

            _logger.LogInformation("Activation successful.");
            return new ActivationResult { StatusCode = 2000, Message = "Activation successful.", IsActivated = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Activation failed due to an unexpected error.");
            return new ActivationResult { StatusCode = 5001, Message = "Activation failed due to an unexpected error: " + ex.Message, IsActivated = false };
        }
    }

    public ActivationResult Reactivation(string activationCodeFromBarsa, string secretKey, string guid, string filePath = "ActivationData")
    {
        try
        {
            var activationState = _activationState.LoadFromFile(filePath);

            var currentHardwareInfo = HardwareInfo.GetHardwareInfo();
            if (string.IsNullOrEmpty(currentHardwareInfo))
            {
                _logger.LogWarning("Unable to retrieve current hardware information.");
                return new ActivationResult { StatusCode = 4005, Message = "Unable to retrieve current hardware information.", IsActivated = false };
            }

            var savedHardwareInfo = activationState.HardwareInfo?.Split('|');
            var currentHardwareParts = currentHardwareInfo.Split('|');

            if (savedHardwareInfo == null || savedHardwareInfo.Length != currentHardwareParts.Length)
            {
                _logger.LogWarning("Invalid hardware information in the saved activation data.");
                return new ActivationResult { StatusCode = 4008, Message = "Invalid saved hardware information.", IsActivated = false };
            }

            bool hardwareMatch = false;
            for (int i = 0; i < savedHardwareInfo.Length; i++)
            {
                if (savedHardwareInfo[i] == currentHardwareParts[i])
                {
                    hardwareMatch = true;
                    break;
                }
            }

            if (!hardwareMatch)
            {
                activationState.IsActivated = false;
                activationState.SaveToFile(filePath);
                return new ActivationResult { StatusCode = 4006, Message = "Hardware mismatch detected. System deactivated.", IsActivated = false };
            }

            if (!TotpManager.ValidateTotpWithDelay(activationCodeFromBarsa, secretKey))
            {
                _logger.LogWarning("Invalid reactivation code provided.");
                return new ActivationResult { StatusCode = 4007, Message = "Invalid reactivation code.", IsActivated = false };
            }

            activationState.IsActivated = true;
            activationState.SaveToFile(filePath);

            _logger.LogInformation("Reactivation successful.");
            return new ActivationResult { StatusCode = 2001, Message = "Reactivation successful.", IsActivated = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reactivation failed due to an unexpected error.");
            return new ActivationResult { StatusCode = 5002, Message = "Reactivation failed due to an unexpected error: " + ex.Message, IsActivated = false };
        }
    }


    public ActivationResult Deactivate(string filePath = "ActivationData")
    {
        try
        {
            var activationState = _activationState.LoadFromFile(filePath);

            if (!activationState.IsActivated)
            {
                return new ActivationResult { StatusCode = 2002, Message = "System is already deactivated.", IsActivated = false };
            }

            activationState.IsActivated = false;
            activationState.SaveToFile(filePath);

            ActivationState._cachedState = null;

            _logger.LogInformation("Deactivation successful.");
            return new ActivationResult { StatusCode = 2003, Message = "Deactivation successful.", IsActivated = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deactivation failed due to an unexpected error.");
            return new ActivationResult { StatusCode = 5000, Message = "Deactivation failed due to an unexpected error: " + ex.Message, IsActivated = true };
        }
    }
}

