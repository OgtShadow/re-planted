# ArtificialEsp

Katalog zawiera dwa komponenty warstwy urządzeń:

- `main.go` - lokalny mock HTTP urządzenia używany do testów bez fizycznego ESP32.
- `firmware/RePlantedNode/RePlantedNode.ino` - bazowy firmware ESP32 dla lokalnej komunikacji MQTT.

## MQTT Firmware

Firmware używa:

- `WiFi.h`
- `PubSubClient`
- `ArduinoJson`

Topiki:

- Telemetria sensor: `replanted/telemetry/sensor/{deviceId}`
- Telemetria actuator: `replanted/telemetry/actuator/{deviceId}`
- Komendy: `replanted/commands/{deviceId}`

Obsługiwany format komendy:

```json
{
  "deviceId": "esp32-node-01",
  "command": "pump",
  "state": true,
  "durationMs": 2500,
  "requestedAtUtc": "2026-08-08T12:00:00Z"
}
```

Zaimplementowane mechanizmy bezpieczeństwa:

- interlock pompy zależny od stanu czujnika poziomu cieczy,
- dead-man switch z automatycznym wyłączeniem pompy po `durationMs`,
- moving average dla odczytów ADC (`soilMoisture`, `lightLevel`).

## Konfiguracja przed wgraniem firmware

W pliku `RePlantedNode.ino` ustaw:

- `wifiSsid`
- `wifiPassword`
- `mqttHost`
- `deviceId`

Dla logiki 3.3V:

- wszystkie piny sterujące muszą pracować wyłącznie w logice 3.3V,
- przekaźnik pompy musi być dobrany do sterowania z poziomu 3.3V,
- stan niski poziomu cieczy blokuje komendę uruchomienia pompy.
