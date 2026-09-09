using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace XKantor.LocalAgent.Security;

// Cienka warstwa nad Windows DPAPI (System.Security.Cryptography.ProtectedData) - patrz etap 2,
// sekcja 10 ("Użyj odpowiednich mechanizmów Windows/.NET do bezpiecznego przechowywania
// sekretów") i docs/SECURITY.md. Zakres LocalMachine (nie CurrentUser) - patrz uzasadnienie w
// Core/Configuration/ConfigPaths.cs: usługa Windows zwykle działa na koncie bez załadowanego
// profilu użytkownika, więc CurrentUser by nie zadziałało niezawodnie. Kompromis: każdy
// proces/użytkownik NA TEJ SAMEJ maszynie z odpowiednimi uprawnieniami plikowymi może
// odszyfrować - akceptowalne, bo i tak jedynym procesem czytającym ten plik jest usługa
// Agenta, a dostęp do samego pliku jest dodatkowo ograniczony ACL-ami katalogu (patrz
// installer/README - konfiguracja uprawnień %ProgramData%\XKantorLocalAgent\secure).
[SupportedOSPlatform("windows")]
public static class SecureKeyStore
{
    public static byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.LocalMachine);

    public static byte[] Unprotect(byte[] protectedBytes) =>
        ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.LocalMachine);
}
