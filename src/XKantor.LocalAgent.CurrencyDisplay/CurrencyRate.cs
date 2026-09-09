namespace XKantor.LocalAgent.CurrencyDisplay;

// Patrz etap 2, sekcja 18 (UPDATE_CURRENCY_DISPLAY - przykład "EUR BUY: 4.2500 SELL: 4.3200").
public sealed record CurrencyRate(string Symbol, decimal Buy, decimal Sell);
