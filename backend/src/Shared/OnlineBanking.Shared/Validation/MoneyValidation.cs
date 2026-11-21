namespace OnlineBanking.Shared.Validation;

public static class MoneyValidation
{
    public static bool IsValidPositiveAmount(decimal amount) =>
        amount > 0m && decimal.Round(amount, 2) == amount;

    public static bool IsValidNonNegativeAmount(decimal amount) =>
        amount >= 0m && decimal.Round(amount, 2) == amount;
}
