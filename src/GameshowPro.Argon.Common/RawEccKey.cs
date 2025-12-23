namespace Argon;

internal class RawEccKey
{
    internal struct EccInfo
    {
        internal uint Magic;
        internal bool Public;
        internal int KeyLength;
        internal bool Ecdh;
    }

    internal static EccInfo[] AlgInfo;

    private static readonly string?[] EcdsaCurveIDs;

    private static readonly string?[] EcdhCurveIDs;

    internal static int GetKeyLength(EccCurve curve)
    {
        switch (curve)
        {
            case EccCurve.TpmEccNistP256:
                return 256;
            case EccCurve.TpmEccNistP384:
                return 384;
            case EccCurve.TpmEccNistP521:
                return 521;
            default:
                Globs.Throw<ArgumentException>("GetKeyLength(): Invalid ECC curve");
                return -1;
        }
    }

    internal static uint MagicFromTpmAlgId(TpmAlgId algId, bool isEcdh, EccCurve curve, bool publicKey)
    {
        uint magic = AlgInfo.FirstOrDefault((EccInfo x) => x.Public == publicKey && x.KeyLength == GetKeyLength(curve) && x.Ecdh == isEcdh)!.Magic;
        if (magic == 0)
        {
            Globs.Throw("Unrecognized ECC parameter set");
        }

        return magic;
    }

    internal static byte[] GetKeyBlob(byte[] x, byte[] y, TpmAlgId alg, bool isEcdh, EccCurve curve)
    {
        //See RFC5114 and https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-gkdi/24876a37-9a92-4187-9052-222bb6f85d4a
        Marshaller marshaller = new();
        byte[] magic = BitConverter.GetBytes(MagicFromTpmAlgId(alg, isEcdh, curve, publicKey: true));
        marshaller.Put(magic, ""); //little-endian
        int num = (GetKeyLength(curve) + 7) / 8;
        if (x.Length != num || y.Length != num)
        {
            Globs.Throw<ArgumentException>("GetKeyBlob: Malformed ECC key");
            return [];
        }

        byte[] o = Globs.ReverseByteOrder(Globs.HostToNet(num));
        marshaller.Put(o, "len"); //little-endian
        marshaller.Put(x, "x"); //big-endian
        marshaller.Put(y, "y"); //big-endian
        return marshaller.GetBytes();
    }

    internal static byte[] GetCompressedKeyBlob(byte[] x, byte[] y)
    {
        //See https://stackoverflow.com/questions/17171542/algorithm-for-elliptic-curve-point-compression
        //Only works if curve's p mod 4 === 3 as is the case for NIST P-256 (secp256r1), NIST P-384(secp384r1), NIST P-521(secp521r1)
        if (y.Length != 32)
        {
            throw new ArgumentException("Expected 32 bytes", nameof(y));
        }
        byte yMsb = BitConverter.IsLittleEndian ? y.Last() : y[0];
        byte[] compressed = new byte[x.Length + 1];
        Buffer.BlockCopy(x, 0, compressed, 0, x.Length);
        compressed[x.Length] = (byte)(yMsb > 127 ? 1 : 0); // sign bit is most significant bit of most significant byte
        return compressed;
    }

    internal static void KeyInfoFromPublicBlob(byte[] blob, out byte[]? x, out byte[]? y)
    {
        x = null;
        y = null;
        Marshaller marshaller = new(blob);
        uint magic = BitConverter.ToUInt32(marshaller.GetNBytes(4), 0);
        if (!AlgInfo.Any((EccInfo xx) => xx.Magic == magic))
        {
            Globs.Throw<ArgumentException>("KeyInfoFromPublicBlob: Public key blob magic not recognized");
        }

        uint n = BitConverter.ToUInt32(marshaller.GetNBytes(4), 0);
        x = marshaller.GetNBytes((int)n);
        y = marshaller.GetNBytes((int)n);
    }

    internal static bool IsCurveSupported(EccCurve curve)
    {
        if ((int)curve < EcdsaCurveIDs.Length && EcdsaCurveIDs[(uint)curve] != null)
        {
            return true;
        }

        return false;
    }

    internal static string? GetEccAlg(TpmPublic pub)
    {
        if (pub.unique.GetUnionSelector() != TpmAlgId.Ecc)
        {
            return null;
        }

        EccParms eccParms = (EccParms)pub.parameters;
        bool flag = pub.objectAttributes.HasFlag(ObjectAttr.Sign);
        bool flag2 = pub.objectAttributes.HasFlag(ObjectAttr.Decrypt);
        if (!(flag ^ flag2))
        {
            Globs.Throw<ArgumentException>("ECC Key must either sign or decrypt");
            return null;
        }

        TpmAlgId unionSelector = eccParms.scheme.GetUnionSelector();
        if (flag && unionSelector != TpmAlgId.Ecdsa && unionSelector != TpmAlgId.Null)
        {
            Globs.Throw<ArgumentException>("Unsupported ECC signing scheme");
            return null;
        }

        if (!IsCurveSupported(eccParms.curveID))
        {
            Globs.Throw<ArgumentException>("Unsupported ECC curve");
            return null;
        }

        int curveID = (int)eccParms.curveID;
        if (!flag)
        {
            return EcdhCurveIDs[curveID];
        }

        return EcdsaCurveIDs[curveID];
    }

    static RawEccKey()
    {
        EccInfo[] array = new EccInfo[12];
        EccInfo eccInfo = new()
        {
            Magic = 827016005u,
            KeyLength = 256,
            Ecdh = true,
            Public = true
        };
        array[0] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 843793221u,
            KeyLength = 256,
            Ecdh = true,
            Public = false
        };
        array[1] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 860570437u,
            KeyLength = 384,
            Ecdh = true,
            Public = true
        };
        array[2] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 877347653u,
            KeyLength = 384,
            Ecdh = true,
            Public = false
        };
        array[3] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 894124869u,
            KeyLength = 521,
            Ecdh = true,
            Public = true
        };
        array[4] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 910902085u,
            KeyLength = 521,
            Ecdh = true,
            Public = false
        };
        array[5] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 827540293u,
            KeyLength = 256,
            Ecdh = false,
            Public = true
        };
        array[6] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 844317509u,
            KeyLength = 256,
            Ecdh = false,
            Public = false
        };
        array[7] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 861094725u,
            KeyLength = 384,
            Ecdh = false,
            Public = true
        };
        array[8] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 877871941u,
            KeyLength = 384,
            Ecdh = false,
            Public = false
        };
        array[9] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 894649157u,
            KeyLength = 521,
            Ecdh = false,
            Public = true
        };
        array[10] = eccInfo;
        eccInfo = new EccInfo
        {
            Magic = 911426373u,
            KeyLength = 521,
            Ecdh = false,
            Public = false
        };
        array[11] = eccInfo;
        AlgInfo = array;
        EcdsaCurveIDs = [null, null, null, "ECDSA_P256", "ECDSA_P384", "ECDSA_P521"];
        EcdhCurveIDs = [null, null, null, "ECDH_P256", "ECDH_P384", "ECDH_P521"];
    }
}
