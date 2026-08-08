#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>

const char* wifiSsid = "REPLACE_WIFI_SSID";
const char* wifiPassword = "REPLACE_WIFI_PASSWORD";
const char* mqttHost = "192.168.1.50";
const int mqttPort = 1883;
const char* deviceId = "esp32-node-01";

const int pumpRelayPin = 26;
const int lampRelayPin = 27;
const int soilMoisturePin = 34;
const int lightPin = 35;
const int waterLevelPin = 14;

const unsigned long telemetryIntervalMs = 5000;
const int filterWindowSize = 12;
const int waterLevelLowState = LOW;

WiFiClient wifiClient;
PubSubClient mqttClient(wifiClient);

unsigned long lastTelemetryAtMs = 0;
unsigned long pumpAutoOffAtMs = 0;
bool pumpTimerActive = false;
bool pumpState = false;
bool lampState = false;

int soilBuffer[filterWindowSize];
int lightBuffer[filterWindowSize];
int filterIndex = 0;
bool filterWarmupDone = false;

void connectWifi()
{
  if (WiFi.status() == WL_CONNECTED)
  {
    return;
  }

  WiFi.mode(WIFI_STA);
  WiFi.begin(wifiSsid, wifiPassword);
  Serial.print("[INFO] Łączenie z siecią Wi-Fi");

  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(250);
    Serial.print(".");
    if (millis() - start > 20000)
    {
      Serial.println("\n[WARN] Przekroczono czas łączenia z Wi-Fi. Ponawiam próbę.");
      WiFi.disconnect();
      delay(500);
      WiFi.begin(wifiSsid, wifiPassword);
      start = millis();
    }
  }

  Serial.print("\n[INFO] Połączono z Wi-Fi. Adres IP: ");
  Serial.println(WiFi.localIP());
}

String commandsTopic()
{
  return String("replanted/commands/") + deviceId;
}

String sensorTelemetryTopic()
{
  return String("replanted/telemetry/sensor/") + deviceId;
}

String actuatorTelemetryTopic()
{
  return String("replanted/telemetry/actuator/") + deviceId;
}

bool isWaterLevelOk()
{
  return digitalRead(waterLevelPin) != waterLevelLowState;
}

void setPumpState(bool nextState)
{
  if (nextState && !isWaterLevelOk())
  {
    digitalWrite(pumpRelayPin, LOW);
    pumpState = false;
    pumpTimerActive = false;
    Serial.println("[ALARM] Odrzucono uruchomienie pompy. Zbyt niski poziom wody.");
    return;
  }

  digitalWrite(pumpRelayPin, nextState ? HIGH : LOW);
  pumpState = nextState;

  if (!nextState)
  {
    pumpTimerActive = false;
    Serial.println("[INFO] Pompa została wyłączona.");
    return;
  }

  Serial.println("[INFO] Pompa została włączona.");
}

void setLampState(bool nextState)
{
  digitalWrite(lampRelayPin, nextState ? HIGH : LOW);
  lampState = nextState;
  Serial.println(nextState ? "[INFO] Oświetlenie zostało włączone." : "[INFO] Oświetlenie zostało wyłączone.");
}

int mapToThousand(int raw)
{
  long mapped = map(raw, 0, 4095, 0, 1000);
  if (mapped < 0)
  {
    return 0;
  }
  if (mapped > 1000)
  {
    return 1000;
  }
  return (int)mapped;
}

int computeAverage(const int* buffer, int length)
{
  long sum = 0;
  for (int i = 0; i < length; i++)
  {
    sum += buffer[i];
  }
  return (int)(sum / length);
}

void updateAnalogFilters()
{
  int soilRaw = analogRead(soilMoisturePin);
  int lightRaw = analogRead(lightPin);

  soilBuffer[filterIndex] = mapToThousand(soilRaw);
  lightBuffer[filterIndex] = mapToThousand(lightRaw);

  filterIndex++;
  if (filterIndex >= filterWindowSize)
  {
    filterIndex = 0;
    filterWarmupDone = true;
  }
}

int readFilteredSoil()
{
  int length = filterWarmupDone ? filterWindowSize : max(1, filterIndex);
  return computeAverage(soilBuffer, length);
}

int readFilteredLight()
{
  int length = filterWarmupDone ? filterWindowSize : max(1, filterIndex);
  return computeAverage(lightBuffer, length);
}

