# ClientServer: tryb offline i snapshot konfiguracji

`ClientServer` jest lokalnym kontrolerem IoT. Do sterowania urządzeniami używa lokalnej telemetrii i MQTT, a z `Server` pobiera topologię roślin oraz reguły automatyzacji. Utrata połączenia z `Server` nie przerywa pracy kontrolera, o ile istnieje ważny lokalny snapshot konfiguracji.

## Synchronizacja konfiguracji

Dla każdego identyfikatora z `IoTController:ClientIds` cykl kontrolera wykonuje:

1. Pobiera topologię roślin z `MainServerApi:PlantsPath`.
2. Pobiera reguły z `MainServerApi:AutomationRulesPath`.
3. Mapuje odpowiedzi do lokalnych kontraktów `ControllerTopologyDto` i `ControllerAutomationRuleDto`.
4. Jeżeli oba żądania zakończą się powodzeniem, tworzy jeden `ControllerConfigurationSnapshot`.
5. Oblicza `Version` jako SHA-256 z serializacji topologii i reguł.
6. Zapisuje cały snapshot w pamięci procesu i w kolejnym backupie na dysku.

Jeżeli pobranie topologii albo reguł się nie powiedzie, synchronizacja jest odrzucana w całości. Ostatnia poprawna konfiguracja pozostaje nienaruszona; pusta odpowiedź lub błąd serwera nie nadpisuje reguł.

## Ważność snapshotu

Ważność ustawia `OfflineMode:SnapshotValidityMinutes` (domyślnie 1440 minut, czyli 24 godziny). Snapshot zawiera:

- `ClientId` - kontrolowanego klienta,
- `Version` - wersję SHA-256 konfiguracji,
- `FetchedAtUtc` - czas poprawnego pobrania,
- `ExpiresAtUtc` - czas końca ważności,
- `Topology` - rośliny i przypisane urządzenia,
- `Rules` - reguły automatyzacji.

Świeży snapshot jest używany normalnie. Po utracie `Server` kontroler nadal odczytuje telemetrię, ocenia zapisane reguły i publikuje komendy przez MQTT. Próba zgłoszenia wykonania reguły do `Server` jest wtedy nieskuteczna, ale nie blokuje lokalnej komendy.

Po wygaśnięciu snapshotu domyślnie włącza się bezpieczny tryb degradacji: telemetria i lokalny MQTT nadal działają, lecz reguły automatyczne nie są wykonywane. Można wymusić dalsze używanie wygasłej konfiguracji przez `OfflineMode:ContinueWithExpiredSnapshot=true`, ale powinno to być stosowane tylko świadomie. `OfflineMode:Enabled=false` wyłącza używanie wygasłych snapshotów, nie wyłącza automatyzacji na świeżej konfiguracji.

## Zapisywanie i odtwarzanie

Ustawienia backupu znajdują się w sekcji `ControllerStateBackup`:

```json
"ControllerStateBackup": {
  "Enabled": true,
  "SaveIntervalSeconds": 60,
  "FilePath": "data/controller-state.json"
}
```

Ścieżka względna jest liczona względem katalogu aplikacji `ClientServer`. Domyślnie plik znajduje się w `apps/ClientServer/data/controller-state.json` podczas uruchomienia lokalnego. W kontenerze należy zamontować trwały wolumen dla katalogu `data`, ponieważ plik w warstwie kontenera może zostać utracony po jego odtworzeniu.

Przy starcie `ControllerStateBackupService` najpierw próbuje odczytać i zdeserializować istniejący plik. Odtwarza topologię, konfiguracje i ostatnią telemetrię do `ControllerStateStore`, zanim rozpocznie się kolejny cykl sterowania. Brak pliku, pusty plik lub niepoprawny snapshot nie zatrzymuje aplikacji.

Zapis przebiega bezpiecznie:

1. Tworzony jest katalog nadrzędny, jeśli nie istnieje.
2. Stan z `ControllerStateStore` jest serializowany do pliku `controller-state.json.tmp`.
3. Strumień jest opróżniany (`FlushAsync`).
4. Plik tymczasowy zastępuje właściwy przez `File.Move(..., overwrite: true)`.

Dzięki temu proces nie nadpisuje działającego snapshotu częściowo zapisanym JSON-em. Interwał jest ograniczony do 15-3600 sekund. Pierwszy zapis wykonywany jest po odtworzeniu stanu, a następne zgodnie z `SaveIntervalSeconds`.

Przykładowy fragment pliku:

```json
{
  "savedAtUtc": "2026-09-08T12:00:00Z",
  "topologies": [],
  "telemetry": [],
  "configurations": [
    {
      "clientId": 1,
      "version": "...sha256...",
      "fetchedAtUtc": "2026-09-08T11:55:00Z",
      "expiresAtUtc": "2026-09-09T11:55:00Z",
      "topology": {},
      "rules": []
    }
  ]
}
```

## Diagnostyka

Bieżący stan snapshotu można sprawdzić przez:

```text
GET /api/client-server/controllers/{clientId}/configuration
```

Endpoint zwraca wersję, czas pobrania, czas wygaśnięcia, liczbę reguł oraz informację, czy automatyzacja jest jeszcze aktywna. Synchronizację ręczną można wymusić przez:

```text
POST /api/client-server/controllers/{clientId}/sync
```

Ręczna synchronizacja również wymaga poprawnego pobrania topologii i reguł; w razie błędu istniejący snapshot pozostaje zachowany.

## Ochrona pliku

Snapshot zawiera konfigurację urządzeń i reguły, dlatego katalog `data` powinien mieć prawa dostępowe ograniczone do procesu `ClientServer`. W środowisku Docker należy przechowywać go na prywatnym, trwałym wolumenie i nie publikować pliku `controller-state.json` w repozytorium.
