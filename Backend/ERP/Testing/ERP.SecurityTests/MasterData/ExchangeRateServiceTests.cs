using Domain.Features.MasterData.Currency;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.SecurityTests.MasterData;

public class ExchangeRateServiceTests
{
    [Fact]
    public async Task ResolveAsync_SameCurrencyReturnsOneWithoutMasterRate()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.ResolveAsync(
            fixture.TenantId,
            fixture.From.CurrencyId,
            fixture.From.CurrencyId,
            new DateTime(2026, 10, 31));

        Assert.Equal(1m, result.Rate);
        Assert.Equal(ExchangeRateSource.SameCurrency, result.Source);
        Assert.Null(result.ExchangeRateId);
    }

    [Fact]
    public async Task ResolveAsync_UsesLatestDailyRateBeforeMonthlyFallback()
    {
        await using var fixture = new Fixture();
        await fixture.AddRate(ExchangeRateType.Monthly, new DateTime(2026, 10, 1), 1.1m);
        await fixture.AddRate(ExchangeRateType.Daily, new DateTime(2026, 10, 30), 1.234567890123m);

        var result = await fixture.Service.ResolveAsync(
            fixture.TenantId,
            fixture.From.CurrencyId,
            fixture.To.CurrencyId,
            new DateTime(2026, 10, 31));

        Assert.Equal(1.234567890123m, result.Rate);
        Assert.Equal(ExchangeRateSource.Daily, result.Source);
        Assert.Equal(new DateTime(2026, 10, 30), result.EffectiveDate);
    }

    [Fact]
    public async Task ResolveAsync_UsesApplicableThenPreviousMonthlyRate()
    {
        await using var fixture = new Fixture();
        await fixture.AddRate(ExchangeRateType.Monthly, new DateTime(2026, 9, 20), 1.2m);

        var previousMonth = await fixture.Service.ResolveAsync(
            fixture.TenantId,
            fixture.From.CurrencyId,
            fixture.To.CurrencyId,
            new DateTime(2026, 10, 31));

        Assert.Equal(1.2m, previousMonth.Rate);
        Assert.Equal(ExchangeRateSource.Monthly, previousMonth.Source);
        Assert.Equal(new DateTime(2026, 9, 1), previousMonth.EffectiveDate);

        await fixture.AddRate(ExchangeRateType.Monthly, new DateTime(2026, 10, 15), 1.3m);
        var applicableMonth = await fixture.Service.ResolveAsync(
            fixture.TenantId,
            fixture.From.CurrencyId,
            fixture.To.CurrencyId,
            new DateTime(2026, 10, 31));

        Assert.Equal(1.3m, applicableMonth.Rate);
        Assert.Equal(new DateTime(2026, 10, 1), applicableMonth.EffectiveDate);
    }

    [Fact]
    public async Task ResolveAsync_RejectsFutureAndReversePairRates()
    {
        await using var fixture = new Fixture();
        await fixture.AddRate(ExchangeRateType.Daily, new DateTime(2026, 11, 1), 1.4m);
        await fixture.AddRate(ExchangeRateType.Monthly, new DateTime(2026, 11, 1), 1.5m);
        await fixture.AddRate(ExchangeRateType.Daily, new DateTime(2026, 10, 30), 0.8m, reverse: true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ResolveAsync(
                fixture.TenantId,
                fixture.From.CurrencyId,
                fixture.To.CurrencyId,
                new DateTime(2026, 10, 31)));

        Assert.Contains("No valid exchange rate", error.Message);
    }

    [Fact]
    public async Task ResolveAsync_RejectsInactiveRateRecords()
    {
        await using var fixture = new Fixture();
        var record = await fixture.AddRate(ExchangeRateType.Daily, new DateTime(2026, 10, 30), 1.2m);
        record.Deactivate();
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ResolveAsync(
                fixture.TenantId,
                fixture.From.CurrencyId,
                fixture.To.CurrencyId,
                new DateTime(2026, 10, 31)));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly CurrencyRepository _currencies;
        private readonly ExchangeRateRepository _rates;

        public Guid TenantId { get; } = Guid.NewGuid();
        public Currency From { get; }
        public Currency To { get; }
        public DomainDbContext Context { get; }
        public ExchangeRateService Service { get; }

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<DomainDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Context = new DomainDbContext(options);
            From = Currency.Create(TenantId, "AAA", "Currency A", "A");
            To = Currency.Create(TenantId, "BBB", "Currency B", "B");
            Context.Currencies.AddRange(From, To);
            Context.SaveChanges();

            _currencies = new CurrencyRepository(Context);
            _rates = new ExchangeRateRepository(Context);
            Service = new ExchangeRateService(_rates, _currencies);
        }

        public async Task<ExchangeRate> AddRate(
            ExchangeRateType type,
            DateTime effectiveDate,
            decimal value,
            bool reverse = false)
        {
            var rate = ExchangeRate.Create(
                TenantId,
                reverse ? To.CurrencyId : From.CurrencyId,
                reverse ? From.CurrencyId : To.CurrencyId,
                type,
                effectiveDate,
                value);
            await _rates.AddAsync(rate);
            await _rates.SaveChangesAsync();
            return rate;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
