using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Security.Authentication;
using System.Text.Json;

namespace AeriezAlert.Backend.Services;

public class MqttService : IDisposable
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly ILogger<MqttService> _logger;

    // Hardcoded credentials as per prompt (usually should be in appsettings)
    private const string BrokerUrl = "goose.rmq2.cloudamqp.com";
    private const int BrokerPort = 8883;
    private const string Username = "mjzobrvj:mjzobrvj";
    private const string Password = "6Nny-gtuyC5e7bNn1s599fgKDrCUy_8d";

    public MqttService(ILogger<MqttService> logger)
    {
        _logger = logger;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _mqttClient.ConnectedAsync += e => {
            _logger.LogInformation("Connected to MQTT Broker.");
            return Task.CompletedTask;
        };
        _mqttClient.DisconnectedAsync += e => {
            _logger.LogWarning($"Disconnected from MQTT Broker: {e.Reason}");
            return Task.CompletedTask;
        };
    }

    public async Task ConnectAsync()
    {
        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerUrl, BrokerPort)
            .WithCredentials(Username, Password)
            .WithTls(new MqttClientOptionsBuilderTlsParameters
            {
                UseTls = true,
                SslProtocol = SslProtocols.Tls12
            })
            .WithCleanSession()
            .Build();

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions)
            .Build();

        await _mqttClient.StartAsync(managedOptions);
    }

    public async Task PublishAsync(string topic, object payload)
    {
        // Ensure topic uses slashes as per requirement
        var safeTopic = topic.Replace(".", "/");
        var jsonPayload = JsonSerializer.Serialize(payload);

        // Build the message
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(safeTopic)
            .WithPayload(jsonPayload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.EnqueueAsync(message);
        _logger.LogInformation($"Published to {safeTopic}: {jsonPayload}");
    }

    public void Dispose()
    {
        _mqttClient?.Dispose();
    }
}
