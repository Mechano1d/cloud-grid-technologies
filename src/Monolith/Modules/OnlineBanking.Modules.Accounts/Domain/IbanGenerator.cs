using System.Security.Cryptography;

namespace OnlineBanking.Modules.Accounts.Domain;

public static class IbanGenerator
{
    private const string BankCode = "322313";

    /// <summary>
    /// Generates a 29-character Ukrainian IBAN format (UA + 2 check digits + 6 bank code + 19 account digits).
    /// </summary>
    public static string GenerateUkrainianIban()
    {
        Span<char> accountDigits = stackalloc char[19];
        for (var i = 0; i < accountDigits.Length; i++)
        {
            accountDigits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        var bban = $"{BankCode}{new string(accountDigits)}";
        var checkDigits = ComputeMod97CheckDigits("UA", bban);
        return $"UA{checkDigits:00}{bban}";
    }

    private static int ComputeMod97CheckDigits(string countryCode, string bban)
    {
        var rearranged = $"{bban}{countryCode}00";
        var remainder = 0;

        foreach (var ch in rearranged)
        {
            if (char.IsDigit(ch))
            {
                remainder = (remainder * 10 + (ch - '0')) % 97;
            }
            else
            {
                var numeric = char.ToUpperInvariant(ch) - 'A' + 10;
                remainder = (remainder * 100 + numeric) % 97;
            }
        }

        return 98 - remainder;
    }
}
