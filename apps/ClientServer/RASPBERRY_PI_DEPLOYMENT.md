# Wdrożenie ClientServer na Raspberry Pi

## Zalecany wariant: natywna aplikacja bez Dockera

ClientServer można uruchomić jako self-contained executable dla Debiana. Na Raspberry Pi nie trzeba wtedy
instalować .NET ani Dockera. Potrzebny jest tylko systemowy broker MQTT Mosquitto.

Na komputerze Windows, z katalogu repozytorium, dla Raspberry Pi 64-bit wykonaj:

```powershell
.\apps\ClientServer\scripts\publish-raspberrypi.ps1 -Runtime linux-arm64
```

Jeżeli urządzenie ma 32-bitowy system Raspberry Pi OS, użyj:

```powershell
.\apps\ClientServer\scripts\publish-raspberrypi.ps1 -Runtime linux-arm
```

Powstanie archiwum `apps/ClientServer/publish/re-planted-clientserver-linux-arm64.zip` albo wariant `linux-arm`.
Skopiuj je na Raspberry Pi i rozpakuj:

```powershell
scp .\apps\ClientServer\publish\re-planted-clientserver-linux-arm64.zip `
  pszerlomski@192.168.1.120:/home/pszerlomski/
```

Na Raspberry Pi:

```bash
sudo apt update
sudo apt install -y mosquitto mosquitto-clients unzip
sudo systemctl enable --now mosquitto
mkdir -p /home/pszerlomski/re-planted-clientserver
unzip -o /home/pszerlomski/re-planted-clientserver-linux-arm64.zip \
  -d /home/pszerlomski/re-planted-clientserver
cd /home/pszerlomski/re-planted-clientserver
chmod +x ClientServer
chmod +x install-native-debian.sh scripts/smoke-test.sh
```

Zainstaluj usługę i konfigurację:

```bash
sudo ./install-native-debian.sh /home/pszerlomski/re-planted-clientserver
sudo nano /etc/re-planted-clientserver/clientserver.env
```

W pliku środowiskowym sprawdź `MainServerApi__BaseUrl` i `ServerApi__BaseUrl`:

```text
http://192.168.1.67:8080
```

Sprawdź również, czy `Jwt__Key` jest identyczny jak na głównym Serverze. Następnie uruchom:

```bash
sudo systemctl restart re-planted-clientserver
sudo systemctl status re-planted-clientserver --no-pager
journalctl -u re-planted-clientserver -f
```

Aplikacja będzie dostępna pod `http://192.168.1.120:8082`. Logi zapisują się w `/opt/re-planted-clientserver/logs`,
a snapshot stanu w `/opt/re-planted-clientserver/data`. Aktualizację wykonuje się przez skopiowanie nowego
folderu publikacji i ponowne uruchomienie `install-native-debian.sh`.

Test po instalacji:

```bash
curl http://localhost:8082/api/client-server/health
curl http://localhost:8082/api/client-server/server-check
./scripts/smoke-test.sh http://localhost:8082 1 localhost 1883
```

Jeśli `ClientServer` nie startuje, sprawdź:

```bash
journalctl -u re-planted-clientserver -n 100 --no-pager
```

## Wariant Dockerowy (alternatywny)

Poniższa starsza część dokumentu opisuje wariant Dockerowy. Przy obecnym wdrożeniu natywnym nie używaj
`docker-compose.raspberrypi.yml`.

Ten dokument opisuje, jak przenieść `apps/ClientServer` na fizyczne urządzenie (Raspberry Pi) tak, aby
łączyło się z głównym `Server` uruchomionym gdzie indziej (np. w tym repozytorium na innym hoście),
miało kompletne logowanie do plików oraz dało się zweryfikować po wdrożeniu bez dostępu do IDE.

### Czego potrzebujesz na Raspberry Pi w wariancie Dockerowym

- Raspberry Pi OS (64-bit zalecane) lub inny Linux z jądrem obsługującym Dockera.
- Zainstalowany Docker Engine + wtyczka Docker Compose (`docker compose version`).
- Sieciowy dostęp (LAN, VPN lub port forwarding) do hosta, na którym działa główny `Server`.
- Ten sam sekret `Jwt:Key` co skonfigurowany na głównym `Server` — `ClientServer` podpisuje nim
  żądania do `Server` i bez zgodnego sekretu synchronizacja topologii/reguł zwróci 401/403.

