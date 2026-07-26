### Faza 1: Rozbudowa Backendu (API) i Bezpieczeństwo
- [ ] Zaimplementowanie systemu autoryzacji oraz zarządzania użytkownikami w relacyjnej bazie danych.
- [ ] Skonfigurowanie tła systemowego (Background Services) w C# do ciągłego nasłuchiwania danych z czujników.
- [ ] Zbudowanie inteligentnego modułu walidacji reguł w celu zapobiegania kolizjom potrzeb sprzętowych roślin.

### Faza 2: Komunikacja Sieciowa i Architektura
- [ ] Wdrożenie protokołu MQTT z wykorzystaniem zewnętrznego brokera wiadomości.
- [ ] Stworzenie logiki "Serwera Klienta" rozdzielającego komunikaty z głównego serwera na peryferia domowe.
- [ ] Zaimplementowanie algorytmów cyfrowego filtrowania sygnałów (np. średnia krocząca) w celu eliminacji szumów pomiarowych.

### Faza 3: Warstwa Sprzętowa IoT (Mikrokontrolery ESP32/C++)
- [✅] Zaprogramowanie cyklicznych odczytów wilgotności gleby, temperatury, wilgotności powietrza i światła.
- [ ] Zbudowanie autonomicznego Trybu Offline (Fail-safe) zapamiętującego harmonogram w pamięci sprzętowej przy braku pingu.
- [ ] Wprowadzenie zabezpieczenia typu Watchdog, które rygorystycznie i sprzętowo limituje czas działania pompy wody.
- [ ] Podłączenie czujnika poziomu cieczy z programową blokadą działania (interlock) w przypadku pustego zbiornika buforowego.

### Faza 4: Frontend (React) i Kokpit Użytkownika
- [ ] Rozbudowa dynamicznego kokpitu (Dashboardu) o komponenty do asynchronicznego odświeżania danych z czujników.
- [ ] Dodanie interfejsu konfiguracyjnego dla reguł automatyzacji w oparciu o logikę warunkową IF-THEN.
- [ ] Zaprojektowanie i wdrożenie wykresów wizualizujących historyczne dane telemetryczne.
- [ ] Zaimplementowanie systemu powiadomień ostrzegającego alertami krytycznymi o konieczności uzupełnienia wody.
- [ ] Dodanie przycisków płynnego sterowania ręcznego z narzutem sieciowym poniżej ułamków sekund.

### Faza 5: Testy, Weryfikacja i Dokumentacja
- [ ] Przeprowadzenie testów opóźnień interfejsu klienckiego przy fizycznej komunikacji sprzętowej.
- [ ] Weryfikacja zachowania sprzętu i skuteczności wykonywania reguł w warunkach symulowanych awarii sieci.