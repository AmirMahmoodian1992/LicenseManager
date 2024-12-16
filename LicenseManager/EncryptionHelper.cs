using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LicenseManager.Controllers;



public class EncryptionHelper
{
    public static string barsaPublicxml = @"<RSAKeyValue><Modulus>s/fXlhYVH+bwfT6xj480s9drrwogWuLjVRj7kGTjN5RGSeUgEs4m5Whx9a/PKSptO9tKM+F/1dmoY9JCSceSb2scQOOPbuXtNjHPd0sTBmMser85XXaTJ56ViRzPvmGfH4ZDBS51QFHyC1GvDcs80RkzkZgbkxYkGRJpc3Lb71qrhOtOYUqb/7zysqMsxaSWUmj5fCZYct1HPIxKeFvndoFvjZhugftb/7bVUzcLGHGyz5/tzz6mds0sbYUH9u04Bbl1vPpQ+2pqCTfhWhYYRorGgd5O+oFdAy8XDySr27KrCMpOJ1yEJiWCwoECHDw6s0Ir+EuDmva79wZXWMINLQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    public static string lsPrivatexml = @"<RSAKeyValue><Modulus>yAbX1QoU6WnQe4TobUacrEHYWUBPvb/weac/wkcFN8fe0vON+hLcZTiDkRpZ0bJRKNM7bUx6IlWWoUWeNbSuW4q1NpvK6xknfLroSAaDDhGpgcgQ01LKHcGzszieWOF7tJG737YkkTOnQLmMCRLNFB1bAjwEQyxWa3npeLpXiKOukVozHU1jjleTyglnT5W1saQzH6T67w2JPAxI8rEG6MdW3TDfTQVsFOgHAH92g4mc6ZmRtToiolwWQtWceC3DJUgIUKpJcoTirOXL+PV17/ZaCRYOMeehTe9gB6UGsZ0xzAP503Gc9AlnHNuDgkU8ZgrOmiXV9TGrJB+LAVJgJQ==</Modulus><Exponent>AQAB</Exponent><P>3Ma/1F3XOBQOMjWvL9eU4XzOAG+H8AbNMSA3mGk2ka4LEUz4l0B18rvTYsKbACGkp+Q1RwtNXxxPCSjN/EZH8WcR2rdxiNNruIt95gcSgYx/qroiEqj/im3UFOOLtn4eboErTlXUmp/4uFo+CRDl6Ectu/804efb5Zq1p48Ru2c=</P><Q>5/Cb2kRRLLEDoyGYSjSkj6uAdJCSCiNHbVxw1T8cPMWZ8/FQZCR16xCzGR2+cUz35rnVYajkpuejthUDFOo0s9HQm3MgKJZ4is+Cz5OLDYUdD4iYmojMLL2dtaUTqFOrQBW7q6x234dC0bbpAeLD51WQJoHTg7Uh33jR7j7nnJM=</Q><DP>Uv0WwMJhkz/esjsB1k8INNaQLRO/mpdD7HJ603zBOXOdz2wKifh+HbdC133le+Apn76l1EXIWLcwcnX3MBxPEMw4pumL8O3gMSemNKB18WKZ3thG1JLYM/Xi4dNDAl9YGxvM5o5W86SsfsfVR90lPvH6nA9rlntsaluEay7ZcIM=</DP><DQ>v+RMPWfaNPx6wuOPiI1HPOoqS8Y0XRjVBoC9hWBCb3EYrz+OQFv+By34zyXRoxGH5CcJiFPgYMoyovl9ZDdkxQUo7wNvrsTXFBkc47nxCI2B/pEHmIrnSXjTWy4pNlGK1GlmrGDytHrG6JTI8Ft5sxISQhmVMlmQnb/rB46Ztus=</DQ><InverseQ>vZ/riimdYsJqmRYm6ZruN3NBhe6WfmINzwrY5lH0Dg8PWjB7xjd2yb4WVe6N6ltz3Ypd5/gGRZoLozbk7yP+VIwMigcDplmszhErJ8SOqQKuftnnf5P2z38DlRyFYBo4XDIv8L/nxJ2Zau9i9Wk1/SeCkU/VFtJeHbpawW5Cfmc=</InverseQ><D>fIX+EWtAT3sHRg4coALIgFhRdmaZ/deivilHgQuzjOFJr2veJCNfv0fqaAfOiMQI0HCH22gz1HIR3v43GtoLfYOhgoiET82ODpFRD522Mqj+LIQ/LT+qAdJXq6gAs/ZTi7r6CbAbnaVZZurb6b4hm3cW9BIm2Sad+jSgCv9+hNm/em1/TiZe0zAu+sQNj9S9+f096ecgKH+JrHJezzJO1WTzCQj1kcVvFVJcSTxCB5/4OUOTfIfvQRL1EHOevZfeuA8XZ84Bv8U2ycXjIgJ5AE0tMVAr1+SI4sXprnT36FiFoygVfb/PBE7YYHQOZUEO/VZ82LnB+bb9b7EZ/chcVQ==</D></RSAKeyValue>";

    public static (byte[] EncryptedData, byte[] AESKey, byte[] AESIV, byte[] Signature) PrepareDataForAPI(string jsonData, string privateKeyPath, string publicKeyPath)
    {
        byte[] signature = SignData(jsonData, privateKeyPath);

        string dataToEncrypt = jsonData;

        var (encryptedData, aesKey, aesIV) = EncryptDataWithAES(dataToEncrypt);

        byte[] encryptedAESKey = EncryptAESKeyWithRSA(aesKey, publicKeyPath);

        return (encryptedData, encryptedAESKey, aesIV, signature);
    }

    public static byte[] SignData(string data, string privateKeyPath)
    {
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        using (var rsa = new RSACryptoServiceProvider())
        {
            string privateKeyXml = lsPrivatexml; // File.ReadAllText(privateKeyPath);
            rsa.FromXmlString(privateKeyXml);

            return rsa.SignData(dataBytes, new SHA256CryptoServiceProvider());
        }
    }

    public static (byte[] EncryptedData, byte[] AESKey, byte[] AESIV) EncryptDataWithAES(string data)
    {
        using (AesManaged aes = new AesManaged())
        {
            aes.GenerateKey();
            aes.GenerateIV();

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(data);
                    }
                }

                return (msEncrypt.ToArray(), aes.Key, aes.IV);
            }
        }
    }

    public static byte[] EncryptAESKeyWithRSA(byte[] aesKey, string publicKeyPath)
    {
        using (var rsa = new RSACryptoServiceProvider())
        {
            string publicKeyXml = barsaPublicxml; //File.ReadAllText(publicKeyPath);
            rsa.FromXmlString(publicKeyXml);

            return rsa.Encrypt(aesKey, false); // false = PKCS1 padding
        }
    }
    public static string GenerateNonce()
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] nonceBytes = new byte[16];
            rng.GetBytes(nonceBytes);
            return Convert.ToBase64String(nonceBytes);
        }
    }
}