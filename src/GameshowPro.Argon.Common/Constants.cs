namespace Argon;

public static class Constants
{
    public static readonly byte[] s_kdfPrepend = Keys.s_noise[0..4];
    public static readonly byte[] s_kdfAppend = Keys.s_noise[4..11];
    public static readonly byte[] s_hmacKey = Keys.s_noise[11..16];
    public static readonly byte[] s_iv = Keys.s_noise[16..32];
}
