using System.IO.Compression;

namespace Argon;

public enum GetPublicKeyResultCode
{
    NotSupported,
    NotValid,
    Created
}

public enum InitializeResultCode
{
    AuthProblem,
    ClockProblem,
    OK
}

public record GetPublicKeyResult(GetPublicKeyResultCode Code, byte[]? Key, string? Message);
public record InitializeResult(InitializeResultCode Code, string? Message);
public record CheckLicenseResult(bool IsOk, string Details);
public record GetLicenseResult(byte[]? Payload, string Details);

public static class Tools
{
    public static readonly string s_workingDirectoryEnvironmentVariable = "ArgonWorkingDir";

    private readonly static byte[] s_auth = [0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xBE, 0xEF];
    private static void GetTpmKey(Tpm2 tpm, out TpmHandle? hClientKey, out TpmPublic? publicKey)
    {
        SensitiveCreate inSensitive = new(s_auth, null); //The authorization key, which allows us to access the key later

        TpmPublic inPublic = new(
            TpmAlgId.Sha256,
            ObjectAttr.Decrypt | ObjectAttr.UserWithAuth | ObjectAttr.SensitiveDataOrigin,
            null,
            new EccParms(new SymDefObject(), new SchemeEcdh(TpmAlgId.Sha256), EccCurve.TpmEccNistP256, new NullKdfScheme()),
            new EccPoint());

        //PcrSelection creationPcr = new (TpmAlgId.Sha1, new uint[] { 0, 1, 2 }); //Select the Platform Configuration Registers to be used for randomization
        tpm.OwnerAuth = new(s_auth);
        try
        {
            hClientKey = tpm.CreatePrimary(
                TpmRh.Owner,
                inSensitive,
                inPublic,
                null,
                [],
                out publicKey,
                out _,
                out _,
                out _
            );
        }
        catch {
            hClientKey = null;
            publicKey = null;
        }
    }

    public static GetPublicKeyResult GetPublicKey()
    {
        StringBuilder stringBuilder = new();
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);
        GetTpmKey(tpm, out TpmHandle? hClientKey, out TpmPublic? publicKey);
        if (hClientKey == null || publicKey == null)
        {
            return new(GetPublicKeyResultCode.NotValid, null, "Could not get key. Does Argon control TPM?");
        }
        var pubParams = (EccParms)publicKey.parameters;
        var eccPub = (EccPoint)publicKey.unique;
        bool isEcdsa = pubParams.scheme.GetUnionSelector() == TpmAlgId.Ecdsa;
        byte[] keyBlob = RawEccKey.GetKeyBlob(eccPub.x, eccPub.y, TpmAlgId.Ecc, !isEcdsa, pubParams.curveID);
        //byte[] compressedBlob = RawEccKey.GetCompressedKeyBlob(eccPub.x, eccPub.y);
        
        TimeSpan clockDelta = ClockDelta(tpm, DateTimeOffset.UtcNow);
        tpm.FlushContext(hClientKey);

        stringBuilder.AppendLine("Machine:" + Environment.MachineName);
        stringBuilder.AppendLine("ClockError:" + clockDelta.ToString());
        stringBuilder.AppendLine("Time:" + DateTimeOffset.UtcNow.ToString("O"));
        stringBuilder.AppendLine("KeyHex:" + Convert.ToHexString(keyBlob));
        stringBuilder.AppendLine("Key:" + Convert.ToBase64String(keyBlob));
        //stringBuilder.AppendLine("CompHex:" + Convert.ToHexString(compressedBlob));
        //stringBuilder.AppendLine("Comp:" + Convert.ToBase64String(compressedBlob));

