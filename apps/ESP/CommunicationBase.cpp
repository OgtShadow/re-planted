#include <WiFi.h>
#include <WiFiManager.h>
#include <ESPmDNS.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <Preferences.h>

#define PUMP_PIN 23 
#define SOIL_MOISTURE_PIN 34
#define FILTER_WINDOW_SIZE 10

WiFiClient wifiClient;
PubSubClient mqttClient(wifiClient);
Preferences preferences;

String deviceId;
String configTopic;
String commandTopic;
String telemetryTopic;

int soilMoistureBuffer[FILTER_WINDOW_SIZE];
int bufferIndex = 0;

int wateringIntervalHours = 24;
int moistureMinThreshold = 300;
int telemetryIntervalSeconds = 60;
unsigned long lastTelemetryTime = 0;
unsigned long lastWateringTime = 0;

void setupTopics() {
    String mac = WiFi.macAddress();
    mac.replace(":", "");
    deviceId = "esp32-" + mac;
    
    configTopic = "replanted/node/" + deviceId + "/config";
    commandTopic = "replanted/commands/" + deviceId;
    telemetryTopic = "replanted/telemetry/sensor/" + deviceId;
}

int getFilteredMoisture() {
    int rawValue = analogRead(SOIL_MOISTURE_PIN);
    int normalizedValue = map(rawValue, 0, 4095, 0, 1000); 
    
    soilMoistureBuffer[bufferIndex] = normalizedValue;
    bufferIndex = (bufferIndex + 1) % FILTER_WINDOW_SIZE;
    
    int sum = 0;
    for (int i = 0; i < FILTER_WINDOW_SIZE; i++) {
        sum += soilMoistureBuffer[i];
    }
    return sum / FILTER_WINDOW_SIZE;
}

void loadOfflineSchedule() {
    preferences.begin("replanted", true);
    wateringIntervalHours = preferences.getInt("waterInt", 24);
    moistureMinThreshold = preferences.getInt("moistMin", 300);
    telemetryIntervalSeconds = preferences.getInt("telemetryInt", 60);
    preferences.end();
    Serial.println("Zaladowano konfiguracje Trybu Offline (Fail-safe) z NVS.");
}

void saveOfflineSchedule(int waterInt, int moistMin, int telInt) {
    preferences.begin("replanted", false);
    preferences.putInt("waterInt", waterInt);
    preferences.putInt("moistMin", moistMin);
    preferences.putInt("telemetryInt", telInt);
    preferences.end();
    
    wateringIntervalHours = waterInt;
    moistureMinThreshold = moistMin;
    telemetryIntervalSeconds = telInt;
    Serial.println("Zapisano nowa konfiguracje do pamieci NVS.");
}

void controlPump(int durationMs) {
    Serial.print("Uruchamianie pompy na ");
    Serial.print(durationMs);
    Serial.println(" ms.");
    
    digitalWrite(PUMP_PIN, HIGH);
    delay(durationMs);
    digitalWrite(PUMP_PIN, LOW);
    
    lastWateringTime = millis();
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
    JsonDocument doc;
    DeserializationError error = deserializeJson(doc, payload, length);
    
    if (error) {
        Serial.println("Blad parsowania JSON z MQTT.");
        return;
    }

    if (String(topic) == configTopic) {
        Serial.println("Odebrano nowa konfiguracje (Ack).");
        JsonObject schedule = doc["schedule"];
        saveOfflineSchedule(
            schedule["wateringIntervalHours"].as<int>(),
            schedule["soilMoistureMinThreshold"].as<int>(),
            schedule["telemetryIntervalSeconds"].as<int>()
        );
    } 
    else if (String(topic) == commandTopic) {
        String cmd = doc["command"].as<String>();
        bool state = doc["state"].as<bool>();
        int durationMs = doc["durationMs"].as<int>();
        
        if (cmd == "pump" && state) {
            controlPump(durationMs);
        }
    }
}

void sendRegistrationHandshake() {
    JsonDocument doc;
    doc["deviceId"] = deviceId;
    
    JsonArray capabilities = doc["capabilities"].to<JsonArray>();
    capabilities.add("soilMoistureAnalog");
    capabilities.add("pump");
    
    char buffer[256];
    serializeJson(doc, buffer);
    
    mqttClient.publish("replanted/discovery/register", buffer);
    Serial.println("Wyslano prosbe o rejestracje (Handshake).");
}

void sendTelemetry() {
    JsonDocument doc;
    doc["deviceId"] = deviceId;
    doc["sourceType"] = "sensor";
    doc["soilMoisture"] = getFilteredMoisture();
    doc["pumpState"] = false; 
    
    char buffer[256];
    serializeJson(doc, buffer);
    
    mqttClient.publish(telemetryTopic.c_str(), buffer);
    Serial.println("Wyslano telemetrie.");
}

void connectToMQTT() {
    Serial.println("Szukanie kontrolera mDNS _mqtt._tcp.local...");
    int n = MDNS.queryService("mqtt", "tcp");
    
    if (n > 0) {
        mqttClient.setServer(MDNS.IP(0), MDNS.port(0));
        mqttClient.setCallback(mqttCallback);
        
        if (mqttClient.connect(deviceId.c_str())) {
            Serial.println("Polaczono z brokerem MQTT.");
            mqttClient.subscribe(configTopic.c_str());
            mqttClient.subscribe(commandTopic.c_str());
            sendRegistrationHandshake();
        }
    } else {
        Serial.println("Nie znaleziono brokera przez mDNS.");
    }
}

void setup() {
    Serial.begin(115200);
    pinMode(PUMP_PIN, OUTPUT);
    digitalWrite(PUMP_PIN, LOW);
    
    for (int i = 0; i < FILTER_WINDOW_SIZE; i++) {
        soilMoistureBuffer[i] = 0;
    }

    WiFiManager wifiManager;
    wifiManager.setConfigPortalTimeout(180);
    if (!wifiManager.autoConnect("Re-Planted-Setup")) {
        Serial.println("Blad polaczenia WiFi. Przejscie w absolutny Tryb Offline.");
    } else {
        setupTopics();
        if (MDNS.begin(deviceId.c_str())) {
            connectToMQTT();
        }
    }
    
    loadOfflineSchedule();
}

void loop() {
    if (WiFi.status() == WL_CONNECTED) {
        if (!mqttClient.connected()) {
            connectToMQTT();
        }
        mqttClient.loop();
        
        unsigned long currentMillis = millis();
        if (currentMillis - lastTelemetryTime >= telemetryIntervalSeconds * 1000) {
            lastTelemetryTime = currentMillis;
            sendTelemetry();
        }
    } else {
        unsigned long currentMillis = millis();
        if (currentMillis - lastWateringTime >= wateringIntervalHours * 3600000UL) {
            if (getFilteredMoisture() < moistureMinThreshold) {
                Serial.println("Tryb Fail-Safe: Krytycznie niska wilgotnosc, awaryjne uruchomienie pompy.");
                controlPump(2000); 
            }
        }
    }
}