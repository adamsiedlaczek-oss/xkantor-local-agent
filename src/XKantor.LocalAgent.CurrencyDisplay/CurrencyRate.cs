namespace XKantor.LocalAgent.CurrencyDisplay;

// Patrz etap 2, sekcja 18 (UPDATE_CURRENCY_DISPLAY - przykład "EUR BUY: 4.2500 SELL: 4.3200").
//
// DisplayRow/DecimalPlaces dodane dla sterownika PEZET (zadanie "pezet\realizacja.txt") - dane
// biznesowe (kurs po uwzględnieniu "Kurs walut za", format po "Miejsce po przecinku", numer
// wiersza z "Pozycja wyświetlacza") ZAWSZE liczy xKantor.APP (zadanie sekcja 6/8 - "LocalAgent
// nie duplikuje logiki kursów"), Agent tylko formatuje/wysyła to, co dostał. Wartości domyślne
// (0/4) - SerialLineAdapter (istniejący, generyczny adapter) ich nie używa, więc nic się dla
// niego nie zmienia. DisplayRow <= 0 = ta waluta nie ma przypisanego wiersza fizycznej tablicy -
// adapter PEZET pomija taki wiersz (nigdy nie zgaduje numeru).
public sealed record CurrencyRate(string Symbol, decimal Buy, decimal Sell, int DisplayRow = 0, int DecimalPlaces = 4);
