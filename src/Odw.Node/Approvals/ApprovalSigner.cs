using System.Security.Cryptography;
using System.Text;

namespace Odw.Node.Approvals;

/// <summary>Firma HMAC-SHA-256 de una decisión de aprobación (clave de 32 bytes).</summary>
public sealed class ApprovalSigner
{
    private readonly byte[] _key;

    public ApprovalSigner(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length < 32)
        {
            throw new ArgumentException("La clave HMAC debe tener al menos 32 bytes.", nameof(key));
        }

        _key = key.ToArray();
    }

    public string Sign(ApprovalRecord record, string status, DateTimeOffset decidedAt, string decidedBy)
    {
        var payload =
            $"{record.ApprovalId}|{record.TaskId}|{record.TaskHash}|{record.Nonce}|{status}|"
            + $"{decidedAt.ToUniversalTime():O}|{decidedBy}";

        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return "hmac-sha256:" + Convert.ToHexStringLower(hash);
    }

    public bool Verify(ApprovalRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Signature)
            || record.DecidedAt is null
            || string.IsNullOrWhiteSpace(record.DecidedBy)
            || record.Status is not (ApprovalStatus.Approved or ApprovalStatus.Denied))
        {
            return false;
        }

        var expected = Sign(record, record.Status, record.DecidedAt.Value, record.DecidedBy);
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(record.Signature);

        return left.Length == right.Length
               && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
