using System;
using System.Text.Json;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

/*
  FakeESP32 — MQTT Simulator
  ════════════════════════════
  Simulates the physical ESP32 smart bin by publishing random
  sensor readings to HiveMQ every 3 seconds.

  JSON published  : { "dustbin_level": 42, "hand_detected": true }
  Topic           : sensors/data          ← must match backend subscription
  Broker          : HiveMQ Cloud (TLS 8883)

  Run FIRST before starting the backend or opening the dashboard.
*/

class Program
{
    static async Task Main(string[] args)
    {
        var factory = new MqttFactory();
        var client  = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId("FakeESP32")
            .WithTcpServer("0edc9d366f354d1987fd42fad33222d2.s1.eu.hivemq.cloud", 8883)
            .WithCredentials("SmartBin", "Sm@rtBin_2026")
            .WithTls()
            .Build();

        // ── Reconnect on unexpected disconnect ───────────────────
        bool intentionalStop = false;

        client.DisconnectedAsync += async e =>
        {
            if (intentionalStop) return;
            Console.WriteLine("[MQTT] Disconnected — reconnecting in 3s...");
            await Task.Delay(3000);
            try   { await client.ConnectAsync(options); }
            catch { Console.WriteLine("[MQTT] Reconnect failed."); }
        };

        await client.ConnectAsync(options);
        Console.WriteLine("[FakeESP32] Connected to HiveMQ broker");
        Console.WriteLine("[FakeESP32] Publishing to topic: sensors/data");
        Console.WriteLine("[FakeESP32] Press Ctrl+C to stop.\n");

        var random = new Random();
        int packet = 0;

        // Simulate gradual fill cycles instead of pure random —
        // makes the dashboard easier to visually test.
        int  level     = 0;
        bool filling   = true;

        while (true)
        {
            // Slowly fill then empty the bin
            if (filling)
            {
                level += random.Next(1, 8);
                if (level >= 100) { level = 100; filling = false; }
            }
            else
            {
                level -= random.Next(5, 15);
                if (level <= 0)  { level = 0;   filling = true;  }
            }

            level = Math.Clamp(level, 0, 100);

            // Hand detected randomly ~30% of the time
            bool handDetected = random.Next(0, 10) < 3;

            // Build payload — field names MUST match C# SensorData class
            var data = new
            {
                dustbin_level = level,
                hand_detected = handDetected
            };

            string json = JsonSerializer.Serialize(data);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("sensors/data")
                .WithPayload(json)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await client.PublishAsync(message);

            packet++;
            Console.WriteLine($"[#{packet:D4}] Sent: {json}");

            await Task.Delay(3000);
        }
    }
}
