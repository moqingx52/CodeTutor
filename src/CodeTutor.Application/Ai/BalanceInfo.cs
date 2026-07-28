namespace CodeTutor.Application.Ai;

public sealed record BalanceInfo(
    bool IsAvailable,
    IReadOnlyList<BalanceCurrencyInfo> Balances)
{
    public string ToDisplayText()
    {
        if (Balances.Count == 0)
            return IsAvailable ? "余额：可用（无明细）" : "余额：不可用";

        var parts = Balances
            .Select(b => $"{b.Currency} {b.TotalBalance}")
            .ToList();

        var status = IsAvailable ? "可用" : "不可用";
        return $"余额：{string.Join(" / ", parts)}（{status}）";
    }
}

public sealed record BalanceCurrencyInfo(
    string Currency,
    string TotalBalance,
    string GrantedBalance,
    string ToppedUpBalance);
