package main

import (
	_ "embed"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"sync"
	"time"
)

//go:embed swagger.yaml
var swaggerSpec []byte

const swaggerUIHTML = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Swagger UI - Re-Planted Mock</title>
  <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css" />
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
  <script>
    window.onload = () => {
      window.ui = SwaggerUIBundle({
        url: '/swagger.yaml',
        dom_id: '#swagger-ui',
      });
    };
  </script>
</body>
</html>`

type DeviceState struct {
	sync.RWMutex
	PumpOn       bool
	LightOn      bool
	WaterLevelCm int
}

type SensorData struct {
	DeviceID           string `json:"deviceId"`
	LightIsDark        bool   `json:"lightIsDark"`
	SoilMoistureAnalog int    `json:"soilMoistureAnalog"`
	Temperature        int    `json:"temperature"`
	Humidity           int    `json:"humidity"`
	WaterLevelCm       int    `json:"waterLevelCm"`
	PumpState          bool   `json:"pumpState"`
	LampState          bool   `json:"lampState"`
	Timestamp          string `json:"timestamp"`
}

type CommandPayload struct {
	State bool `json:"state"`
}

type WaterSimulationPayload struct {
	LevelCm int `json:"levelCm"`
}

type DeviceHandler struct {
	state *DeviceState
}

func NewDeviceHandler() *DeviceHandler {
	return &DeviceHandler{
		state: &DeviceState{
			PumpOn:       false,
			LightOn:      false,
			WaterLevelCm: 15,
		},
	}
}

func (h *DeviceHandler) handleDeviceState(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Nieobsługiwana metoda", http.StatusMethodNotAllowed)
		return
	}

	h.state.RLock()
	defer h.state.RUnlock()

	state := map[string]interface{}{
		"pumpOn":       h.state.PumpOn,
		"lightOn":      h.state.LightOn,
		"waterLevelCm": h.state.WaterLevelCm,
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	if err := json.NewEncoder(w).Encode(state); err != nil {
		log.Printf("[BŁĄD] Kodowanie JSON: %v", err)
	}
}

func (h *DeviceHandler) handleSensors(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Nieobsługiwana metoda", http.StatusMethodNotAllowed)
		return
	}

	h.state.RLock()
	currentWaterLevel := h.state.WaterLevelCm
	currentPumpState := h.state.PumpOn
	currentLightState := h.state.LightOn
	h.state.RUnlock()

	data := SensorData{
		DeviceID:           "esp32-test-node-01",
		LightIsDark:        true,
		SoilMoistureAnalog: 650,
		Temperature:        245,
		Humidity:           580,
		WaterLevelCm:       currentWaterLevel,
		PumpState:          currentPumpState,
		LampState:          currentLightState,
		Timestamp:          time.Now().UTC().Format(time.RFC3339),
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	if err := json.NewEncoder(w).Encode(data); err != nil {
		log.Printf("[BŁĄD] Kodowanie JSON: %v", err)
	}
}

func (h *DeviceHandler) handlePump(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Nieobsługiwana metoda", http.StatusMethodNotAllowed)
		return
	}

	var cmd CommandPayload
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		http.Error(w, "Nieprawidłowy format JSON", http.StatusBadRequest)
		return
	}

	h.state.Lock()
	defer h.state.Unlock()

	if cmd.State && h.state.WaterLevelCm <= 2 {
		log.Printf("[ALARM] Odrzucono próbę włączenia pompy - krytycznie niski poziom wody (%d cm)!", h.state.WaterLevelCm)
		http.Error(w, "Zbyt niski poziom wody. Operacja zablokowana.", http.StatusConflict)
		return
	}

	h.state.PumpOn = cmd.State
	stan := "WYŁĄCZONA"
	if cmd.State {
		stan = "WŁĄCZONA"
	}
	log.Printf("[INFO] Stan pompy zmieniony na: %s", stan)
	w.WriteHeader(http.StatusOK)
}

func (h *DeviceHandler) handleLight(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Nieobsługiwana metoda", http.StatusMethodNotAllowed)
		return
	}

	var cmd CommandPayload
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		http.Error(w, "Nieprawidłowy format JSON", http.StatusBadRequest)
		return
	}

	h.state.Lock()
	h.state.LightOn = cmd.State
	h.state.Unlock()

	stan := "WYŁĄCZONE"
	if cmd.State {
		stan = "WŁĄCZONE"
	}
	log.Printf("[INFO] Stan oświetlenia zmieniony na: %s", stan)
	w.WriteHeader(http.StatusOK)
}

func (h *DeviceHandler) handleWaterSimulation(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Nieobsługiwana metoda", http.StatusMethodNotAllowed)
		return
	}

	var cmd WaterSimulationPayload
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		http.Error(w, "Nieprawidłowy format JSON", http.StatusBadRequest)
		return
	}

	if cmd.LevelCm < 0 {
		cmd.LevelCm = 0
	}

	h.state.Lock()
	h.state.WaterLevelCm = cmd.LevelCm

	if h.state.WaterLevelCm <= 2 && h.state.PumpOn {
		h.state.PumpOn = false
		log.Printf("[SYSTEM] Pompa awaryjnie wyłączona - poziom wody spadł do %d cm!", h.state.WaterLevelCm)
	}
	h.state.Unlock()

	log.Printf("[INFO] Symulacja: Poziom wody ustawiony na = %d cm", cmd.LevelCm)
	w.WriteHeader(http.StatusOK)
}

func main() {
	handler := NewDeviceHandler()

	http.HandleFunc("/sensors", handler.handleSensors)
	http.HandleFunc("/command/pump", handler.handlePump)
	http.HandleFunc("/command/light", handler.handleLight)
	http.HandleFunc("/simulate/water-tank", handler.handleWaterSimulation)
	http.HandleFunc("/device-state", handler.handleDeviceState)

	http.HandleFunc("/swagger.yaml", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/x-yaml")
		w.Write(swaggerSpec)
	})

	http.HandleFunc("/swagger/", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "text/html")
		w.Write([]byte(swaggerUIHTML))
	})

	port := ":8085"
	fmt.Printf("[INFO] Uruchamianie serwera testowego IoT na porcie %s...\n", port)
	fmt.Printf("[INFO] Dokumentacja Swagger UI dostępna pod adresem: http://localhost%s/swagger/\n", port)
	if err := http.ListenAndServe(port, nil); err != nil {
		log.Fatalf("[BŁĄD KRYTYCZNY] Serwer zatrzymany: %v", err)
	}
}
