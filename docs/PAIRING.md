# Parowanie stanowiska

## Dlaczego przez przeglądarkę, nie bezpośrednio z Agenta

Agent nigdy nie inicjuje połączeń wychodzących do Internetu (patrz `docs/DECISIONS.md` #7,
`docs/ARCHITECTURE.md`). Cały przepływ parowania/odnowienia jest więc zaprojektowany tak, że
**przeglądarka** (już zalogowana do xkantor.app, po HTTPS) jest jedynym elementem, który
faktycznie rozmawia z centralą - Agent tylko przygotowuje/konsumuje dane lokalnie.

## Przepływ parowania (pierwsze uruchomienie stanowiska)

```
OPERATOR (w xkantor.app)                 PRZEGLĄDARKA                    AGENT (localhost)
        |                                     |                               |
        |  "Sparuj to stanowisko"             |                               |
        |------------------------------------>|                               |
        |                                     |  POST /api/v1/pairing/start   |
        |                                     |------------------------------>|
        |                                     |                               | generuje/ładuje
        |                                     |                               | parę kluczy ECDSA,
        |                                     |                               | losuje PairingCode
        |                                     |  {PairingCode, PublicKey}     | (ważny 10 min,
        |                                     |<------------------------------| jednorazowy)
        |                                     |                               |
        |     pokazuje PairingCode            |                               |
        |<------------------------------------|                               |
        |                                     |                               |
        |  wpisuje PairingCode w panelu       |                               |
        |  xkantor.app (poza tym repo)        |                               |
        |------------------------------------>|                               |
        |                                     |  HTTPS: xkantor.app zapisuje  |
        |                                     |  parowanie, generuje          |
        |                                     |  StationId + IssuerToken      |
        |                                     |  (poza zakresem tego repo)    |
        |                                     |                               |
        |                                     |  POST /api/v1/pairing/complete|
        |                                     |  {PairingCode, StationId,     |
        |                                     |   IssuerToken}                |
        |                                     |------------------------------>|
        |                                     |                               | weryfikuje kod,
        |                                     |                               | losuje StationSecret,
        |                                     |                               | zapisuje certyfikat
        |                                     |  {StationSecret (RAZ!),       |
        |                                     |   CertificateExpiresAtUtc}    |
        |                                     |<------------------------------|
        |                                     |                               |
        |            przeglądarka/xkantor.app zapamiętuje StationSecret       |
        |            (poza zakresem tego repo - do ustalenia z backendem,     |
        |            patrz docs/QUESTIONS.csv)                                |
```

**Format `IssuerToken`** (dowód, że centrala faktycznie autoryzowała to parowanie) nie jest
jeszcze ustalony z backendem xkantor.app - Agent przyjmuje go jako nieprzezroczysty ciąg i
zapisuje w ramach `StationCertificate` bez własnej weryfikacji podpisu centrali. Patrz
`docs/QUESTIONS.csv` (PAIRING_PROTOCOL) - jawnie odnotowane, nieblokujące dalszej pracy zgodnie z
etapem 2, sekcja 4.

## Użycie na co dzień: token sesji

Po sparowaniu przeglądarka NIE wysyła `StationSecret` przy każdym żądaniu operacji - zamiast
tego wymienia go na krótkoterminowy token:

```
PRZEGLĄDARKA                                          AGENT
    |  POST /api/v1/session/start                       |
    |  {StationSecret, Scopes: ["read","print"]}         |
    |---------------------------------------------------->|
    |  {Token (ważny ~2 min), ExpiresAtUtc}               |
    |<----------------------------------------------------|
    |                                                     |
    |  GET /api/v1/printers                              |
    |  Authorization: Bearer <Token>                      |
    |---------------------------------------------------->|
```

## Odnawianie tożsamości (co ~23 dni, w oknie 7 dni przed wygaśnięciem)

```
PRZEGLĄDARKA                       AGENT                          XKANTOR.APP (poza tym repo)
    |  POST /api/v1/identity/       |                                       |
    |  renewal-request               |                                       |
    |  (wymaga tokenu sesji,        |                                       |
    |   scope "renew")               |                                       |
    |------------------------------->|                                       |
    |                                | podpisuje żądanie                    |
    |                                | (StationId, thumbprint,               |
    |                                |  nonce) kluczem prywatnym             |
    |  {PayloadJson, SignatureBase64}|                                       |
    |<-------------------------------|                                       |
    |                                                                        |
    |  przekazuje żądanie do xkantor.app przez HTTPS (poza zakresem repo)    |
    |------------------------------------------------------------------------>
    |  <- nowy certyfikat (StationId, thumbprint, nowe ExpiresAtUtc, IssuerToken)
    |<------------------------------------------------------------------------
    |                                                                        |
    |  POST /api/v1/identity/certificate                                    |
    |  (wymaga tokenu sesji, scope "renew")                                 |
    |------------------------------->|                                       |
    |                                | weryfikuje StationId + thumbprint     |
    |                                | pasują do siebie, zapisuje            |
    |  {success: true}               |                                       |
    |<-------------------------------|                                       |
```

Jeśli odnowienie się nie powiedzie przed wygaśnięciem: Agent wchodzi w **okres awaryjny**
(7 dni, `RenewalPolicy.OkresAwaryjny`) - działa nadal (`LIMITED`), ale przestaje działać
bezterminowo bez ważnej autoryzacji (`OFFLINE` po tym okresie). Patrz `docs/SECURITY.md`.
