# Re-Planted - Harmonogram i Lista Zadań (Checklista Inżynierska)

### Faza 1: Środowisko i Infrastruktura (Docker & Baza Danych)
- [✅] Skonfigurowanie pliku `docker-compose.yml` (PostgreSQL 16, Nginx, .NET 8 Backend, React Frontend).
- [ ] Zdefiniowanie zewnętrznego brokera wiadomości MQTT w infrastrukturze.
- [✅] Zaprojektowanie relacyjnego schematu bazy danych (tabele m.in.: `Users`, `Devices`, `Telemetry`, `AutomationRules`).
- [ ] Utworzenie i uruchomienie migracji Entity Framework Core w .NET 8.

### Faza 2: Serwer Główny (C# / .NET 8 Minimal API)
- [✅] Zaimplementowanie endpointów CRUD dla zarządzania urządzeniami (ESP32) i regułami automatyzacji (wraz z automatyczną dokumentacją w Swaggerze).
- [✅] Wdrożenie mechanizmów autoryzacji i uwierzytelniania użytkowników (JWT).
- [ ] Integracja klienta MQTT w aplikacji .NET do subskrybowania tematów i nasłuchiwania telemetrii od IoT Controllera.
- [ ] Utworzenie usługi `BackgroundService` w C# do asynchronicznego zapisywania przetworzonych danych telemetrycznych w PostgreSQL.
- [ ] Skonfigurowanie Huba SignalR do bezpiecznej, dwukierunkowej komunikacji Web <-> Serwer w czasie rzeczywistym.
- [ ] Implementacja centralnego Silnika Reguł (Rule Engine) ewaluującego logikę IF-THEN na podstawie napływającej telemetrii z zabezpieczeniem przed kolizjami stanów.
- [ ] hashowanie haseł.

### Faza 3: IoT Controller (Serwer Klienta) - Warstwa Pośrednia
- [ ] Konfiguracja lokalnego środowiska wymiany wiadomości dla peryferiów domowych (bramka dystrybucyjna).
- [ ] Zbudowanie mechanizmu cache'owania – pobieranie i lokalne zapisywanie zatwierdzonych reguł z Głównego Serwera.
- [ ] Implementacja Trybu Offline na poziomie kontrolera – przejęcie autonomicznego zarządzania stanem urządzeń w przypadku utraty połączenia WAN (Fail-safe).

### Faza 4: Warstwa Sprzętowa i Firmware IoT (ESP32, C/C++)
**Inżynieria Sprzętowa (Hardware):**
- [✅] Zbudowanie obwodu czujników (DHT, wilgotność gleby, światło) z bezwzględnym zachowaniem limitu napięcia 3.3V dla pinów GPIO.
- [ ] Zintegrowanie modułu przekaźnika z pompą wody – wdrożenie fizycznego zabezpieczenia układu za pomocą diody Flyback (gaszącej) przeciwko prądom indukcyjnym.
- [ ] Podłączenie oświetlenia (taśma LED) i weryfikacja zapotrzebowania prądowego.
- [ ] Instalacja czujnika poziomu cieczy w zbiorniku buforowym.

**Oprogramowanie Mikrokontrolera (Firmware):**
- [ ] Implementacja pętli pomiarowej (polling) z zachowaniem przerw na ograniczenia czasowe czujników (np. interwały zapytań dla DHT11/22).
- [ ] Zastosowanie filtru cyfrowego (algorytm średniej kroczącej) dla pomiarów z przetworników ADC (wilgotność gleby i światło) w celu redukcji szumów i stabilizacji odczytów.
- [✅] Normalizacja przetworzonych wyników do formatu JSON przy użyciu `ArduinoJson` (wartości z czujników ADC w zakresie 0-1000, czujnik cieczy jako boolean `true`/`false`).
- [ ] Zbudowanie blokady programowej (interlock) zapobiegającej uruchomieniu pompy wody przy `false` z czujnika poziomu cieczy (dry-run protection).
- [ ] Oprogramowanie Watchdoga (sprzętowego/programowego) wyłączającego przekaźnik pompy po przekroczeniu krytycznego limitu czasu (zapobieganie przelaniu).
- [ ] Wdrożenie najniższego poziomu Trybu Offline na Client Server – utrzymanie cykli dobowych rośliny wyłącznie na podstawie wewnętrznej pamięci stanu w przypadku utraty pingu do lokalnego brokera.

### Faza 5: Frontend i Kokpit Użytkownika (React 19, Vite, MUI)
- [ ] Zestawienie i walidacja połączenia klienta SignalR w React dla nasłuchu zdarzeń z serwera na żywo.
- [✅] Utworzenie dynamicznego Dashboardu z kafelkami reprezentującymi status poszczególnych roślin i parametry środowiskowe.
- [ ] Opracowanie graficznego interfejsu (formularzy) do budowania i parametryzowania logiki automatyzacji przez użytkownika.
- [ ] Zaimplementowanie wykresów trendów dla historycznych danych telemetrycznych.
- [ ] Wdrożenie powiadomień typu "Toast/Alert" informujących o zdarzeniach krytycznych (np. niski poziom wody, awaria zasilania pompy).
- [ ] Zaprojektowanie modułu sterowania ręcznego (Override) z przyciskami natychmiastowego wymuszenia akcji (włącz/wyłącz pompę lub światło) z wizualną sygnalizacją asynchronicznego potwierdzenia zwrotnego z ESP32.

### Faza 6: Walidacja Systemu i Prace Badawcze (Dokumentacja)
- [ ] Wykonanie testów obciążeniowych i pomiar opóźnień (latency) pełnej pętli komunikacyjnej: Interfejs Webowy -> API -> MQTT -> ESP32 -> Przekaźnik.
- [ ] Symulacja awarii warstwowych – weryfikacja poprawności uruchamiania trybów Fail-safe w Kontrolerze IoT oraz bezpośrednio na Node'ach ESP32.