Obraz bazowy `mcr.microsoft.com/dotnet/aspnet:8.0` jest wieloarchitekturowy (obsługuje `arm64`/`arm/v7`),
więc `docker build` uruchomiony bezpośrednio na Raspberry Pi automatycznie pobierze właściwy wariant.
Alternatywnie obraz można zbudować na PC przez `docker buildx build --platform linux/arm64 ...` i przesłać
go na urządzenie (`docker save` / `docker load` albo push do rejestru).

## 2. Skopiuj folder na urządzenie

Z repozytorium wystarczy przenieść na Raspberry Pi:

- `apps/ClientServer/` (cały folder z kodem, `Dockerfile`, `appsettings.json`),
- `infrastructure/mosquitto/mosquitto.conf` (jeśli broker MQTT też ma działać lokalnie na Pi),
- `docker-compose.raspberrypi.yml` (z korzenia repozytorium),
- `.env.raspberrypi.example` (z korzenia repozytorium).

Przykład (z maszyny deweloperskiej, przez `scp`):

```powershell
scp -r apps/ClientServer infrastructure docker-compose.raspberrypi.yml .env.raspberrypi.example pi@<adres-ip-pi>:/home/pi/re-planted/
```

## 3. Skonfiguruj połączenie z głównym Serverem

Na Raspberry Pi skopiuj `.env.raspberrypi.example` do `.env` obok `docker-compose.raspberrypi.yml`
i uzupełnij wartości:

```dotenv
MAIN_SERVER_BASE_URL=http://<adres-ip-lub-domena-glownego-servera>:8080
JWT_KEY=<dokladnie-ten-sam-sekret-co-na-glownym-serverze>
IOT_CLIENT_ID=<id-klienta-obslugiwanego-przez-to-urzadzenie>
```

`MAIN_SERVER_BASE_URL` musi być adresem osiągalnym z Raspberry Pi (np. adres LAN, tunel VPN lub domena
publiczna) — nazwa kontenera Dockera `app`, używana w `docker-compose.yml` do developmentu, nie zadziała
poza tamtą siecią Compose.

Compose Raspberry Pi ustawia `IoTController__TelemetrySource=Mqtt`. Oznacza to, że sterowanie nie korzysta
z HTTP mocka sensorów: kontroler wybiera najnowszą telemetrię MQTT z sensorów zapisanych w topologii
danego klienta. Każdy fizyczny ESP32 musi mieć `deviceId` dokładnie taki sam jak `ExternalDeviceId`
urządzenia sensorowego skonfigurowanego na głównym Serverze i publikować JSON na temacie:

```text
replanted/telemetry/<sourceType>/<deviceId>
```

Przykład:

```text
replanted/telemetry/esp32/esp32-node-01
```

Minimalny payload powinien zawierać pola `deviceId`, `soilMoisture`, `temperature`, `humidity`,
`waterLevel`, `waterLevelOk`, `pumpState`, `lampState` i `timestampUtc`. Komendy dla urządzenia
wracają na `replanted/commands/<deviceId>`. Po starcie sprawdź w logach wpis `Zasubskrybowano tematy MQTT`
oraz późniejsze `Odebrano telemetrię MQTT`.

## 4. Uruchom kontener

```bash
cd /home/pi/re-planted
docker compose -f docker-compose.raspberrypi.yml --env-file .env up -d --build
```

Domyślnie uruchamiane są dwa kontenery:

- `mosquitto` — lokalny broker MQTT dla ESP32 podłączonych do tego Raspberry Pi,
- `app_client` — `ClientServer` nasłuchujący na porcie `APP_CLIENT_PORT` (domyślnie `8082`).

Dane trwałe są montowane jako wolumeny z hosta:

- `./apps/ClientServer/data` → `/app/data` (snapshot topologii/reguł, `controller-state.json`),
- `./apps/ClientServer/logs` → `/app/logs` (logi Serilog, patrz sekcja 5).

Dzięki temu restart kontenera lub `docker compose down` nie usuwa historii ani stanu offline.

