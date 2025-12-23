using System.IO.Compression;

namespace Argon.Create;

public static class CreationTools
{
    public enum CreateKeyPairResultCode
    {
        Exception,
        Written
    }

    public record CreateKeyPairResult(CreateKeyPairResultCode Code, string? Details);

    public enum GenerateLicenseResultCode
    {
        Exception,
        Written
    }

    public record MachineProfile(string? Name, DateTime? Time, byte[]? Key);

    public record GenerateLicenseResult(GenerateLicenseResultCode Code, string? Details);

    public record WorkingFile(string FullPath, string Leaf);
    internal static CreateKeyPairResult CreateKeyPair(string? precalculatedDir)
    {
        string workingDir = precalculatedDir ?? GetWorkingDir();
        StringBuilder details = new();
        EnsureWorkingDirResult result = EnsureWorkingDir(precalculatedDir);
        details.Append(result.Message); 
        CreateKeyPairResultCode? resultCode = null;
        if (result.Success)
        {
            CngKey key = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256, null, new CngKeyCreationParameters() 
            {
                ExportPolicy = CngExportPolicies.AllowPlaintextExport,
                KeyCreationOptions = CngKeyCreationOptions.MachineKey,
                KeyUsage = CngKeyUsages.KeyAgreement,
                Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                UIPolicy = new CngUIPolicy(CngUIProtectionLevels.None)
            });
            byte[] privateBytes = key.Export(CngKeyBlobFormat.EccPrivateBlob);
            byte[] publicBytes = key.Export(CngKeyBlobFormat.EccPublicBlob);
            byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
            if (BytesToCsFile("Private.cs", "s_private",  privateBytes, null))
            {
                if (BytesToCsFile("Public.cs", "s_public", publicBytes, randomBytes))
                {
                    resultCode = CreateKeyPairResultCode.Written;
                }
            }
        }
        return new(resultCode ?? CreateKeyPairResultCode.Exception, details.ToString());
        bool BytesToCsFile(string filename, string variableName, byte[] data, byte[]? noise)
        {
            string fullPath = Path.Combine(workingDir, filename);
            try
            {
                File.Delete(fullPath);
            }
            catch { }
            try
            {
                using StreamWriter outputFile = new(fullPath, false, Encoding.UTF8);
                outputFile.WriteLine("//***This file should not be checked in to source control!***");
                outputFile.Write("namespace Argon;\r\ninternal static partial class Keys\r\n{");
                WriteByteArray(outputFile, variableName, data);
                if (noise != null)
                {
                    WriteByteArray(outputFile, "s_noise", noise);
                }
                outputFile.WriteLine("}");
                details.AppendLine($"Wrote {fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                details.AppendLine($"Failed to write {fullPath}: {ex.Message}");
                return false;
            }
        }

        void WriteByteArray(StreamWriter outputFile, string name, byte[] data)
        {
            outputFile.WriteLine("    internal static readonly byte[] " + name + " = ");
            outputFile.WriteLine("    {");
            outputFile.WriteLine("        " + string.Join(", ", Enumerable.Range(0, data.Length).Select(i => "0x" + Convert.ToHexString(data, i, 1))));
            outputFile.WriteLine("    };");
        }
    }

    internal static string GetPrivateHashString()
    {
        int result = 0;
        unchecked
        {
            foreach (byte b in Keys.s_private)
            {
                result = (result * 31) ^ b;
            }
        }
        return Convert.ToBase64String(BitConverter.GetBytes(result));
    }

    /// <summary>
    /// Generate a license file from a payload file and a machine profile. License file path will have the same relationship to licenseRoot as the machine profile has to machineProfileRoot.
    /// </summary>
    /// <param name="payloadFile">The content of the license file.</param>
    /// <param name="machineProfilePath">Absolute path to the profile of the destination machine.</param>
    /// <param name="machineProfileRoot">Absolute path to the root directory of all profiles. Should be some subset from the left of machineProfile</param>
    /// <param name="licenseRoot">Absolute path to the root directory container all license files.</param>
    internal static GenerateLicenseResult GenerateLicense(string payloadFile, string machineProfilePath, string machineProfileRoot, string licenseRoot)
    {
        string licenseFilename = Path.GetFileNameWithoutExtension(machineProfilePath) + ".license";
        string? machineProfileDirectory = Path.GetDirectoryName(machineProfilePath);
        if (machineProfileDirectory == null)
        {
            return new(GenerateLicenseResultCode.Exception, "Machine profile directory not found");
        }
        //calculate the relative path from the machine profile to the machine profile root
        string commonRelativePath = Path.GetRelativePath(machineProfileRoot, machineProfileDirectory);
        //calculate the license file path
        string licenseFilePath = Path.Combine(licenseRoot, commonRelativePath, licenseFilename);
        return GenerateLicense(payloadFile, machineProfilePath, licenseFilePath);
    }

