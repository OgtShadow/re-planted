# re-planted

Projekt inżynierski do zarządzania domowym systemem roślin (frontend + backend + PostgreSQL + komunikacja realtime).

## 1. O projekcie

re-planted składa się z:

- aplikacji frontendowej (React + Vite),
- API backendowego (ASP.NET Core Minimal API),
- dodatkowego serwisu `ClientServer` do logiki komunikacji klient-serwer,
- lokalnego brokera MQTT (Mosquitto) do komunikacji ClientServer <-> ESP32,
- bazy PostgreSQL,
- komunikacji realtime przez SignalR.

Aktualna konfiguracja zawiera gotowy workflow uruchamiania przez Docker Compose oraz lokalnie (bez kontenerów).

## 2. Stack technologiczny

- Frontend: React 19, Vite, MUI, SignalR client
- Backend: .NET 8 (ASP.NET Core Minimal API), Entity Framework Core, Npgsql
- Baza danych: PostgreSQL 16
- Runtime deployment: Docker, Docker Compose, Nginx (dla frontendu)
- Messaging: MQTT (Mosquitto), MQTTnet (.NET), PubSubClient (ESP32)
- Dokumentacja API: Swagger / OpenAPI

## 3. Struktura repozytorium

Najważniejsze katalogi i pliki:

```
.
├── apps/
│   ├── Client/                    # Frontend (React + Vite)
│   │   ├── src/
│   │   ├── Dockerfile
│   │   └── nginx.conf
│   ├── ClientServer/              # Osobny serwis ASP.NET Core do logiki klient-serwer
│   │   ├── Controllers/
│   │   ├── Contracts/
│   │   ├── Services/
│   │   ├── Dockerfile
│   │   └── ClientServer.csproj
│   ├── ArtificialEsp/             # Symulator + firmware ESP32
│   │   ├── main.go                # HTTP mock urządzenia
│   │   └── firmware/RePlantedNode/RePlantedNode.ino
│   └── Server/                    # Backend (.NET 8)
│       ├── src/
│       │   ├── Contracts/         # DTO i kontrakty API
│       │   ├── Data/              # DbContext i konfiguracja EF
│       │   ├── Endpoints/         # Minimal API endpointy
│       │   ├── Extensions/        # Rozszerzenia startupu
│       │   ├── Models/            # Encje domenowe
│       │   └── Services/
│       ├── Migrations/            # Migracje EF Core
│       ├── Dockerfile
│       └── Server.csproj
├── docker-compose.yml
├── .env
└── README.md
```

## 4. Wymagania

### Uruchamianie przez Docker

- Docker Desktop z aktywnym Docker Compose

### Uruchamianie lokalne (bez Dockera)

- Node.js 20+ (zalecany 22)
- npm
- .NET SDK 8.0
- lokalny PostgreSQL (jeśli uruchamiasz backend poza Dockerem)

## 5. Konfiguracja środowiska

Projekt używa pliku `.env` w katalogu głównym.

Aktualnie wykorzystywane zmienne:

```
POSTGRES_DB=replanted_db
POSTGRES_USER=postgres
POSTGRES_PASSWORD=super_tajne_haslo_123

POSTGRES_PORT=5432
APP_PORT=8081
CLIENT_PORT=8080
SENSOR_MOCK_PORT=8085
APP_CLIENT_PORT=8082

VITE_API_BASE_URL=http://localhost:8081
```

Uwagi:

- `APP_PORT`, `CLIENT_PORT`, `SENSOR_MOCK_PORT` i `APP_CLIENT_PORT` są używane przez Docker Compose.
- `VITE_API_BASE_URL` przekazywane jest jako build-arg do obrazu frontendu Docker.
- Backend w kontenerze nasłuchuje na porcie `8080`.
- Serwis `ClientServer` w kontenerze nasłuchuje na porcie `8080`, a host mapuje go na `APP_CLIENT_PORT`.

## 6. Szybki start (Docker Compose)

W katalogu głównym repo:

1. Build obrazów:

```
docker compose build
```

2. Start usług:

```
docker compose up -d
```

3. Sprawdzenie statusu:

```
docker compose ps
```

4. Logi backendu:

```
docker compose logs app -f
```

5. Logi serwisu ClientServer:

```
docker compose logs app_client -f
```

6. Zatrzymanie usług:

```
docker compose down
```

Domyślne adresy po starcie:

