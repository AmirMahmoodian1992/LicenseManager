using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using TotpActivation;

public class ActivationState
{
    public string HardwareInfo { get; set; }
    public string GUID { get; set; }
    public bool IsActivated { get; set; }

    public static ActivationState _cachedState { get; set; }

    private static readonly string DefaultFilePath = "ActivationData";

    public static ActivationState LoadFromFile(string filePath = null)
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
                Console.WriteLine("Activation data file not found.");
                return new ActivationState { IsActivated = false };
            }

            var encryptedData = File.ReadAllText(filePath);
            var decryptedData = LicenseFileManager.Decrypt(encryptedData);
            _cachedState = JsonConvert.DeserializeObject<ActivationState>(decryptedData);

            return _cachedState;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading activation state from file: {ex.Message}");
            return new ActivationState { IsActivated = false };
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
            Console.WriteLine($"Error saving activation state to file: {ex.Message}");
        }
    }
}

public static class ActivationManager
{fdgdfggdfg
    public static ActivationResult FirstTimeActivation(string activationCodeFromBarsa, string secretKey, string guid, string filePath = "ActivationData")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(activationCodeFromBarsa))
            {
                return new ActivationResult { StatusCode = 4001, Message = "Activation code is required.", IsActivated = false };
            }

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                return new ActivationResult { StatusCode = 4002, Message = "Secret key is required.", IsActivated = false };
            }

            if (string.IsNullOrWhiteSpace(guid))
            {
                return new ActivationResult { StatusCode = 4003, Message = "GUID is required.", IsActivated = false };
            }

            if (!TotpManager.ValidateTotp(activationCodeFromBarsa, secretKey))
            {
                return new ActivationResult { StatusCode = 4004, Message = "Invalid activation code.", IsActivated = false };
            }

            var hardwareInfo = HardwareInfo.GetHardwareInfo();
            if (string.IsNullOrEmpty(hardwareInfo))
            {
                return new ActivationResult { StatusCode = 4005, Message = "Unable to retrieve hardware information.", IsActivated = false };
            }

            var activationState = new ActivationState
            {
                HardwareInfo = hardwareInfo,
                GUID = guid,
                IsActivated = true
            };

            activationState.SaveToFile(filePath);

            return new ActivationResult { StatusCode = 2000, Message = "Activation successful.", IsActivated = true };
        }
        catch (Exception ex)
        {
            return new ActivationResult { StatusCode = 5001, Message = "Activation failed due to an unexpected error: " + ex.Message, IsActivated = false };
        }
    }

    public static ActivationResult Reactivation(string activationCodeFromBarsa, string secretKey, string guid, string filePath = "ActivationData")
    {
        try
        {
            var activationState = ActivationState.LoadFromFile(filePath);

            var currentHardwareInfo = HardwareInfo.GetHardwareInfo();
            if (string.IsNullOrEmpty(currentHardwareInfo))
            {
                return new ActivationResult { StatusCode = 4005, Message = "Unable to retrieve current hardware information.", IsActivated = false };
            }

            if (activationState.HardwareInfo != currentHardwareInfo || activationState.GUID != guid)
            {
                activationState.IsActivated = false;
                activationState.SaveToFile(filePath);
                return new ActivationResult { StatusCode = 4006, Message = "Hardware mismatch detected. System deactivated.", IsActivated = false };
            }

            if (!TotpManager.ValidateTotp(activationCodeFromBarsa, secretKey))
            {
                return new ActivationResult { StatusCode = 4007, Message = "Invalid reactivation code.", IsActivated = false };
            }

            activationState.IsActivated = true;
            activationState.SaveToFile(filePath);

            return new ActivationResult { StatusCode = 2001, Message = "Reactivation successful.", IsActivated = true };
        }
        catch (Exception ex)
        {
            return new ActivationResult { StatusCode = 5002, Message = "Reactivation failed due to an unexpected error: " + ex.Message, IsActivated = false };
        }
    }
    public static ActivationResult Deactivate(string filePath = "ActivationData")
    {
        try
        {
            var activationState = ActivationState.LoadFromFile(filePath);

            if (!activationState.IsActivated)
            {
                return new ActivationResult { StatusCode = 2002, Message = "System is already deactivated.", IsActivated = false };
            }

            activationState.IsActivated = false;
            activationState.SaveToFile(filePath);

            ActivationState._cachedState = null;

            return new ActivationResult { StatusCode = 2003, Message = "Deactivation successful.", IsActivated = false };
        }
        catch (Exception ex)
        {
            return new ActivationResult { StatusCode = 5000, Message = "Deactivation failed due to an unexpected error: " + ex.Message, IsActivated = true };
        }
    }

}


