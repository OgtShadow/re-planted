# re-planted
Graduation project

Aplication for managing your house plant automatized system made by Piotr Szerłomski.

Notion: https://www.notion.so/Re-planted-2b191ddb0b1180139f38d5eb8f4dd225

Stack:
Embeded-C-Arduino
Front-Js React
Back-C#

File structure:
# Struktura Plików
```
>apps
 |  >Server
 |  |  >src
 |  >Client
 |  |  >src
 |  |  >assets
 |  >IoTServer
 |  |  >src
 |  >IoT
 |  |  >src
 |  >Shared
>env.
>gitignore
>README.md
```

npm run . (w Cliencie)

## Docker

Uruchomienie całego projektu (frontend + backend) przez Docker Compose:

1. Build obrazów:
	docker compose -f docker-compose.yml build
2. Start kontenerów:
	docker compose -f docker-compose.yml up -d
3. Sprawdzenie statusu:
	docker compose -f docker-compose.yml ps

Domyślne adresy:

- Frontend: http://localhost:3000
- Backend API: http://localhost:5000

Zatrzymanie usług:

docker compose -f docker-compose.yml down

Jeśli używasz własnej bazy Oracle, ustaw zmienną REPLANTED_DB_CONNECTION_STRING przed uruchomieniem compose.