## 5. System logów

`ClientServer` używa Serilog skonfigurowanego w `appsettings.json` (sekcja `Serilog`):

- Konsola — widoczna przez `docker logs replanted_app_client` / `docker compose logs -f app_client`.
- Plik — `logs/clientserver-YYYYMMDD.log`, rotacja dzienna, limit 10 MB na plik, zachowywane 14 plików.

Aby zmienić poziom logowania bez przebudowy obrazu, ustaw zmienną środowiskową `LOG_LEVEL` w `.env`
(`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`) — mapowana jest na
`Serilog__MinimumLevel__Default` w `docker-compose.raspberrypi.yml`.

Podgląd logów na urządzeniu:

```bash
docker compose -f docker-compose.raspberrypi.yml logs -f app_client
tail -f apps/ClientServer/logs/clientserver-*.log
```

Kontener ma też zdefiniowany `HEALTHCHECK` (co 30s odpytuje `/api/client-server/health`), widoczny
w `docker ps` jako `healthy` / `unhealthy` / `starting`.

## 6. Weryfikacja po wdrożeniu

W `apps/ClientServer/scripts/` znajdują się gotowe skrypty smoke-testowe, które nie wymagają
zainstalowanego SDK .NET ani IDE — tylko `curl` (i opcjonalnie `nc` do sprawdzenia portu MQTT).

Na samym Raspberry Pi (bash):

```bash
chmod +x apps/ClientServer/scripts/smoke-test.sh
./apps/ClientServer/scripts/smoke-test.sh http://localhost:8082 1 localhost 1883
```

Zdalnie z maszyny deweloperskiej z Windows (PowerShell), podając adres IP Raspberry Pi:

```powershell
./apps/ClientServer/scripts/smoke-test.ps1 -ClientServerUrl "http://192.168.1.60:8082" -ClientId 1 -MqttHost "192.168.1.60" -MqttPort 1883
```

Skrypt sprawdza po kolei:

1. `GET /api/client-server/health` — czy `ClientServer` w ogóle wystartował.
2. `GET /api/client-server/server-check` — czy `ClientServer` widzi główny `Server` (test `/communication-test`).
3. `GET /api/client-server/controllers/{clientId}/status` — stan maszyny stanów pompy/soaku.
4. `GET /api/client-server/controllers/{clientId}/topology` — czy topologia roślin została zsynchronizowana.
5. `GET /api/client-server/controllers/{clientId}/telemetry/current` — czy telemetria MQTT dociera.
6. `GET /api/client-server/controllers/{clientId}/configuration` — ważność lokalnego snapshotu offline.
7. Dostępność portu brokera MQTT.

Kod wyjścia `0` oznacza wszystkie testy zielone; `1` oznacza co najmniej jedno niepowodzenie — treść
odpowiedzi nieudanego żądania jest wypisywana w konsoli, aby ułatwić diagnozę.

Do ręcznej, bardziej szczegółowej diagnostyki nadal można użyć kolekcji Bruno w
`apps/ClientServer/re-planted_clientserver_collection`, wskazując `client_server` na port urządzenia.

## 7. Częste problemy

| Objaw | Prawdopodobna przyczyna |
| --- | --- |
| `server-check` zwraca 502 | `MAIN_SERVER_BASE_URL` nieosiągalny z sieci Raspberry Pi (zły adres/firewall). |
| `sync` zwraca 502, `configuration` pokazuje wygasły snapshot | Główny `Server` osiągalny, ale `Jwt:Key` się nie zgadza — sprawdź logi `MainServerTopologyClient` (401/403). |
| Brak telemetrii mimo działającego MQTT | ESP32 publikuje na inny broker/temat niż `Mqtt__BrokerHost`/`Mqtt__TelemetryTopicFilter`; sprawdź `docker compose logs mosquitto`. |
| Kontener restartuje się w pętli | Sprawdź `docker compose logs app_client` oraz plik w `apps/ClientServer/logs/` — `Log.Fatal` przy starcie loguje pełny wyjątek przed zamknięciem procesu. |
| Snapshot `controller-state.json` znika po restarcie | Wolumen `./apps/ClientServer/data` nie został zamontowany — sprawdź `docker inspect replanted_app_client`. |
