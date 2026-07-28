using CodeTutor.Infrastructure.Ai;
using FluentAssertions;

namespace CodeTutor.Infrastructure.Tests;

public sealed class BalanceResponseParserTests
{
    [Fact]
    public void Parse_maps_balance_infos_and_availability()
    {
        var dto = new DeepSeekTextAnswerProvider.BalanceResponseDto
        {
            IsAvailable = true,
            BalanceInfos =
            [
                new DeepSeekTextAnswerProvider.BalanceInfoDto
                {
                    Currency = "CNY",
                    TotalBalance = "110.00",
                    GrantedBalance = "10.00",
                    ToppedUpBalance = "100.00"
                }
            ]
        };

        var info = BalanceResponseParser.Parse(dto);

        info.IsAvailable.Should().BeTrue();
        info.Balances.Should().ContainSingle();
        info.Balances[0].Currency.Should().Be("CNY");
        info.Balances[0].TotalBalance.Should().Be("110.00");
        info.ToDisplayText().Should().Be("余额：CNY 110.00（可用）");
    }

    [Fact]
    public void Parse_handles_empty_balance_infos()
    {
        var dto = new DeepSeekTextAnswerProvider.BalanceResponseDto
        {
            IsAvailable = false,
            BalanceInfos = null
        };

        var info = BalanceResponseParser.Parse(dto);

        info.IsAvailable.Should().BeFalse();
        info.Balances.Should().BeEmpty();
        info.ToDisplayText().Should().Be("余额：不可用");
    }
}
