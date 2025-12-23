namespace Argon;

internal partial class TbsWrapper
{
    public partial class NativeMethods
    {
        [LibraryImport("tbs.dll")]
        internal static partial TBS_RESULT
        Tbsi_Context_Create(
            ref TBS_CONTEXT_PARAMS ContextParams,
            ref UIntPtr Context);

        [LibraryImport("tbs.dll")]
        internal static partial TBS_RESULT
        Tbsip_Context_Close(
            UIntPtr Context);

        [LibraryImport("tbs.dll")]
        internal static partial TBS_RESULT
            Tbsi_Get_OwnerAuth(
            UIntPtr Context,
            TBS_OWNERAUTH_TYPE OwnerAuthType,
            [System.Runtime.InteropServices.MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), In, Out]
             byte[] OutBuffer,
            ref uint OutBufferSize);
    }

    public enum TBS_RESULT : uint
    {
        TBS_SUCCESS = 0,
        TBS_E_BLOCKED = 0x80280400,
        TBS_E_INTERNAL_ERROR = 0x80284001,
        TBS_E_BAD_PARAMETER = 0x80284002,
        TBS_E_INSUFFICIENT_BUFFER = 0x80284005,
        TBS_E_COMMAND_CANCELED = 0x8028400D,
        TBS_E_OWNERAUTH_NOT_FOUND = 0x80284015
    }

    public enum TBS_OWNERAUTH_TYPE : uint
    {
        TBS_OWNERAUTH_TYPE_FULL = 1,
        TBS_OWNERAUTH_TYPE_ADMIN = 2,
        TBS_OWNERAUTH_TYPE_USER = 3,
        TBS_OWNERAUTH_TYPE_ENDORSEMENT = 4,
        TBS_OWNERAUTH_TYPE_ENDORSEMENT_20 = 12,
        TBS_OWNERAUTH_TYPE_STORAGE_20 = 13
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TBS_CONTEXT_PARAMS
    {
        public TBS_CONTEXT_VERSION Version;
        public TBS_CONTEXT_CREATE_FLAGS Flags;
    }

    public enum TBS_CONTEXT_VERSION : uint
    {
        ONE = 1,
        TWO = 2
    }

    public enum TBS_CONTEXT_CREATE_FLAGS : uint
    {
        RequestRaw = 0x00000001,
        IncludeTpm12 = 0x00000002,
        IncludeTpm20 = 0x00000004,
    }
} // class TbsWrapper
