using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OstQuiz.Api.Services.Security;

/// <summary>
/// Issues and validates compact HMAC-signed progression tokens of the form
/// <c>base64url(payload).base64url(signature)</c>, where payload is <c>yyyy-MM-dd|step</c>.
///
/// The token authorizes a player to access audio/hints up to <c>step</c> and to submit a
/// guess AT <c>step</c> — without any server-side session. The signature makes it impossible
/// for a client to forge a higher step (i.e. skip ahead to more audio or more hints).
/// </summary>
public class StepTokenService
{
    private readonly byte[] _key;

    public StepTokenService(IOptions<StepTokenOptions> opts, ILogger<StepTokenService> log)
    {
        var secret = opts.Value.Secret;
        if (secret == new StepTokenOptions().Secret)
            log.LogWarning("Using the default step-token secret. Set Tokens__Secret in production.");
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Issue(DateOnly date, int step)
    {
        var payload = $"{date:yyyy-MM-dd}|{step}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        return $"{Base64Url(payloadBytes)}.{Base64Url(Sign(payloadBytes))}";
    }

    public bool TryValidate(string? token, out DateOnly date, out int step)
    {
        date = default;
        step = -1;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return false;

        byte[] payloadBytes, signature;
        try
        {
            payloadBytes = FromBase64Url(token[..dot]);
            signature = FromBase64Url(token[(dot + 1)..]);
        }
        catch
        {
            return false;
        }

        // Constant-time signature comparison.
        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payloadBytes))) return false;

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var bar = payload.IndexOf('|');
        if (bar <= 0) return false;

        return DateOnly.TryParse(payload[..bar], out date) && int.TryParse(payload[(bar + 1)..], out step);
    }

    private byte[] Sign(byte[] payload)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(payload);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(s);
    }
}
