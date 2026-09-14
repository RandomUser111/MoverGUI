using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace MoverGUI;

internal static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    public static string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var input = ToBlob(bytes);
        try
        {
            if (!CryptProtectData(ref input, "MoverGUI password", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN, out var output))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var protectedBytes = new byte[output.cbData];
                Marshal.Copy(output.pbData, protectedBytes, 0, output.cbData);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
            }
        }
        finally
        {
            FreeBlob(input);
        }
    }

    public static string Unprotect(string protectedValue)
    {
        var bytes = Convert.FromBase64String(protectedValue);
        var input = ToBlob(bytes);
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN, out var output))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var plainBytes = new byte[output.cbData];
                Marshal.Copy(output.pbData, plainBytes, 0, output.cbData);
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
            }
        }
        finally
        {
            FreeBlob(input);
        }
    }

    private static DATA_BLOB ToBlob(byte[] data)
    {
        var blob = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
        Marshal.Copy(data, 0, blob.pbData, data.Length);
        return blob;
    }

    private static void FreeBlob(DATA_BLOB blob)
    {
        if (blob.pbData != IntPtr.Zero)
            Marshal.FreeHGlobal(blob.pbData);
    }
}
