# IoT Smart Bin ♻️🤖

An automated, IoT-enabled Smart Bin designed to reduce physical contact and optimize waste collection using an ESP32 microcontroller. 

## 🌟 Features

- **Touchless Operation:** Uses an ultrasonic sensor to detect a user's hand and automatically opens the lid via a servo motor, promoting better hygiene.
- **Real-time Capacity Monitoring:** A second ultrasonic sensor calculates the exact fill level of the trash (0-100%).
- **Cloud Integration:** The bin securely publishes real-time telemetry data (hand detection status, trash fill level) to the cloud using the MQTT protocol.
- **Software Integration:** Data is structured in JSON format, ready to be consumed by a C# desktop/web application for monitoring and analytics.

## 🛠️ Tech Stack

- **Hardware:** ESP32, 2x Ultrasonic Sensors (HC-SR04), Servo Motor
- **Connectivity:** WiFi, MQTT (HiveMQ Cloud)
- **Software:** C/C++ (Arduino IDE)

## 🚀 Getting Started

### Prerequisites
- [Arduino IDE](https://www.arduino.cc/en/software)
- ESP32 Board Package installed in Arduino IDE
- Libraries required: `WiFi`, `WiFiClientSecure`, `PubSubClient`, `ESP32Servo`

### Hardware Wiring
- **Hand Sensor:** TRIG (Pin 2), ECHO (Pin 4)
- **Trash Level Sensor:** TRIG (Pin 18), ECHO (Pin 19)
- **Servo Motor:** Signal (Pin 13)

### Configuration
1. Open `smartbin.ino` in the Arduino IDE.
2. Update the WiFi and MQTT configuration with your credentials:
```cpp
const char* ssid          = "YOUR_WIFI_SSID";
const char* wifi_password = "YOUR_WIFI_PASSWORD";

const char* mqtt_server   = "YOUR_MQTT_BROKER_URL"; 
const char* mqtt_user     = "YOUR_MQTT_USERNAME";
const char* mqtt_password = "YOUR_MQTT_PASSWORD";
