using System.Security.Cryptography;

namespace XiaoShuTong.Tools;

/// <summary>
/// 邀请码生成器（切片验证版）
/// </summary>
/// <remarks>
/// 一次性邀请码 8 位 / 内测邀请码 16 位（平台生成，本切片仅校验）。
/// 字符集去除易混淆字符（0/O、1/I/L）。
/// </remarks>
public static class InviteCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// 生成指定长度的随机邀请码
    /// </summary>
    public static string Generate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