- Frontend: http://localhost:8080
- Backend API: http://localhost:8081
- ClientServer: http://localhost:8082
- Swagger UI: http://localhost:8081/swagger
- OpenAPI JSON: http://localhost:8081/swagger/v1/swagger.json
- ClientServer Swagger: http://localhost:8082/swagger
- ClientServer health: http://localhost:8082/api/client-server/health
- ClientServer server-check: http://localhost:8082/api/client-server/server-check
- Sensor mock: http://localhost:8085/sensors
- PostgreSQL: localhost:5432
- MQTT broker: localhost:1883

## 7. Uruchamianie lokalne (bez Docker Compose)

### 7.1 Backend

Przejdź do katalogu backendu:

```
cd apps/Server
```

Uruchom aplikację:

```
dotnet run
```

Domyślny profil developerski nasłuchuje na:

- http://localhost:5000

Jeżeli chcesz hot-reload:

```
dotnet watch run
```

### 7.2 Frontend

Przejdź do katalogu frontendu:

```
cd apps/Client
npm install
npm run dev
```

Vite uruchomi frontend (zwykle pod adresem http://localhost:5173).

Frontend odczytuje API URL z `VITE_API_BASE_URL`, a jeśli go nie ma, domyślnie używa `http://localhost:5000`.

## 8. Skrypty npm (apps/Client)

- `npm run dev` - uruchamia frontend Vite w trybie developerskim
- `npm run start` - alias na `vite`
- `npm run build` - build produkcyjny frontendu
- `npm run preview` - podgląd buildu
- `npm run lint` - lint frontendu
- `npm run dev:backend` - uruchamia backend przez `dotnet watch run`
- `npm run dev:frontend` - uruchamia frontend Vite
- `npm run .` - uruchamia frontend i backend równolegle

## 9. API i realtime

### 9.1 Endpointy diagnostyczne

- `GET /` - status serwera + podsumowanie danych
- `GET /communication-test` - szybki test łączności
- `GET /api/post` - informacja o endpointzie testowym
- `POST /api/post` - zapis testowej wiadomości in-memory

### 9.2 Endpointy roślin

- `GET /api/users/{userId}/plants` - lista roślin użytkownika
- `GET /api/users/{userId}/plants/{id}` - szczegóły rośliny użytkownika
- `POST /api/users/{userId}/plants` - dodanie rośliny dla użytkownika
- `PUT /api/users/{userId}/plants/{id}` - aktualizacja rośliny użytkownika
- `DELETE /api/users/{userId}/plants/{id}` - usunięcie rośliny użytkownika

### 9.3 SignalR

Hub:

- `/plantHub`

Wysyłane zdarzenie po zmianach w roślinach:

- `PlantsUpdated`

### 9.4 ClientServer

Serwis `ClientServer` to osobny kontener ASP.NET Core, przygotowany pod logikę pośredniczącą między frontendem a głównym backendem.

Dostępne endpointy:

- `GET /` - prosty status serwisu i link do dokumentacji
- `GET /api/client-server/health` - health check serwisu
- `GET /api/client-server/server-check` - test połączenia z głównym serwerem
- `GET /api/client-server/controllers/{clientId}/devices/{deviceId}/telemetry/latest` - ostatnia telemetria MQTT urządzenia
- `POST /api/client-server/controllers/{clientId}/devices/{deviceId}/pump` - publikacja komendy uruchomienia pompy z parametrem `durationMs`

Serwis korzysta z konfiguracji:

- `MainServerApi__BaseUrl=http://app:8080`
- `MainServerApi__PlantsPath=/api/users/{clientId}/plants`

Dodatkowa konfiguracja MQTT:

- `Mqtt__Enabled=true`
- `Mqtt__BrokerHost=mosquitto`
- `Mqtt__BrokerPort=1883`
- `Mqtt__TelemetryTopicFilter=replanted/telemetry/+/+`
- `Mqtt__CommandsTopicTemplate=replanted/commands/{deviceId}`

### 9.5 MQTT Tier 3 <-> Tier 4

Topiki:

- Telemetria: `replanted/telemetry/{sensor|actuator}/{deviceId}`
- Komendy: `replanted/commands/{deviceId}`

Model telemetrii (`TelemetryPayload`):

- `deviceId`
- `sourceType`
- `soilMoisture`, `lightLevel`, `temperature`, `humidity`, `waterLevel` (zakres 0-1000)
- `waterLevelOk`, `pumpState`, `lampState`
- `timestampUtc`

Model komendy (`CommandPayload`):

- `deviceId`
- `command` (np. `pump`)
- `state`
- `durationMs`
- `requestedAtUtc`

Bezpieczeństwo wykonania komendy pompy w firmware ESP32:

- interlock poziomu wody blokuje start pompy przy niskim stanie,
- dead-man switch wyłącza pompę automatycznie po `durationMs`,
- filtr moving average wygładza odczyty ADC przed publikacją telemetrii.

## 10. Swagger / OpenAPI

Dokumentacja API jest włączona i dostępna pod:

- UI: http://localhost:8080/swagger
- JSON: http://localhost:8080/swagger/v1/swagger.json

Endpointy mają przypisane:

- tagi,
- podsumowania (`summary`),
- opisy (`description`),
- deklaracje request/response.

## 11. Baza danych i migracje

- Backend uruchamia migracje automatycznie przy starcie (`Database.Migrate()`).
- W Docker Compose backend czeka na zdrowy stan Postgresa (healthcheck), zanim wystartuje.

## 12. Najczęstsze problemy

### CORS / błędy komunikacji frontend-backend

- Upewnij się, że frontend i backend używają zgodnego URL API (`VITE_API_BASE_URL`).
- W Docker Compose backend działa na porcie 8080.
- Serwis `ClientServer` jest dostępny pod `http://localhost:8082`.

### Błąd połączenia z bazą przy starcie

- Sprawdź status Postgresa:

```
docker compose ps
docker compose logs postgres_db
```

### Błąd builda Dockera przez duży context

- Buduj serwer z kontekstem `apps/Server`, nie z katalogu głównego dla Dockerfile serwera.

Poprawny przykład:

```
docker build -f ./apps/Server/Dockerfile -t re-planted-app ./apps/Server
```

## 13. Przydatne komendy

```
# pełny start
docker compose up -d --build

# podgląd logów backendu
docker compose logs app -f

# restart backendu
docker compose restart app

# sprawdzenie API
curl http://localhost:8080/api/plants
```

## 14. Informacje dodatkowe

- Notion projektu: https://www.notion.so/Re-planted-2b191ddb0b1180139f38d5eb8f4dd225

## 15. Mock Sensor System

system stworzony na potrzeby symulowania zwrotu z sensorów na potrzeby procesu pisania i testowania systemu Server Kilenta

### 15.1 Build
Powinien zachpodzić razem z resztą ponieważ posioada własny kontener lub bez docker:
```
cd apps/ArtificialEsp
go run main.go
```

### 15.2 Endpointy

- `GET /sensors` - przykładowe zamockowane odczyty z sensorów
- `GET /swagger` - pełna dokumentacja endpointów
- `POST /command/pump` - włączanie/wyłączanie pompy
- `POST /command/light` - włączanie/wyłączanie światła
- `POST /simulate/water-tank` - symulacja poziomu wody w zbiorniku

### 15.3 Dodatkowe informacje

Zdarzały się błędy budowania dockera gdzie miał problem z cache aplikacji i nie wczytywał nowych zmian więc watch out i jak
```
docker compose up -d --build --force-recreate --no-deps sensor-mock
```
nie przejdzie i dalej wyglądało na cache:
```
docker compose build --no-cache sensor-mock
docker compose up -d --force-recreate --no-deps sensor-mock
```

## 16. Firmware ESP32

Bazowy firmware pod lokalną komunikację MQTT znajduje się w:

- `apps/ArtificialEsp/firmware/RePlantedNode/RePlantedNode.ino`

Firmware realizuje:

- subskrypcję komend z `replanted/commands/{deviceId}`,
- publikację telemetrii sensor/actuator do `replanted/telemetry/...`,
- interlock pompy zależny od czujnika poziomu cieczy,
- automatyczne wyłączanie pompy po `durationMs` bez blokowania pętli,
- moving average dla wejść analogowych (wilgotność gleby, światło).

## 17. Automatyzacja: trwałe reguły i Rule Engine

System automatyzacji zastąpił dawną logikę, która obsługiwała tylko podlewanie pierwszej napotkanej rośliny z niską wilgotnością.

### 17.1 Model `AutomationRule` (Server)

Trwała encja EF Core (`apps/Server/src/Models/AutomationRule.cs`), właściciel danych: **Server**. Reguła wiąże: roślinę, urządzenie-czujnik + odczytywane pole, warunek + próg, urządzenie-aktuator + akcję, czas trwania (traktowany jako bezpiecznik/maksimum, patrz 17.3), harmonogram godzinowy, priorytet, cooldown i status.

CRUD API: `/api/users/{userId}/automation-rules` (`GET`, `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}`), autoryzacja jak przy roślinach/urządzeniach (użytkownik zarządza tylko swoimi regułami). Dodatkowo `POST /{id}/trigger` — wywoływane przez ClientServer zaraz po wykonaniu reguły, żeby cooldown (`LastTriggeredUtc`) przetrwał restart ClientServer i był widoczny w API.

Harmonogram światła nie jest ustawiany ręcznie per reguła: dla aktuatorów z `TargetParameter == "light"` Server automatycznie nadpisuje `ScheduleStartTime`/`ScheduleEndTime` reguły wartościami `LightScheduleStart`/`LightScheduleEnd` z `Plant.Parameters` — jedno miejsce konfiguracji na roślinę, więc światło nie zapali się np. w środku nocy niezależnie od tego, która reguła nim steruje.

### 17.2 Parametry rośliny: godziny dozwolone dla światła

`Plant.Parameters` (`apps/Server/src/Models/Parameters.cs`) ma nowe pola `LightScheduleStart`/`LightScheduleEnd` (nullable `TimeSpan`) — okno godzinowe, w którym automatyka może włączać światło (np. żeby nie przeszkadzać we śnie). Ustawiane w tym samym miejscu co reszta parametrów rośliny.

Front: `PlantParametersSeter` (`apps/Client/src/components/PlantParametersSeter`) ma dwa pola `<input type="time">` ("Od"/"Do") pod suwakiem godzin światła dziennego oraz przycisk "Bez ograniczenia" czyszczący oba pola (brak ograniczenia = światło może się załączyć o dowolnej porze). Zmiany lecą razem z resztą `plant.parameters` przez istniejący `PUT /api/users/{userId}/plants/{id}`.

### 17.3 Rule Engine (ClientServer)

`AutomationRuleEngine` (`apps/ClientServer/src/Services/AutomationRuleEngine.cs`) — czysta, testowalna logika ewaluacji, używana przez `IoTControllerBackgroundService` w każdym cyklu pollingu zamiast starego kodu wybierającego "pierwszą roślinę z niską wilgotnością":

1. Dla każdej włączonej reguły sprawdza kolejno: harmonogram godzinowy → cooldown → wartość czujnika z bieżącej telemetrii → warunek (`LessThan`/`LessOrEqual`/`GreaterThan`/`GreaterOrEqual`) względem progu.
2. **Czas pracy aktuatora jest dynamiczny**, a nie sztywny: dla wszystkiego poza światłem (pompy, itd.) silnik liczy różnicę między odczytem czujnika a progiem reguły (`gap`) i dzieli ją przez `EffectStrength` urządzenia (ile jednostek parametru zmienia jedna sekunda pracy), zaokrąglając w górę i przycinając do `[1s, DurationSeconds]`. Im dalej od celu, tym dłużej pracuje aktuator, ale nigdy dłużej niż ustawiony w regule bezpiecznik. Światło nie dawkuje — działa po prostu zgodnie z oknem harmonogramu przez skonfigurowany czas.
3. **Rozstrzyganie konfliktów** dla aktuatorów współdzielonych przez wiele roślin (typowo światło/ogrzewanie — pompa jest zawsze 1:1 z rośliną, więc konfliktu praktycznie nie ma): reguły grupowane są po `ActuatorDeviceId`; jeśli w danym cyklu spełni się więcej niż jedna reguła dla tego samego urządzenia, wygrywa ta o niższej wartości `Priority`, a przy remisie — ta, która czekała najdłużej od ostatniego zadziałania (`LastTriggeredUtc`, nigdy nietriggerowana = czeka najdłużej), żeby żadna roślina nie była trwale zagłodzona przez inną z takim samym priorytetem.

Po wykonaniu akcji ClientServer publikuje komendę przez wspólny most MQTT (`MqttBridgeService.PublishActuatorCommandAsync`) i zgłasza wykonanie do Servera (`POST /{id}/trigger`), aktualizując cooldown. Ręczne sterowanie z UI i automatyzacja korzystają teraz z tej samej ścieżki MQTT.

