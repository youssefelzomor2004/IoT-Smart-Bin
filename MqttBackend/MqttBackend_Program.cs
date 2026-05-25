/*
  SmartBin MQTT Backend
  ══════════════════════
  Subscribes to HiveMQ "sensors/data", parses JSON,
  then forwards to the SignalR server at localhost:5013.

  IMPORTANT: Start SignalR server BEFORE this program.
*/

using System;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using System.Net.Http.Json;

enum BinState { Empty, Normal, AlmostFull, Full }

class Program
{
    static readonly HttpClient http = new HttpClient();
    static bool _intentionalDisconnect = false;

    static async Task Main(string[] args)
    {
        var factory    = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId("SmartBinBackend")
            .WithTcpServer("0edc9d366f354d1987fd42fad33222d2.s1.eu.hivemq.cloud", 8883)
            .WithCredentials("SmartBin", "Sm@rtBin_2026")
            // FIX: WithTls() is obsolete in MQTTnet 4.3+ — use WithTlsOptions instead
            .WithTlsOptions(o => o.UseTls())
            .Build();

        mqttClient.ConnectedAsync += async e =>
        {
            Console.WriteLine("[MQTT] Connected to broker");
            await mqttClient.SubscribeAsync("sensors/data");
            Console.WriteLine("[MQTT] Subscribed to sensors/data");
        };

        mqttClient.DisconnectedAsync += async e =>
        {
            if (_intentionalDisconnect) return;
            Console.WriteLine("[MQTT] Disconnected — reconnecting in 5s...");
            await Task.Delay(5000);
            try   { await mqttClient.ConnectAsync(options); }
            catch { Console.WriteLine("[MQTT] Reconnect failed."); }
        };

        mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            // FIX: MQTTnet 4.3+ deprecates .Payload — use .PayloadSegment (ArraySegment<byte>)
            var seg = e.ApplicationMessage?.PayloadSegment;
            if (seg == null || seg.Value.Count == 0) return;

            var message = Encoding.UTF8.GetString(seg.Value.Array!, seg.Value.Offset, seg.Value.Count);
            Console.WriteLine($"[MQTT IN] {message}");

            try
            {
                var data = JsonSerializer.Deserialize<SensorData>(message);
                if (data == null) return;

                var state = GetBinState(data);
                Dispatch(data, state);

                var response = await http.PostAsJsonAsync(
                    "http://localhost:5013/update",
                    new BinUpdate
                    {
                        Level = data.dustbin_level,
                        Hand  = data.hand_detected,
                        State = state.ToString()
                    });

                Console.WriteLine(response.IsSuccessStatusCode
                    ? "[SignalR] Forwarded OK"
                    : $"[SignalR] HTTP error {(int)response.StatusCode}");
            }
            catch (JsonException jex)
            {
                Console.WriteLine($"[JSON Error] {jex.Message}  Raw: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.GetType().Name}: {ex.Message}");
            }
        };

        await mqttClient.ConnectAsync(options);

        Console.WriteLine("Running. Press Enter to exit...");
        Console.ReadLine();

        _intentionalDisconnect = true;
        await mqttClient.DisconnectAsync();
        Console.WriteLine("Disconnected cleanly.");
    }

    static BinState GetBinState(SensorData data)
    {
        if (data.dustbin_level >= 90) return BinState.Full;
        if (data.dustbin_level >= 60) return BinState.AlmostFull;
        if (data.dustbin_level >= 10) return BinState.Normal;
        return BinState.Empty;
    }

    static void Dispatch(SensorData data, BinState state)
    {
        Console.WriteLine($"[State] {state} | Level: {data.dustbin_level}%");
        if (state == BinState.Full)
        {
            Console.WriteLine("  WARNING: Bin full — lid locked closed.");
            return;
        }
        Console.WriteLine(data.hand_detected ? "  -> Lid OPEN" : "  -> Lid CLOSED");
    }
}

class SensorData
{
    public int  dustbin_level { get; set; }
    public bool hand_detected { get; set; }
}

class BinUpdate
{
    public int    Level { get; set; }
    public bool   Hand  { get; set; }
    public string State { get; set; } = string.Empty;
}