void publishTelemetry()
{
  StaticJsonDocument<256> sensorDoc;
  sensorDoc["deviceId"] = deviceId;
  sensorDoc["sourceType"] = "sensor";
  sensorDoc["soilMoisture"] = readFilteredSoil();
  sensorDoc["lightLevel"] = readFilteredLight();
  sensorDoc["temperature"] = 0;
  sensorDoc["humidity"] = 0;
  sensorDoc["waterLevel"] = isWaterLevelOk() ? 1000 : 0;
  sensorDoc["waterLevelOk"] = isWaterLevelOk();
  sensorDoc["pumpState"] = pumpState;
  sensorDoc["lampState"] = lampState;

  char sensorBuffer[256];
  size_t sensorSize = serializeJson(sensorDoc, sensorBuffer, sizeof(sensorBuffer));
  bool sensorPublished = mqttClient.publish(sensorTelemetryTopic().c_str(), sensorBuffer, sensorSize);

  StaticJsonDocument<192> actuatorDoc;
  actuatorDoc["deviceId"] = deviceId;
  actuatorDoc["sourceType"] = "actuator";
  actuatorDoc["pumpState"] = pumpState;
  actuatorDoc["lampState"] = lampState;
  actuatorDoc["waterLevelOk"] = isWaterLevelOk();

  char actuatorBuffer[192];
  size_t actuatorSize = serializeJson(actuatorDoc, actuatorBuffer, sizeof(actuatorBuffer));
  bool actuatorPublished = mqttClient.publish(actuatorTelemetryTopic().c_str(), actuatorBuffer, actuatorSize);

  if (sensorPublished && actuatorPublished)
  {
    Serial.println("[INFO] Opublikowano telemetrię MQTT.");
  }
  else
  {
    Serial.println("[WARN] Nie udało się opublikować części telemetrii MQTT.");
  }
}

void processPumpDeadManSwitch()
{
  if (!pumpTimerActive)
  {
    return;
  }

  if ((long)(millis() - pumpAutoOffAtMs) >= 0)
  {
    setPumpState(false);
    Serial.println("[INFO] Automatyczne wyłączenie pompy po upływie durationMs.");
  }
}

void handleCommand(const String& payload)
{
  StaticJsonDocument<256> doc;
  DeserializationError error = deserializeJson(doc, payload);
  if (error)
  {
    Serial.println("[WARN] Odrzucono komendę MQTT. Niepoprawny JSON.");
    return;
  }

  const char* command = doc["command"] | "";
  bool state = doc["state"] | false;
  int durationMs = doc["durationMs"] | 0;

  if (String(command) == "pump")
  {
    if (!state)
    {
      setPumpState(false);
      return;
    }

    if (durationMs <= 0)
    {
      Serial.println("[WARN] Odrzucono komendę pompy. Brak poprawnego durationMs.");
      return;
    }

    setPumpState(true);
    if (pumpState)
    {
      pumpAutoOffAtMs = millis() + (unsigned long)durationMs;
      pumpTimerActive = true;
      Serial.print("[INFO] Ustawiono automatyczne wyłączenie pompy za ");
      Serial.print(durationMs);
      Serial.println(" ms.");
    }

    return;
  }

  if (String(command) == "lamp")
  {
    setLampState(state);
    return;
  }

  Serial.println("[WARN] Odrzucono komendę MQTT. Nieobsługiwany typ komendy.");
}

void mqttMessageCallback(char* topic, byte* payload, unsigned int length)
{
  String message;
  message.reserve(length);
  for (unsigned int i = 0; i < length; i++)
  {
    message += (char)payload[i];
  }

  Serial.print("[INFO] Otrzymano komendę z tematu ");
  Serial.println(topic);
  handleCommand(message);
}

void connectMqtt()
{
  mqttClient.setServer(mqttHost, mqttPort);
  mqttClient.setCallback(mqttMessageCallback);

  while (!mqttClient.connected())
  {
    String clientId = String("replanted-") + deviceId;
    Serial.print("[INFO] Łączenie z brokerem MQTT jako ");
    Serial.println(clientId);

    if (mqttClient.connect(clientId.c_str()))
    {
      Serial.println("[INFO] Połączono z brokerem MQTT.");
      bool subscribed = mqttClient.subscribe(commandsTopic().c_str(), 1);
      if (subscribed)
      {
        Serial.print("[INFO] Zasubskrybowano temat komend: ");
        Serial.println(commandsTopic());
      }
      else
      {
        Serial.println("[WARN] Nie udało się zasubskrybować tematu komend.");
      }
      return;
    }

    Serial.print("[WARN] Brak połączenia MQTT. Kod błędu: ");
    Serial.println(mqttClient.state());
    delay(2000);
  }
}

void setup()
{
  Serial.begin(115200);
  pinMode(pumpRelayPin, OUTPUT);
  pinMode(lampRelayPin, OUTPUT);
  pinMode(waterLevelPin, INPUT_PULLUP);

  setPumpState(false);
  setLampState(false);

  for (int i = 0; i < filterWindowSize; i++)
  {
    soilBuffer[i] = 0;
    lightBuffer[i] = 0;
  }

  connectWifi();
  connectMqtt();
}

void loop()
{
  if (WiFi.status() != WL_CONNECTED)
  {
    connectWifi();
  }

  if (!mqttClient.connected())
  {
    connectMqtt();
  }

  mqttClient.loop();
  updateAnalogFilters();
  processPumpDeadManSwitch();

  unsigned long now = millis();
  if (now - lastTelemetryAtMs >= telemetryIntervalMs)
  {
    lastTelemetryAtMs = now;
    publishTelemetry();
  }
}
