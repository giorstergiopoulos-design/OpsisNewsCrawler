using System.Security.Cryptography;
using System.Text;

namespace OpsisNewsCrawler.Windows;

/// <summary>Keeps a secret (the user's own AI key) in the settings file encrypted for the current Windows account (DPAPI), never as plain text.</summary>
internal static class SecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OpsisNewsCrawler.AiKey");

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(data);
    }

    /// <summary>The secret, or an empty string when there is none or it cannot be read on this account.</summary>
    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return "";
        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException) { return ""; }
    }
}
