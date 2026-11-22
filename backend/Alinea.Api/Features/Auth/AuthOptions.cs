using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Alinea.Api.Features.Auth;

public sealed class AuthOptions
{
    public string OtpKey { get; set; } = "";
    public bool AllowInsecureLocalHttp { get; set; }
    public string DataProtectionPath { get; set; } = "";
    public string[] KnownProxies { get; set; } = [];
    public static bool IsValid(AuthOptions options) => options.OtpKey.Length >= 64
        && options.OtpKey.Length % 2 == 0
        && options.OtpKey.All(Uri.IsHexDigit)
        && options.KnownProxies.All(p => IPAddress.TryParse(p, out var ip) && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any));
}

public sealed class SmsOptions
{
    public string Provider { get; set; } = "Disabled";
    public string DevelopmentInboxKey { get; set; } = "";
}

public sealed class AuthCrypto(AuthOptions options)
{
    private readonly byte[] key = Convert.FromHexString(options.OtpKey);
    public string Hash(string purpose, string value) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(purpose + ":" + value)));
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static bool Equal(string? a, string? b) => a is not null && b is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    public static string? NormalizePhone(string? value)
    {
        if (value is null || value.Length > 40) return null;
        var normalized = Regex.Replace(value, @"[ ()\-]", "");
        return Regex.IsMatch(normalized, @"^\+[1-9][0-9]{7,14}$", RegexOptions.CultureInvariant) ? normalized : null;
    }
}
