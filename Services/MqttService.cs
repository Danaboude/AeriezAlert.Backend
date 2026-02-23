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
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public MqttService(ILogger<MqttService> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        
        _mqttClient.ConnectedAsync += async e => {
            _logger.LogInformation("Connected to MQTT Broker.");
            
            // Subscribe to Ping topic
            await _mqttClient.SubscribeAsync("aeriez/ping");
            await _mqttClient.SubscribeAsync("aeriez/disconnect");
            _logger.LogInformation("Subscribed to aeriez/ping and aeriez/disconnect");
        };

        _mqttClient.DisconnectedAsync += e => {
            _logger.LogWarning($"Disconnected from MQTT Broker: {e.Reason}");
            return Task.CompletedTask;
        };

        _mqttClient.ApplicationMessageReceivedAsync += async e => {
            var topic = e.ApplicationMessage.Topic;
            
            if (topic == "aeriez/ping")
            {
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                _logger.LogInformation($"[MQTT] Received Ping: {payload}");

                try 
                {
                    string? identifier = null;
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var userLookup = scope.ServiceProvider.GetRequiredService<UserLookupService>();
                        var json = System.Text.Json.JsonDocument.Parse(payload);
                        
                        DateTime? clientTimestamp = null;
                        if (json.RootElement.TryGetProperty("timestamp", out var tsElement))
                        {
                            if (tsElement.TryGetDateTime(out var ts)) {
                                clientTimestamp = ts;
                            }
                        }

                        if (json.RootElement.TryGetProperty("identifier", out var idElement))
                        {
                            identifier = idElement.GetString();
                            if (!string.IsNullOrEmpty(identifier))
                            {
                                // Validate and register user against cached list
                                bool isRegistered = await userLookup.RegisterActiveUserAsync(identifier, clientTimestamp);
                                
                                if (!isRegistered)
                                {
                                    _logger.LogWarning($"[MQTT] Registration failed for {identifier} - User not found in allowed list.");
                                    identifier = null; // Clear identifier so we don't send ACK
                                }
                            }
                        }
                    }

                    // Send ACK back to user if we have a valid identifier
                    if (!string.IsNullOrEmpty(identifier))
                    {
                        var ackPayload = new 
                        { 
                            type = "pong", 
                            timestamp = DateTime.UtcNow,
                            message = "Connected successfully" // User requested friendly message
                        };
                        // user topic format: user/email/com
                        var userTopic = $"user/{identifier}"; 
                        await PublishAsync(userTopic, ackPayload);
                        _logger.LogInformation($"[MQTT] Sent Pong to {userTopic}");
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error processing ping message");
                }
            }
            else if (topic == "aeriez/disconnect")
            {
                var payload = e.ApplicationMessage.ConvertPayloadToString();
                _logger.LogInformation($"[MQTT] Received Disconnect: {payload}");

                try 
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var userLookup = scope.ServiceProvider.GetRequiredService<UserLookupService>();
                        var json = System.Text.Json.JsonDocument.Parse(payload);
                        
                        if (json.RootElement.TryGetProperty("identifier", out var idElement))
                        {
                            var identifier = idElement.GetString();
                            if (!string.IsNullOrEmpty(identifier))
                            {
                                userLookup.RemoveActiveUser(identifier);
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error processing disconnect message");
                }
            }
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
