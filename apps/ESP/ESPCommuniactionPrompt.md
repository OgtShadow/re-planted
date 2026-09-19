Jesteś inżynierem systemów wbudowanych tworzącym oprogramowanie dla mikrokontrolera ESP32 w ramach rozproszonego systemu IoT "Re-Planted". Architektura systemu składa się z 4 warstw: Aplikacja Webowa -> Serwer Główny -> IoT Controller (Serwer Klienta) -> Urządzenia IoT (ESP32). Głównym założeniem systemu jest wysoka niezawodność (Fail-safe) oraz lokalne przetwarzanie danych w przypadku utraty połączenia.

Twoim zadaniem jest napisanie kompletnego, zoptymalizowanego kodu w C++ (framework Arduino) dla nowego modułu, do którego podłączone są następujące komponenty:
>[TUTAJ WPISZ SWOJE CZUJNIKI I URZĄDZENIA, np. czujnik poziomu wody na pinie 32, oświetlenie LED na pinie 21]<

Twój kod musi rygorystycznie przestrzegać poniższych założeń architektonicznych:

1. Zestawienie Połączenia (WiFi + mDNS):
Użyj biblioteki WiFiManager do wystawienia Captive Portal (Access Point), aby użytkownik mógł podać dane do Wi-Fi bez wpisywania ich w kodzie. Po połączeniu, użyj protokołu mDNS (biblioteka ESPmDNS), aby znaleźć w sieci lokalnej usługę _mqtt._tcp. Z tym adresem IP urządzenie musi połączyć się jako klient MQTT (np. PubSubClient).

2. Komunikacja MQTT, Rejestracja (Handshake) i Serializacja:
Używaj biblioteki ArduinoJson do budowania i parsowania wiadomości.
Po połączeniu z brokerem, ESP32 musi natychmiast wysłać zgłoszenie na temat replanted/discovery/register. Wymagany schemat serializacji:

C++
JsonDocument doc;
doc["deviceId"] = "esp32-MAC_ADRES";
JsonArray capabilities = doc["capabilities"].to<JsonArray>();
capabilities.add("nazwaCzujnika1"); // np. "soilMoistureAnalog", "waterLevelCm", "pump", "light"
capabilities.add("nazwaCzujnika2");
char buffer[256];
serializeJson(doc, buffer);
mqttClient.publish("replanted/discovery/register", buffer);
3. Nasłuch Komend i Konfiguracji:
Mikrokontroler musi subskrybować dwa tematy:

replanted/node/{deviceId}/config - stąd za pomocą deserializeJson należy odczytać obiekt "schedule" i zapisać jego parametry do pamięci nieulotnej NVS (biblioteka Preferences), co jest wymagane do działania Trybu Offline.

replanted/commands/{deviceId} - stąd przychodzą komendy manualne. Oczekiwany format:

C++
// Przykład parsowania komendy:
String cmd = doc["command"].as<String>();
bool state = doc["state"].as<bool>();
int durationMs = doc["durationMs"].as<int>();
4. Normalizacja Danych, Telemetria i Filtracja (Krytyczne):
Wszystkie odczyty z czujników analogowych (np. z przetwornika ADC) muszą być bezwzględnie mapowane do ustandaryzowanego przedziału 0 - 1000. Stany urządzeń wykonawczych przesyłaj jako true / false.

Surowe dane z ADC muszą przejść przez programowy filtr uśredniający (np. tablica cykliczna z 10 ostatnich pomiarów), aby wyeliminować szumy przed ich wysłaniem.

Przykład publikacji telemetrii na temat replanted/telemetry/sensor/{deviceId}:

C++
JsonDocument doc;
doc["deviceId"] = "esp32-MAC_ADRES";
doc["sourceType"] = "sensor";
doc["nazwaCzujnikaAnalogowego"] = zfiltrowanaWartosc; // Zawsze 0-1000
doc["nazwaUrzadzeniaWykonawczego"] = stanZmiennejBool; // true/false
serializeJson(doc, buffer);
5. Tryb Offline (Fail-safe):
Główna pętla programu (loop()) musi sprawdzać stan połączenia sieciowego. W przypadku braku dostępu do Wi-Fi/MQTT, układ musi wykorzystywać zapisane z NVS progi krytyczne oraz zegar systemowy (millis()), aby autonomicznie zarządzać podłączonymi urządzeniami (np. awaryjnie uruchomić pompę, gdy pomiar spadnie poniżej progu).

6. Zasady Inżynieryjne:
Pisz czysty kod, wszystkie zmienne, klasy i metody nazywaj w języku angielskim. Wszelkie logi na port szeregowy (Serial.println()) wypisuj wyłącznie w języku angielskim. Dodawaj komentarze do kodu. Zwróć mi tylko kompletny plik .cpp gotowy do kompilacji w Arduino IDE.