using Argon;
using Argon.Create;
using System.Text;

string _licensePayload = @"{
    ""English"":""Hello world"",
    ""Anglo -Saxon"": ""ᚠᛇᚻ᛫ᛒᛦᚦ᛫ᚠᚱᚩᚠᚢᚱ᛫ᚠᛁᚱᚪ᛫ᚷᛖᚻᚹᛦᛚᚳᚢᛗ"",
    ""Icelandic"": ""Ég get etið gler án þess að meiða mig.""
}";

Console.WriteLine($"Argon test utility {Tools.GetProductVersion<Program>()}.");


GetPublicKeyResult getPublicKeyResult = Tools.GetPublicKey();
if (getPublicKeyResult.Code != GetPublicKeyResultCode.Created || getPublicKeyResult.Key == null)
{
    Console.WriteLine(getPublicKeyResult.Message);
    return;
}
byte[] creationKey = CreationTools.GetSymmetricKeyMaterial(getPublicKeyResult.Key);
byte[] decodeKey = Tools.GetSymmetricKeyMaterial();
if (ByteArraysEqual(creationKey, decodeKey))
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Symmetric keys match.");

    //create license
    byte[] payloadToEncode = Encoding.Unicode.GetBytes(_licensePayload);
    string licenseFilePath = Path.GetTempFileName();
    CreationTools.GenerateLicenseResult generateLicense = CreationTools.GenerateLicense(payloadToEncode, creationKey, licenseFilePath);
    if (generateLicense.Code != CreationTools.GenerateLicenseResultCode.Written)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("License was not generated.");
        Console.WriteLine(generateLicense.Details);
        return;
    }
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("License was generated.");
    //decode license
    GetLicenseResult getLicenseResult = Tools.GetLicense(decodeKey, licenseFilePath);
    if (getLicenseResult.Payload == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("License could not be decoded.");
        Console.WriteLine(generateLicense.Details);
        return;
    }
    else
    {
        string payloadString = Encoding.Unicode.GetString(getLicenseResult.Payload);
        if (payloadString == _licensePayload)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Decoded payload matched.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Decoded payload did not match.");
            Console.WriteLine(Convert.ToHexString(payloadToEncode));
            Console.WriteLine(Convert.ToHexString(getLicenseResult.Payload));
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Decoded:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(payloadString); Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Original:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(_licensePayload);
        }
    }
    //clean up
    File.Delete(licenseFilePath);
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Symmetric keys don't match.");
    Console.WriteLine(Convert.ToHexString(creationKey));
    Console.WriteLine(Convert.ToHexString(decodeKey));
}
static bool ByteArraysEqual(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
    => a1.SequenceEqual(a2);