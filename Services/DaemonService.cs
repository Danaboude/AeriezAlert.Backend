using AeriezAlert.Backend.Models;

namespace AeriezAlert.Backend.Services;

public class DaemonService : BackgroundService
{
    private readonly ILogger<DaemonService> _logger;
    private readonly MqttService _mqttService;
    private readonly IServiceProvider _serviceProvider;
    
    // Polling configuration
    private const int PollingIntervalMs = 15000;
    private bool _isRunning = false;
    private bool _isPolling = false;

    public bool IsRunning => _isRunning;

    public DaemonService(ILogger<DaemonService> logger, MqttService mqttService, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _mqttService = mqttService;
        _serviceProvider = serviceProvider;
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
            _logger.LogInformation("--- [Mock Middleware Cycle Start] ---");

            // 1. Define Mock Input List (Mixed valid and invalid users)
            // In production, this comes from your API source.
            var mockInputList = new List<PhonesPings>
            {
                new PhonesPings { Email = "user1@acme.com" }, // Valid (Mock DB)
                new PhonesPings { Email = "unknown@test.com" }, // Invalid
                new PhonesPings { PhoneNumber = "1234567890" } // Invalid/Unknown
            };

            _logger.LogInformation($"1. Input List: {mockInputList.Count} entries.");

            using (var scope = _serviceProvider.CreateScope())
            {
                var phoneService = scope.ServiceProvider.GetRequiredService<PhoneNotificationService>();

                // 2. Step 1: Ping / CheckPhones
                // "The API will then return a ConnectionPingResult."
                var pingResult = phoneService.CheckPhones(mockInputList);

                _logger.LogInformation($"2. Ping Result: {pingResult.Phones.Count} checked.");

                // 3. Step 2: Handle Unknowns & Filter
                // "You should send a notification to the unknown phones... filter out the None and Unknown"
                var validUsers = new List<PhonesPings>();

                foreach (var phoneResult in pingResult.Phones)
                {
                    var identifier = !string.IsNullOrEmpty(phoneResult.Email) ? phoneResult.Email : phoneResult.PhoneNumber;

                    if (phoneResult.Check == PhonesPingsAnswer.Unknown || phoneResult.Check == PhonesPingsAnswer.none)
                    {
                        // Mock "Sending Invite"
                        _logger.LogWarning($"   -> [Unknown User] Sending SMS/Invite to: {identifier}");
                    }
                    else if (phoneResult.Check == PhonesPingsAnswer.notification)
                    {
                        // Known user, add to filter list
                        validUsers.Add(new PhonesPings 
                        { 
                            Email = phoneResult.Email, 
                            PhoneNumber = phoneResult.PhoneNumber 
                        });
                        _logger.LogInformation($"   -> [Known User] Added to valid list: {identifier}");
                    }
                }

                if (validUsers.Count == 0)
                {
                     _logger.LogInformation("3. No valid users found to notify.");
                }
                else
                {
                    // 4. Step 3: Get Notifications for Knowns
                    // "send a new request with the filtered numbers... receive a PhoneWithNotificationResult"
                    _logger.LogInformation($"3. Requesting notifications for {validUsers.Count} valid users...");
                    
                    var notificationResult = phoneService.GetNotificationsGlobal(validUsers);

                    // 5. Step 4: Publish MQTT
                    // "send notifications to all remaining phones"
                    var random = new Random();
                    foreach (var pwn in notificationResult.PhonesWithNotifications)
                    {
                        var identifier = !string.IsNullOrEmpty(pwn.Email) ? pwn.Email : pwn.PhoneNumber;
                        
                        // Simulate a message if we "have" one (Random chance for demo)
                        if (random.NextDouble() < 0.5) 
                        {
                            var topicIdentifier = identifier.Replace(".", "/");
                            var topic = $"user/{topicIdentifier}";
                            
                            // Generate realistic Ticket Status
                            var ticketId = random.Next(1000, 9999);
                            var isNewTicket = random.NextDouble() > 0.5;
                            var title = isNewTicket ? "New Ticket Assigned" : "Ticket Closed";
                            var body = isNewTicket 
                                ? $"Ticket #{ticketId} has been assigned to you. Please review the details." 
                                : $"Ticket #{ticketId} has been marked as resolved.";

                            var message = new 
                            {
                                title = title,
                                body = body,
                                imageUrl = "https://picsum.photos/200", // Demo Image
                                actionUrl = "https://google.com"
                            };


                            await _mqttService.PublishAsync(topic, message);
                            _logger.LogInformation($"4. [MQTT] Published to {topic}");
                        }
                        else
                        {
                            _logger.LogInformation($"4. [No New Message] for {identifier}");
                        }
                    }
                }
            }
             _logger.LogInformation("--- [Mock Middleware Cycle End] ---");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during polling cycle.");
        }
        finally
        {
            _isPolling = false;
            await Task.Delay(PollingIntervalMs);
        }
    }
}
