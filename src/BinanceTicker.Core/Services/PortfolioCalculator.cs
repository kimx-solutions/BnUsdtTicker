using BinanceTicker.Core.Models;
namespace BinanceTicker.Core.Services;

public static class PortfolioCalculator
{
    public static HoldingValuation Value(HoldingSetting holding,TickerPrice? quote,ConnectionStatus status,DateTimeOffset now)
    {
        if (holding.Quantity == 0) return new(0,0,0,null);
        var stale=quote is not null && (status != ConnectionStatus.Connected ||
            now-new DateTimeOffset(quote.UpdatedAt.ToUniversalTime()) > TimeSpan.FromSeconds(60));
        try
        {
            var cost=holding.Quantity*holding.AverageCost;
            if (quote is null) return new(null,cost,null,null,MissingQuote:true);
            var value=holding.Quantity*quote.Price;
            var profit=holding.Quantity*(quote.Price-holding.AverageCost);
            return new(value,cost,profit,cost == 0 ? null : profit/cost*100,IsStale:stale,UpdatedAt:quote.UpdatedAt);
        }
        catch (OverflowException) { return new(null,null,null,null,IsStale:stale,Overflowed:true,UpdatedAt:quote?.UpdatedAt); }
    }

    public static PortfolioSummary Summarize(IEnumerable<HoldingSetting> holdings,IReadOnlyDictionary<string,TickerPrice> quotes,
        ConnectionStatus status,DateTimeOffset now)
    {
        decimal value=0,cost=0,profit=0; var count=0;var missing=0;var stale=false;
        foreach (var holding in holdings.DistinctBy(h=>h.Symbol,StringComparer.Ordinal))
        {
            count++;
            var row=Value(holding,quotes.GetValueOrDefault(holding.Symbol),status,now); stale |= row.IsStale;
            if (row.MissingQuote || row.Overflowed) { missing++; continue; }
            try
            {
                // Compute all sums before assignment so a failed addition cannot corrupt the subtotal.
                var nextValue=value+row.MarketValue!.Value;var nextCost=cost+row.CostBasis!.Value;var nextProfit=profit+row.Profit!.Value;
                value=nextValue;cost=nextCost;profit=nextProfit;
            }
            catch (OverflowException) { missing++; }
        }
        decimal? percent=null;
        if (missing == 0 && cost != 0)
        {
            try { percent=profit/cost*100; } catch (OverflowException) { missing++; }
        }
        return new(missing == 0 ? value : null,missing == 0 ? cost : null,missing == 0 ? profit : null,percent,
            value,profit,count,missing,stale);
    }
}