    internal static GenerateLicenseResult GenerateLicense(string payloadFile, string machineProfilePath, string licenseFilePath)
    {
        StringBuilder details = new();
        EnsureWorkingDirResult result = EnsureWorkingDir(Path.GetDirectoryName(licenseFilePath));
        details.AppendLine(result.Message);
        GenerateLicenseResultCode? resultCode = null;
        if (result.Success)
        {
            MachineProfile profile = LoadMachineProfile(machineProfilePath);
            if (profile.Key == null)
            {
                details.AppendLine("Key not found in machine profile");
            }
            else
            {
                byte[] payload = File.ReadAllBytes(payloadFile); //Presumes a short payload... could use a stream for bigger payloads
                byte[] compressed = Compress(payload);
                byte[] symmetricKey = GetSymmetricKeyMaterial(profile.Key);

                GenerateLicenseResult generateResult = GenerateLicense(compressed, symmetricKey, licenseFilePath);
                details.AppendLine(generateResult.Details);
                resultCode = generateResult.Code;
            }
        }
        return new(resultCode ?? GenerateLicenseResultCode.Exception, details.ToString());
    }

    public static byte[] Compress(byte[] uncompressed)
    {
        using var stream = new MemoryStream();
        using (var compress = new DeflateStream(stream, CompressionLevel.Optimal, true))
        {
            compress.Write(uncompressed, 0, uncompressed.Length);
        }
        return stream.ToArray();
    }

    public static GenerateLicenseResult GenerateLicense(byte[] payload, byte[] symmetricKey, string licenseFilePath)
    {
        StringBuilder details = new();
        EnsureWorkingDirResult result = EnsureWorkingDir(Path.GetDirectoryName(licenseFilePath));
        details.AppendLine(result.Message);
        GenerateLicenseResultCode? resultCode = null;
        if (result.Success)
        {    
            using Aes aes = Aes.Create();
            aes.Key = symmetricKey;
            aes.IV = Constants.s_iv;
            aes.Padding = PaddingMode.ISO10126;
            aes.Mode = CipherMode.CBC;
            using FileStream outFile = new(licenseFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using CryptoStream cs = new(outFile, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using MemoryStream input = new(payload);
            input.CopyTo(cs);
            cs.FlushFinalBlock();
            details.AppendLine($"Wrote to {licenseFilePath}");
            resultCode = GenerateLicenseResultCode.Written;
        }
        return new(resultCode ?? GenerateLicenseResultCode.Exception, details.ToString());
    }

    internal static MachineProfile LoadMachineProfile(string filePath)
    {
        IEnumerable<string> lines = File.ReadLines(filePath);
        string? name = null;
        DateTime? time = null;
        byte[]? key = null;
        foreach (string? line in lines)
        {
            if (line == null || line.Length < 3) continue;
            switch (line[..2])
            {
                case "M:":
                    name ??= line[2..];
                    break;
                case "T:":
                    if (time is null && DateTime.TryParseExact(line[..2], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                    {
                        time = parsed;
                    }
                    break;
                case "U:":
                    key ??= Convert.FromBase64String(line[2..]);
                    break;
            }
        }
        return new MachineProfile(name, time, key);
    }

    public static byte[] GetSymmetricKeyMaterial(byte[] clientPublicKey)
    {
        using ECDiffieHellmanCng creator = new(CngKey.Import(Keys.s_private, CngKeyBlobFormat.EccPrivateBlob, CngProvider.MicrosoftSoftwareKeyStorageProvider));
        creator.HashAlgorithm = CngAlgorithm.Sha256;
        creator.SecretPrepend = Constants.s_kdfPrepend;
        creator.SecretAppend = Constants.s_kdfAppend;
        creator.HmacKey = Constants.s_hmacKey; //Otherwise DeriveKeyMaterial will set UseSecretAsHmacKey to true
        creator.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hmac;
        CngKey client = CngKey.Import(clientPublicKey, CngKeyBlobFormat.EccPublicBlob);
        return creator.DeriveKeyMaterial(client);
    }

    internal static ImmutableList<WorkingFile> GetWorkingFiles(string? precalculatedDir)
    {
        string workingDir = precalculatedDir ?? GetWorkingDir(); 
        return [.. Directory
            .GetFiles(workingDir, "*.*", SearchOption.AllDirectories)
            .Select(f => new WorkingFile(f, Path.GetRelativePath(workingDir, f)))];
    }
        
}
