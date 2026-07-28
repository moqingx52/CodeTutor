using CodeTutor.Application.Ai;

namespace CodeTutor.Infrastructure.Ai;

internal static class BalanceResponseParser
{
    public static BalanceInfo Parse(DeepSeekTextAnswerProvider.BalanceResponseDto dto)
    {
        var balances = dto.BalanceInfos?
            .Select(b => new BalanceCurrencyInfo(
                b.Currency ?? "UNKNOWN",
                b.TotalBalance ?? "0",
                b.GrantedBalance ?? "0",
                b.ToppedUpBalance ?? "0"))
            .ToList() ?? [];

        return new BalanceInfo(dto.IsAvailable, balances);
    }
}
