/*
  ================================================================
  Smart Bin — ESP32
  ================================================================
  Sensor 1 (TRIG1/ECHO1) : hand detection  → controls servo lid
  Sensor 2 (TRIG2/ECHO2) : trash fill level
  Publishes JSON to MQTT  : topic "sensors/data"

  JSON keys must match the C# SensorData class exactly:
    { "dustbin_level": 42, "hand_detected": true,
      "handDistance": 12.3, "trashDistance": 18.6 }
  ================================================================
*/

#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <PubSubClient.h>
#include <ESP32Servo.h>

// ═══════════════════════════════════════════════════════════════
//  CONFIG
// ═══════════════════════════════════════════════════════════════
const char* ssid          = "YOUR_WIFI_SSID";
const char* wifi_password = "YOUR_WIFI_PASSWORD";

const char* mqtt_server   = "YOUR_MQTT_BROKER_URL"; // e.g., 0edc9d366f354d1987fd42fad33222d2.s1.eu.hivemq.cloud
const int   mqtt_port     = 8883;
const char* mqtt_user     = "YOUR_MQTT_USERNAME";
const char* mqtt_password = "YOUR_MQTT_PASSWORD";

// ═══════════════════════════════════════════════════════════════
//  PINS
// ═══════════════════════════════════════════════════════════════
#define TRIG1      2    // Hand sensor — trigger
#define ECHO1      4    // Hand sensor — echo
#define TRIG2     18    // Trash level sensor — trigger
#define ECHO2     19    // Trash level sensor — echo
#define SERVO_PIN 13    // Servo signal pin (PWM capable)

// Bin geometry — measure your actual bin and adjust these:
#define BIN_EMPTY_CM  8.5f    // Distance reading when bin is empty
#define BIN_FULL_CM    2.0f    // Distance reading when bin is full

// Hand detection threshold
#define HAND_THRESHOLD_CM  15.0f

// ═══════════════════════════════════════════════════════════════
//  GLOBALS
// ═══════════════════════════════════════════════════════════════
WiFiClientSecure espClient;
PubSubClient     mqttClient(espClient);
Servo            servo;

// ═══════════════════════════════════════════════════════════════
//  WIFI
// ═══════════════════════════════════════════════════════════════
void setup_wifi() {
  Serial.print("[WiFi] Connecting");
  WiFi.begin(ssid, wifi_password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println();
  Serial.print("[WiFi] Connected — IP: ");
  Serial.println(WiFi.localIP());
}

// ═══════════════════════════════════════════════════════════════
//  MQTT RECONNECT
// ═══════════════════════════════════════════════════════════════
void reconnect() {
  while (!mqttClient.connected()) {
    Serial.print("[MQTT] Connecting...");
    if (mqttClient.connect("ESP32Client", mqtt_user, mqtt_password)) {
      Serial.println(" connected.");
    } else {
      Serial.print(" failed rc=");
      Serial.print(mqttClient.state());
      Serial.println(" — retrying in 2s");
      delay(2000);
    }
  }
}

// ═══════════════════════════════════════════════════════════════
//  ULTRASONIC DISTANCE
//  Returns distance in cm, or 999.0f on timeout (no echo).
// ═══════════════════════════════════════════════════════════════
float readDistance(int trig, int echo) {
  digitalWrite(trig, LOW);
  delayMicroseconds(2);
  digitalWrite(trig, HIGH);
  delayMicroseconds(10);
  digitalWrite(trig, LOW);

  long duration = pulseIn(echo, HIGH, 30000UL);
  if (duration == 0) return 999.0f;
  return (float)duration * 0.034f / 2.0f;
}

// ═══════════════════════════════════════════════════════════════
//  SETUP
// ═══════════════════════════════════════════════════════════════
void setup() {
  Serial.begin(115200);

  pinMode(TRIG1, OUTPUT);
  pinMode(ECHO1, INPUT);
  pinMode(TRIG2, OUTPUT);
  pinMode(ECHO2, INPUT);

  servo.attach(SERVO_PIN);
  servo.write(0);  // Lid closed on boot

  setup_wifi();

  espClient.setInsecure();
  mqttClient.setServer(mqtt_server, mqtt_port);
  mqttClient.setKeepAlive(60);

  Serial.println("[System] Ready");
}

// ═══════════════════════════════════════════════════════════════
//  LOOP
// ═══════════════════════════════════════════════════════════════
void loop() {
  if (!mqttClient.connected()) {
    reconnect();
  }
  mqttClient.loop();

  // ── Sensor 1: hand detection ──────────────────────────────
  float handDistance = readDistance(TRIG1, ECHO1);
  bool hand_detected = (handDistance < HAND_THRESHOLD_CM && handDistance != 999.0f);

  // ── Servo ─────────────────────────────────────────────────
  servo.write(hand_detected ? 90 : 0);

  // ── Sensor 2: trash fill level ────────────────────────────
  float trashDistance = readDistance(TRIG2, ECHO2);

  // FIX: int — matches C# SensorData public int dustbin_level
  // Calculation stays float internally for accuracy, cast to int at the end
  int dustbin_level = (int)((BIN_EMPTY_CM - trashDistance) / (BIN_EMPTY_CM - BIN_FULL_CM) * 100.0f);
  dustbin_level = constrain(dustbin_level, 0, 100);

  // ── JSON payload ──────────────────────────────────────────
  String payload;
  payload.reserve(120);
  payload  = "{";
  payload += "\"dustbin_level\":"  ; payload += dustbin_level;          // int, no decimals
  payload += ",\"hand_detected\":"; payload += (hand_detected ? "true" : "false");
  payload += ",\"handDistance\":" ; payload += String(handDistance,  1);
  payload += ",\"trashDistance\":"; payload += String(trashDistance, 1);
  payload += "}";

  // ── Publish ───────────────────────────────────────────────
  bool published = mqttClient.publish("sensors/data", payload.c_str());

  Serial.print(published ? "[OK] " : "[FAIL] ");
  Serial.println(payload);

  delay(1000);
}
