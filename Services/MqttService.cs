using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AeriezAlert.Backend.Services;

public class MqttService : IDisposable
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly ILogger<MqttService> _logger;
    private readonly IConfiguration _configuration;

    /* 
       Old CloudAMQP Credentials (Kept for reference):
       private const string BrokerUrl = "goose.rmq2.cloudamqp.com";
       private const int BrokerPort = 8883;
       private const string Username = "mjzobrvj:mjzobrvj";
       private const string Password = "6Nny-gtuyC5e7bNn1s599fgKDrCUy_8d";
    */

    public MqttService(ILogger<MqttService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
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
        var brokerUrl = _configuration["MqttSettingsBrokerUrl"] ?? "localhost";
        var brokerPort = int.Parse(_configuration["MqttSettingsBrokerPort"] ?? "1883");
        var username = _configuration["MqttSettingsUsername"] ?? "guest";
        var password = _configuration["MqttSettingsPassword"] ?? "guest";

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerUrl, brokerPort)
            .WithCredentials(username, password)
            .WithCleanSession();

        // Only use TLS if port is not 1883 (standard non-secure MQTT port) or if explicitly configured
        // For local development with standard RabbitMQ docker (port 1883), TLS is usually disabled.
        if (brokerPort == 8883) 
        {
             clientOptionsBuilder.WithTlsOptions(
                 o => o.WithSslProtocols(SslProtocols.Tls12)
                       .UseTls()
             );
        }

        var clientOptions = clientOptionsBuilder.Build();

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions)
            .Build();

        await _mqttClient.StartAsync(managedOptions);
    }

    public async Task PublishAsync(string topic, object payload)
    {
        // Ensure topic uses slashes as per requirement
        // If the topic is just an email/phone, we might need to prefix it, but the caller usually sets the topic.
        // The caller (ConnectionController/NotificationController) should pass "user/{email}" or similar.
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
