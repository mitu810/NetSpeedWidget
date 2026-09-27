using System;
using System.Security.Cryptography;
using System.Text;

namespace NetSpeedWidget.Services;

public static class PasswordHashService
{
    private const int Iterations = 600_000;
    private const string Prefix = "PBKDF2-SHA256";

    /// <summary>使用独立随机盐生成密码派生值，不存储密码明文。</summary>
    public static string Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return string.Empty;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>验证新密码格式并兼容既有 SHA-256 配置，使用固定时间比较。</summary>
    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored)) return false;
        try
        {
            // 1. 保留既有配置兼容性；重新设置密码时使用新格式。
            if (stored.Length == 64)
                return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)), Convert.FromHexString(stored));
            // 2. 只接受已知迭代数和固定长度，避免配置损坏导致无界计算。
            var fields = stored.Split('$');
            if (fields.Length != 4 || fields[0] != Prefix || fields[1] != Iterations.ToString()) return false;
            var salt = Convert.FromBase64String(fields[2]);
            var expected = Convert.FromBase64String(fields[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
