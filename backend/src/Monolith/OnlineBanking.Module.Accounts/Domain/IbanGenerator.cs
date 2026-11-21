using System.Security.Cryptography;
using System.Text;

namespace OnlineBanking.Module.Accounts.Domain;

public static class IbanGenerator
{
    public const string DefaultBankCode = "322313";
    private const int UaIbanLength = 29;

    public static string Generate(string bankCode = DefaultBankCode)
    {
        var digits = new char[19];
        for (var i = 0; i < digits.Length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return CreateFromBban(bankCode, new string(digits));
    }

    public static string CreateFromBban(string bankCode, string accountDigits19)
    {
        if (string.IsNullOrEmpty(bankCode) || bankCode.Length != 6 || !bankCode.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Bank code must consist of 6 ASCII digits.", nameof(bankCode));
        }

        if (string.IsNullOrEmpty(accountDigits19) || accountDigits19.Length != 19 || !accountDigits19.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Account number must consist of 19 ASCII digits.", nameof(accountDigits19));
        }

        var bban = bankCode + accountDigits19;
        var remainder = ComputeMod97($"{bban}UA00");
        var checkDigits = 98 - remainder;

        return $"UA{checkDigits:00}{bban}";
    }

    public static bool IsValidUaIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var normalized = iban.Trim().ToUpperInvariant();
        if (normalized.Length != UaIbanLength || !normalized.StartsWith("UA", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 2; i < normalized.Length; i++)
        {
            if (!char.IsAsciiDigit(normalized[i]))
            {
                return false;
            }
        }

        var rearranged = string.Concat(normalized.AsSpan(4), normalized.AsSpan(0, 4));
        return ComputeMod97(rearranged) == 1;
    }

    private static int ComputeMod97(string input)
    {
        var expanded = new StringBuilder(input.Length * 2);
        foreach (var ch in input)
        {
            if (char.IsAsciiDigit(ch))
            {
                expanded.Append(ch);
            }
            else if (char.IsAsciiLetterUpper(ch))
            {
                expanded.Append(ch - 'A' + 10);
            }
            else
            {
                return -1;
            }
        }

        var remainder = 0;
        for (var i = 0; i < expanded.Length; i++)
        {
            remainder = (remainder * 10 + (expanded[i] - '0')) % 97;
        }

        return remainder;
    }
}
