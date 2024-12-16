using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace TotpActivation
{
    public class TotpManager
    {

        public static int GenerateRandomNineDigitNumber(bool isReactivated = false)
        {
            try
            {
                byte[] randomBytes = new byte[4];
                RandomNumberGenerator.Fill(randomBytes);

                int randomInt = Math.Abs(BitConverter.ToInt32(randomBytes, 0));
                int randomNineDigitNumber = (randomInt % 900_000_000) + 100_000_000;

                GenerateSecretKey(randomNineDigitNumber, isReactivated);

                return randomNineDigitNumber;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error generating nine-digit number.", ex);
            }
        }

        public static string GenerateSecretKey(int randomNineDigitNumber, bool isReactivated)
        {
            try
            {
                var licenseScopeId = LicenseFileManager
                    .LoadSystemRules()
                    .CustomerList
                    .FirstOrDefault()?
                    .LicenseScopeList
                    .FirstOrDefault()?
                    .Id;

                if (licenseScopeId == null)
                {
                    throw new InvalidOperationException("License Scope ID not found.");
                }

                string concatenatedKey = $"{licenseScopeId}{randomNineDigitNumber}" + (isReactivated ? "Reactivation" : "Activation");
                string encodedKey = Base32Encode(Encoding.UTF8.GetBytes(concatenatedKey));

                SaveSecretKey(encodedKey);

                return encodedKey;
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Failed to generate the secret key due to an invalid License Scope ID.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An unexpected error occurred while generating the secret key.", ex);
            }
        }


        private static void SaveSecretKey(string secretKey)
        {
            try
            {
                File.WriteAllText("SecretKey.txt", secretKey);
            }
            catch (IOException ex)
            {
                throw new IOException("Failed to save the secret key to file.", ex);
            }
        }


        public static string GenerateTotp(string secretKey, long timeStep = 30, int digits = 6)
        {
            byte[] key = Base32Decode(secretKey);
            long currentTime = GetUnixTime() / timeStep;

            return GenerateOtp(key, currentTime, digits);
        }

        public static bool ValidateTotp(string providedOtp, string secretKey, int timeDrift = 240, long timeStep = 30, int digits = 6)
        {
            byte[] key = Base32Decode(secretKey);
            long currentTime = GetUnixTime() / timeStep;

            for (int i = -timeDrift; i <= timeDrift; i++)
            {
                string validOtp = GenerateOtp(key, currentTime + i, digits);
                if (providedOtp == validOtp)
                {
                    return true;
                }
            }

            return false;
        }
        public static bool ValidateTotpWithDelay(string providedOtp, string secretKey, int timeDrift = 240, long timeStep = 30, int digits = 6, int delayMilliseconds = 100)
        {
            bool isValid = ValidateTotp(providedOtp, secretKey, timeDrift, timeStep, digits);

            if (!isValid)
            {
                Thread.Sleep(delayMilliseconds);
            }

            return isValid;
        }



        private static string GenerateOtp(byte[] key, long counter, int digits)
        {
            byte[] counterBytes = BitConverter.GetBytes(counter);
            Array.Reverse(counterBytes);

            using (HMACSHA1 hmac = new HMACSHA1(key))
            {
                byte[] hash = hmac.ComputeHash(counterBytes);

                int offset = hash[hash.Length - 1] & 0xf;
                int binaryCode = (hash[offset] & 0x7f) << 24
                                 | (hash[offset + 1] & 0xff) << 16
                                 | (hash[offset + 2] & 0xff) << 8
                                 | (hash[offset + 3] & 0xff);

                int otp = binaryCode % (int)Math.Pow(10, digits);
                return otp.ToString(new string('0', digits));
            }
        }

        private static long GetUnixTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            StringBuilder result = new StringBuilder((data.Length + 7) * 8 / 5);

            int index = 0, digit = 0;
            int currentByte = 0, nextByte;

            while (index < data.Length)
            {
                if (digit > 3)
                {
                    if (index + 1 < data.Length)
                    {
                        nextByte = data[index + 1];
                    }
                    else
                    {
                        nextByte = 0;
                    }

                    currentByte = data[index];
                    result.Append(alphabet[(currentByte & (0xFF >> digit)) << (digit - 3) | (nextByte >> (8 - (digit - 3)))]);
                    index++;
                    digit = (digit + 5) % 8;
                }
                else
                {
                    currentByte = data[index];
                    result.Append(alphabet[(currentByte >> (3 - digit)) & 0x1F]);
                    digit += 5;
                }
            }

            return result.ToString();
        }

        private static byte[] Base32Decode(string base32String)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            byte[] bytes = new byte[base32String.Length * 5 / 8];
            int index = 0, bitBuffer = 0, bitsInBuffer = 0;

            foreach (char c in base32String)
            {
                int charIndex = alphabet.IndexOf(c);
                if (charIndex == -1)
                {
                    throw new ArgumentException("Invalid Base32 character.", nameof(base32String));
                }

                bitBuffer = (bitBuffer << 5) | charIndex;
                bitsInBuffer += 5;

                if (bitsInBuffer >= 8)
                {
                    bytes[index++] = (byte)(bitBuffer >> (bitsInBuffer - 8));
                    bitsInBuffer -= 8;
                }
            }

            return bytes;
        }

    }
}
