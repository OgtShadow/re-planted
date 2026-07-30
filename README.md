# re-planted

Projekt inżynierski do zarządzania domowym systemem roślin (frontend + backend + PostgreSQL + komunikacja realtime).

## 1. O projekcie

re-planted składa się z:

- aplikacji frontendowej (React + Vite),
- API backendowego (ASP.NET Core Minimal API),
- bazy PostgreSQL,
- komunikacji realtime przez SignalR.

Aktualna konfiguracja zawiera gotowy workflow uruchamiania przez Docker Compose oraz lokalnie (bez kontenerów).

## 2. Stack technologiczny

- Frontend: React 19, Vite, MUI, SignalR client
- Backend: .NET 8 (ASP.NET Core Minimal API), Entity Framework Core, Npgsql
- Baza danych: PostgreSQL 16
- Runtime deployment: Docker, Docker Compose, Nginx (dla frontendu)
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

VITE_API_BASE_URL=http://localhost:8081
```

Uwagi:

- `APP_PORT` i `CLIENT_PORT` są używane przez Docker Compose.
- `VITE_API_BASE_URL` przekazywane jest jako build-arg do obrazu frontendu Docker.
- Backend w kontenerze nasłuchuje na porcie `8080`.

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

5. Zatrzymanie usług:

```
docker compose down
```

Domyślne adresy po starcie:

- Frontend: http://localhost:8080
- Backend API: http://localhost:8081
- Swagger UI: http://localhost:8081/swagger
- OpenAPI JSON: http://localhost:8081/swagger/v1/swagger.json
- Sensor mock: http://localhost:8085/sensors
- PostgreSQL: localhost:5432

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
