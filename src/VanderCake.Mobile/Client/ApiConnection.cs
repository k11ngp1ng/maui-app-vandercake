using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace VanderCake.Mobile.Client;

public static class ApiConnection
{
    public static Uri Validate(string address)
    {
        if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ClientFailure("Enter an HTTPS server origin, for example https://localhost:7080 (no path or credentials).");
        return uri;
    }
    public static bool MatchesLocalPin(Uri? request, X509Certificate2? certificate, string pin, DateTimeOffset now)
    {
        var normalized = pin.Replace(":", "").Replace(" ", "").ToUpperInvariant();
        return request is not null && request.Scheme == "https" && request.Host is "localhost" or "10.0.2.2"
            && certificate is not null && normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            && certificate.GetNameInfo(X509NameType.DnsName, false) == "localhost"
            && now.UtcDateTime >= certificate.NotBefore.ToUniversalTime() && now.UtcDateTime <= certificate.NotAfter.ToUniversalTime()
            && certificate.GetCertHashString(HashAlgorithmName.SHA256).Equals(normalized, StringComparison.Ordinal);
    }
    public static HttpClient CreateClient(IConnectionSettings settings)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
#if DEBUG
        // Explicit certificate pin, local development hosts only. Never present in Release.
        handler.ServerCertificateCustomValidationCallback = (request, cert, _, errors) =>
            errors == SslPolicyErrors.None || MatchesLocalPin(request.RequestUri, cert, settings.DevelopmentPin, DateTimeOffset.UtcNow);
#endif
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
    }
}
