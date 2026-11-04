using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Checks the Authenticode signature of a file with the Windows API itself (WinVerifyTrust): no script engine is started. Used before a downloaded
/// installer of a dependency (WebView2) is run: the signature must be valid and the signer must be named by the caller.
/// </summary>
internal static class Authenticode
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustFileInfo { public uint cbStruct; public IntPtr FilePath; public IntPtr hFile; public IntPtr KnownSubject; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint cbStruct; public IntPtr PolicyCallbackData; public IntPtr SipClientData; public uint UiChoice; public uint RevocationChecks;
        public uint UnionChoice; public IntPtr File; public uint StateAction; public IntPtr StateData; public IntPtr UrlReference;
        public uint ProvFlags; public uint UiContext; public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, IntPtr data);

    /// <summary>The subject of the signer when the signature of the file is valid; null otherwise.</summary>
    public static string? ValidSignerSubject(string file)
    {
        IntPtr path = IntPtr.Zero, info = IntPtr.Zero, data = IntPtr.Zero;
        try
        {
            path = Marshal.StringToHGlobalUni(file);
            info = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
            Marshal.StructureToPtr(new WintrustFileInfo { cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(), FilePath = path }, info, false);
            data = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustData>());
            Marshal.StructureToPtr(new WintrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustData>(), UiChoice = 2 /* none */, RevocationChecks = 0, UnionChoice = 1 /* file */, File = info, StateAction = 0
            }, data, false);
            if (WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, data) != 0) return null;
#pragma warning disable SYSLIB0057   // no replacement exists for reading the signer of a signed file
            using var cert = X509Certificate.CreateFromSignedFile(file);
#pragma warning restore SYSLIB0057
            return cert.Subject;
        }
        catch (Exception ex) when (ex is CryptographicException or DllNotFoundException or EntryPointNotFoundException or IOException)
        {
            return null;
        }
        finally
        {
            if (data != IntPtr.Zero) Marshal.FreeHGlobal(data);
            if (info != IntPtr.Zero) Marshal.FreeHGlobal(info);
            if (path != IntPtr.Zero) Marshal.FreeHGlobal(path);
        }
    }
}