        return new(GetPublicKeyResultCode.Created, keyBlob, stringBuilder.ToString());
    }

    public static GetPublicKeyResult WritePublicKey(string? precalculatedDir)
    {
        string workingDir = precalculatedDir ?? GetWorkingDir();
        EnsureWorkingDirResult ensureResult = EnsureWorkingDir(workingDir);
        if (!ensureResult.Success)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ensureResult.Message);
            return new(GetPublicKeyResultCode.NotSupported, null, ensureResult.Message);
        }
        GetPublicKeyResult getPublicKeyResult = GetPublicKey();
        if (getPublicKeyResult.Code == GetPublicKeyResultCode.Created && getPublicKeyResult.Key != null)
        {
            string profilePath = Path.Combine(workingDir, Environment.MachineName + ".profile");
            using StreamWriter outputFile = new(profilePath, Encoding.UTF8, new FileStreamOptions() { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None });
            outputFile.WriteLine("M:{0}", Environment.MachineName);
            outputFile.WriteLine("T:{0}", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            outputFile.WriteLine("U:{0}", Convert.ToBase64String(getPublicKeyResult.Key));
            outputFile.Flush();
            outputFile.Close();
            return new(GetPublicKeyResultCode.Created, getPublicKeyResult.Key,  $"Written to {profilePath}");
        }
        else
        {
            return getPublicKeyResult;
        }
    }

    private static bool IsAdmin()
    {
        if (OperatingSystem.IsWindows())
        {

            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
        }
        return false;
    }

    public static InitializeResult CheckControl()
    {
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);
        try
        {
            GetTpmKey(tpm, out TpmHandle? hClientKey, out TpmPublic? publicKey);
            if (hClientKey == null || publicKey == null)
            {
                return new(InitializeResultCode.AuthProblem, "Could not get key. Does Argon control TPM?");
            }
            TimeSpan delta = ClockDelta(tpm, DateTimeOffset.UtcNow);
            bool clockIsValid = delta > TimeSpan.FromDays(-1);
            string deltaString;
            if (delta.TotalDays > 1 || delta.TotalDays < -1)
            {
                deltaString = Math.Abs(delta.TotalDays).ToString("0.00") + " days.";
            }
            else if (delta.TotalHours > 1 || delta.TotalHours < -1)
            {
                deltaString = Math.Abs(delta.TotalHours).ToString("0.00") + " hours.";
            }
            else
            {
                deltaString = Math.Abs(delta.TotalMinutes).ToString("0.00") + " minutes.";
            }
            return new(
                clockIsValid ? InitializeResultCode.OK : InitializeResultCode.ClockProblem
                , "Argon is in control of the TPM " + 
                (delta > TimeSpan.Zero ? "and system clock is ahead by " : "but the system clock has been moved back by ") +
                deltaString);
        }
        catch
        {
            return new(InitializeResultCode.AuthProblem, "Argon does not control the TPM.");
        }
    }
    public static InitializeResult ReleaseControl()
    {
        if (!IsAdmin())
        {
            return new(InitializeResultCode.AuthProblem, "This feature requires admin.");
        }
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);
        byte[] newAuth = [];
        try
        {
            tpm.HierarchyChangeAuth(TpmHandle.RhOwner, newAuth);
            return new(InitializeResultCode.OK, "Control of the TPM was already released.");
        }
        catch (TpmException e)
        {
            if (e.ErrorString == "BadAuth")
            {
                tpm.OwnerAuth = new(s_auth);
                try
                {
                    tpm.HierarchyChangeAuth(TpmHandle.RhOwner, newAuth);
                    return new(InitializeResultCode.OK, "Argon released control of TPM.");
                }
                catch (TpmException)
                {
                    return new(InitializeResultCode.AuthProblem, "Argon did not have control of the TPM, so could not release it.");
                }
                catch (Exception e3)
                {
                    return new(InitializeResultCode.AuthProblem, $"Unexpected exception while releasing TPM: {e3.Message}");
                }
            }
            else
            {
                return new(InitializeResultCode.AuthProblem, $"Unexpected exception while checking control of TPM: {e.Message}");
            }
        }
        catch (Exception e)
        {
            return new(InitializeResultCode.AuthProblem, $"Unexpected exception while checking control of TPM: {e.Message}");
        }
    }

    public static InitializeResult ConfirmInitialization()
    {
        if (!IsAdmin())
        {
            return new(InitializeResultCode.AuthProblem, "This feature requires admin.");
        }
        StringBuilder stringBuilder = new();
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);

        InitializeResultCode? resultCode = null;
        if (EnsureAuth())
        {
            CheckAndRachetTimeResult checkAndRachetTimeResult = CheckAndRachetTime(tpm);
            stringBuilder.AppendLine(checkAndRachetTimeResult.Message);
            resultCode = checkAndRachetTimeResult.DateTimeOffset.HasValue ? InitializeResultCode.OK : InitializeResultCode.ClockProblem;
        }
        else
        {
            resultCode = InitializeResultCode.AuthProblem;
        }
        return new(resultCode ?? InitializeResultCode.OK, stringBuilder.ToString());
        bool EnsureAuth()
        {
            try
            {
                tpm.HierarchyChangeAuth(TpmHandle.RhOwner, s_auth);
                stringBuilder.AppendLine("Argon took control of TPM.");
                return true;
            }
            catch (TpmException e)
            {
                if (e.ErrorString == "BadAuth")
                {
                    tpm.OwnerAuth = new(s_auth);
                    try
                    {
                        tpm.HierarchyChangeAuth(TpmHandle.RhOwner, s_auth);
                        stringBuilder.AppendLine("Argon already had control of TPM."); 
                        return true;
                    }
                    catch (TpmException)
                    {
                        stringBuilder.AppendLine("Argon failed to take control of the TPM. Try clearing the TPM through Windows Security Processor settings.");
                    }
                    catch (Exception e3)
                    {
                        stringBuilder.AppendLine("Unexpected error: " + e3.Message);
                    }
                }
                else
                {
                    stringBuilder.AppendLine("Unexpected error: " + e.Message);
                }
            }
            catch (Exception e)
            {
                stringBuilder.AppendLine("Unexpected error: " + e.Message);
            }
            return false;
        }
    }

    /// <summary>
    /// If return value is positive, the duration represents how far behind the TPM was. If negative, the TPM is ahead, which is an exception state.
    /// </summary>
    private static TimeSpan ClockDelta(Tpm2 tpm, DateTimeOffset now)
    {
        var timeNonce = new byte[] { 0xa, 0x9, 0x8, 0x7 }; 
        tpm.OwnerAuth = new(s_auth);
        var timeQuote = tpm.GetTime(TpmHandle.RhEndorsement, TpmHandle.RhNull, timeNonce, new NullSignature(), out ISignatureUnion sig);
        DateTimeOffset quoteTime = DateTimeOffset.FromUnixTimeMilliseconds((long)timeQuote.clockInfo.clock);
        //TimeInfo? time = tpm.ReadClock();
        //DateTimeOffset tpmTime = DateTimeOffset.FromUnixTimeMilliseconds((long)time.clockInfo.clock);
        TimeSpan timeDelta = now - quoteTime;
        return timeDelta;
    }

    private static DateTimeOffset? SetTime(Tpm2 tpm, out string message)
    {
        try
        {
            tpm.OwnerAuth = new(s_auth);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            tpm.ClockSet(TpmHandle.RhOwner, (ulong)now.ToUnixTimeMilliseconds());
            message = "Clock adjusted";
            return now;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return null;
        }
    }

    public record CheckAndRachetTimeResult(DateTimeOffset? DateTimeOffset, TimeSpan? Delta, string Message);
    public static CheckAndRachetTimeResult CheckAndRachetTime()
    {
        if (!IsAdmin())
        {
            return new(null, null, "This feature requires admin.");
        }
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);
        return CheckAndRachetTime(tpm);
    }

    private static CheckAndRachetTimeResult CheckAndRachetTime(Tpm2 tpm)
    {
        if (!IsAdmin())
        {
            return new(null, null, "This feature requires admin.");
        }
        CheckAndRachetTimeResult checkTimeResult = CheckTime(tpm);

        DateTimeOffset? setTime = SetTime(tpm, out string message);
        if (setTime.HasValue)
        {
            return new(setTime, checkTimeResult.Delta, $"Clock updated.");
        }
        if (checkTimeResult.Delta <= TimeSpan.Zero && checkTimeResult.DateTimeOffset.HasValue)
        {
            return new(checkTimeResult.DateTimeOffset.Value, checkTimeResult.Delta, "Clock is running ahead, so it couldn't be set back");
        }
        return new(null, checkTimeResult.Delta, "Clock running behind and couldn't be updated: " + message);

    }

    public static CheckAndRachetTimeResult CheckTime()
    {
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);
        return CheckTime(tpm);
    }

    /// <summary>
    /// Return the current DateTimeOffset, as long as it's close enough to the TPM time.
    /// </summary>
    /// <param name="tpm"></param>
    /// <returns></returns>
    private static CheckAndRachetTimeResult CheckTime(Tpm2 tpm)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan delta = ClockDelta(tpm, now);
        if (delta.TotalDays > 1)
        {
            return new(null, delta, "TPM clock is too far behind");
        }
        else if (delta.TotalDays < -2)
        {
            return new(null, delta, "System clock is too far behind");
        }
        else
        {
            return new(now, delta, "System clock is valid");
        }
    }

    public static CheckLicenseResult CheckLicense(string licensePath)
    {
        GetLicenseResult result = GetLicense(licensePath);
        if (result.Payload == null)
        {
            return new(false, result.Details);
        }
        else
        {
            try
            {
                _ = JsonDocument.Parse(result.Payload);
            }
            catch
            {
                return new(false, "Failed to parse license payload as JSON");
            }
            return new(true, "License contained valid JSON");
        }
    }

    public static GetLicenseResult GetLicense(string licenseFilePath)
    {
        byte[] symmetricKey = GetSymmetricKeyMaterial();
        return GetLicense(symmetricKey, licenseFilePath);
    }

    public static GetLicenseResult GetLicense(byte[] symmetricKey, string licenseFilePath) 
    {
        if (symmetricKey.Length < 32)
        {
            return new(null, "Key too short");
        }
        try
        {
            using Aes aes = Aes.Create();
            aes.Key = symmetricKey;
            aes.IV = Constants.s_iv;
            aes.Padding = PaddingMode.ISO10126;
            aes.Mode = CipherMode.CBC;
            using FileStream inFile = new(licenseFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using CryptoStream cs = new(inFile, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using MemoryStream output = new();
            inFile.Flush();
            cs.CopyTo(output);
            byte[]? decompressed = Decompress(output.ToArray());
            if (decompressed == null)
            {
                return new(null, "Could not decompress license");
            }
            return new(decompressed, "OK");
        }
        catch
        {
            return new(null, "Could not decrypt license");
        }
    }

    private static byte[]? Decompress(byte[] compressed)
    {
        try
        {
            byte[] decompressed = new byte[compressed.Length * 10];
            int decompLength;
            using var stream = new MemoryStream(compressed);
            using var decompress = new DeflateStream(stream, CompressionMode.Decompress, true);
            decompLength = decompress.Read(decompressed, 0, decompressed.Length);
            byte[] shortened = new byte[decompLength];
            Buffer.BlockCopy(decompressed, 0, shortened, 0, decompLength);
            return shortened;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Derive a symmetric key using the creator's public key and the private key from this machine's TPM.
    /// </summary>
    public static byte[] GetSymmetricKeyMaterial()
    {
        using Tpm2Device device = new TbsDevice();
        device.Connect();
        using Tpm2 tpm = new(device);
        GetTpmKey(tpm, out TpmHandle? hClientKey, out TpmPublic? publicKey);
        if (hClientKey == null || publicKey == null)
        {
            return [];
        }
        EccPoint creatorPublicKey = CreatorPublicKey();
        EccPoint zPoint = tpm.EcdhZGen(hClientKey, creatorPublicKey);
        return DeriveKey(zPoint);
    }

    /// <summary>
    /// Return the public key of the creator as an <see cref="EccPoint"/>
    /// </summary>
    private static EccPoint CreatorPublicKey()
    {
        RawEccKey.KeyInfoFromPublicBlob(Keys.s_public, out byte[]? x, out byte[]? y);
        Debug.Assert(x != null && y != null);
        return new (x, y);
    }

    /// <summary>
    /// Apply and Key Derivation Function to a Z point.
    /// </summary>
    private static byte[] DeriveKey(EccPoint zPoint)
    {

        byte[] secretBytes = Combine(Constants.s_kdfPrepend, zPoint.x, Constants.s_kdfAppend);
        return HMACSHA256.HashData(Constants.s_hmacKey, secretBytes);
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        byte[] rv = new byte[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            Buffer.BlockCopy(array, 0, rv, offset, array.Length);
            offset += array.Length;
        }
        return rv;
    }

    public static string GetWorkingDir()
    {
        string? path = Environment.GetEnvironmentVariable(s_workingDirectoryEnvironmentVariable, EnvironmentVariableTarget.User);
        if (!Directory.Exists(path))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Argon");
        }
        return path;
    }

    public static bool SetWorkingDir(string? path)
    {
        if (Directory.Exists(path))
        {
            Environment.SetEnvironmentVariable(s_workingDirectoryEnvironmentVariable, path, EnvironmentVariableTarget.User);
            return true;
        }
        return false;
    }

    public record EnsureWorkingDirResult(bool Success, string? Message);
    public static EnsureWorkingDirResult EnsureWorkingDir(string? precalculatedDir)
    {
        string workingDirectory = precalculatedDir ?? GetWorkingDir();
        if (Directory.Exists(workingDirectory))
        {
            return new(true, $"Using working directory {workingDirectory}");
        }
        try
        {
            Directory.CreateDirectory(workingDirectory);
            return new(true, $"Created working directory {workingDirectory}");
        }
        catch (Exception e)
        {
            return new(false, $"Exception creating working directory {workingDirectory}: {e.Message}");
        }
    }

    public static void DoWorkingDirChangeUI()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Enter new path to working directory, or leave empty to keep {GetWorkingDir()}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (SetWorkingDir(Console.ReadLine()))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Working directory was set to {GetWorkingDir()}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Working directory was kept as {GetWorkingDir()}");
        }
    }

    public static string GetProductVersion<T>()
    {
        string assemblyPath = typeof(T).Assembly.Location;
        return FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion ?? "unknown";
    }
}
