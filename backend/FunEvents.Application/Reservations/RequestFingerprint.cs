using System.Security.Cryptography;
using System.Text;

namespace FunEvents.Application.Reservations;

internal static class RequestFingerprint
{
    public static string Create(string eventCode, string sessionCode, string userCode, int quantity)
    {
        var canonicalRequest = $"{eventCode}\n{sessionCode}\n{userCode}\n{quantity}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
    }
}
