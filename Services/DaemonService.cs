using AeriezAlert.Backend.Models;

namespace AeriezAlert.Backend.Services;

public class DaemonService : BackgroundService
{
    private readonly ILogger<DaemonService> _logger;
    private readonly MqttService _mqttService;
    
    // Polling configuration
    private const int PollingIntervalMs = 15000;
    private bool _isRunning = false;
    private bool _isPolling = false;

    public bool IsRunning => _isRunning;

    public DaemonService(ILogger<DaemonService> logger, MqttService mqttService)
    {
        _logger = logger;
        _mqttService = mqttService;
    }

    public void StartPolling()
    {
        if (_isRunning) return;
        _isRunning = true;
        _logger.LogInformation("Daemon Service started polling.");
    }

    public void StopPolling()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _logger.LogInformation("Daemon Service stopped polling.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connect to MQTT on startup of the service
        try
        {
            await _mqttService.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT on startup.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isRunning && !_isPolling)
            {
                await PollForNewTickets();
            }

            await Task.Delay(1000, stoppingToken); // Check every second if we should poll
        }
    }

    private async Task PollForNewTickets()
    {
        _isPolling = true;
        try
        {
             // Mock polling logic similar to the JS daemon
            _logger.LogInformation("Polling for new tickets...");

            // Simulate finding a ticket with 30% chance per poll cycle
            var random = new Random();
            if (random.NextDouble() < 0.3) 
            {
                var ticketId = random.Next(1000, 9999);
                var isNew = random.NextDouble() > 0.5;
                var status = isNew ? "New Ticket Opened" : "Ticket Closed";
                var body = isNew 
                    ? $"A new ticket #{ticketId} has been assigned to you." 
                    : $"Ticket #{ticketId} has been closed.";

                _logger.LogInformation($"[Mock] Found update: {status}");

                // In a real app, we would fetch recipients. Here we mock sending to our test user.
                // We'll send to the specific test user 'user1@acme.com' used in the frontend default.
                // Or we can make this configurable, but for now we follow the "identifier" pattern.
                var targetIdentifier = "user1@acme.com";
                // Topic format: user/{identifier with . replaced by /}
                // e.g. user/user1@acme/com
                
                var safeIdentifier = targetIdentifier.Replace(".", "/");
                var topic = $"user/{safeIdentifier}";
                
                var message = new 
                {
                    title = status,
                    body = body
                };

                await _mqttService.PublishAsync(topic, message);
                _logger.LogInformation($"[Mock] Notification sent to {topic}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during polling cycle.");
        }
        finally
        {
            _isPolling = false;
            // Wait for the rest of the interval
        
            await Task.Delay(PollingIntervalMs);
        }
    }
}
