using AeriezAlert.Backend.Models;

namespace AeriezAlert.Backend.Services;

public class DaemonService : BackgroundService
{
    private readonly ILogger<DaemonService> _logger;
    private readonly MqttService _mqttService;
    private readonly UserLookupService _userLookupService;
    private readonly PhoneNotificationService _phoneNotificationService;
    
    // Polling configuration
    private const int PollingIntervalMs = 15000;
    private bool _isRunning = true;
    private readonly HashSet<int> _sentNotificationIds = new();

    // Semaphore to handle waiting when no users are active.
    // Initial count 0 means it will block immediately if we wait on it.
    private readonly SemaphoreSlim _activitySemaphore = new(0);

    public bool IsRunning => _isRunning;

    public DaemonService(
        ILogger<DaemonService> logger, 
        MqttService mqttService, 
        UserLookupService userLookupService,
        PhoneNotificationService phoneNotificationService)
    {
        _logger = logger;
        _mqttService = mqttService;
        _userLookupService = userLookupService; // Injected directly (Singleton)
        _phoneNotificationService = phoneNotificationService; // Injected directly (Singleton)

        // Subscribe to activity events to wake up the daemon
        _userLookupService.OnUserActivityDetected += OnUserActivity;
    }

    private void OnUserActivity()
    {
        // If we are currently waiting on the semaphore, release it to wake up the loop.
        // CurrentCount == 0 means no slots are available (so we might be waiting).
        if (_activitySemaphore.CurrentCount == 0)
        {
            try 
            {
                _activitySemaphore.Release();
                _logger.LogInformation("[Daemon] Activity detected! Waking up...");
            }
            catch (SemaphoreFullException)
            {
                // Already released, ignore.
            }
        }
    }

    public void StartPolling()
    {
        if (_isRunning) return;
        _isRunning = true;
        _logger.LogInformation("Daemon Service started polling.");
        OnUserActivity(); // Wake up if we were stopped
    }

    public void StopPolling()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _logger.LogInformation("Daemon Service stopped polling.");
    }

    public override void Dispose()
    {
        _userLookupService.OnUserActivityDetected -= OnUserActivity;
        base.Dispose();
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

        _logger.LogInformation("Daemon Service is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isRunning)
            {
                // 1. Fetch Active Users
                var activeIdentifiers = _userLookupService.GetActiveUsers();

                if (activeIdentifiers.Count == 0)
                {
                    _logger.LogInformation("[Daemon] No active users. Sleeping until activity detected...");
                    // Wait indefinitely until OnUserActivity releases the semaphore
                    await _activitySemaphore.WaitAsync(stoppingToken);
                    continue; // Loop again to re-check
                }

                // If we have users, poll for notifications
                await PollForNewTickets(activeIdentifiers);

                // Wait for the polling interval before next cycle
                try 
                {
                    await Task.Delay(PollingIntervalMs, stoppingToken);
                }
                catch (TaskCanceledException) { break; }
            }
            else
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task PollForNewTickets(List<string> activeIdentifiers)
    {
        try
        {
            _logger.LogInformation("--- [Middleware Cycle Start] ---");

            var inputList = activeIdentifiers.Select(id => new PhonesPings 
            { 
                Email = id.Contains("@") ? id : null, 
                PhoneNumber = !id.Contains("@") ? id : null 
            }).ToList();

            _logger.LogInformation($"   -> Checking {inputList.Count} active users.");

            // 2. Fetch Notifications directly
             var notificationResult = await _phoneNotificationService.GetNotificationsGlobal(inputList);
            
            if (notificationResult.PhonesWithNotifications.Count > 0)
            {
                _logger.LogInformation($"   -> Found notifications for {notificationResult.PhonesWithNotifications.Count} users.");

                // 3. Publish MQTT
                foreach (var pwn in notificationResult.PhonesWithNotifications)
                {
                    var identifier = !string.IsNullOrEmpty(pwn.Email) ? pwn.Email : pwn.PhoneNumber;
                    if (string.IsNullOrEmpty(identifier)) continue;

                    // Get user session to check LastSyncTime
                    var session = _userLookupService.GetSession(identifier);
                    var lastSync = session?.LastSyncTime ?? DateTime.MinValue;
                    var maxTimestamp = lastSync;

                    int sentCount = 0;

                    foreach(var notif in pwn.Notifications)
                    {
                        // Filter by timestamp (CreatedAt)
                        if (notif.CreatedAt <= lastSync) continue;
                        if (_sentNotificationIds.Contains(notif.Id)) continue;

                        _sentNotificationIds.Add(notif.Id);
                        sentCount++;

                        var topicIdentifier = identifier.Replace(".", "/");
                        var topic = $"user/{topicIdentifier}";
                        
                        var message = new 
                        {
                            title = notif.Title,
                            body = notif.Message,
                            imageUrl = notif.ImageUrl,
                            actionUrl = notif.ActionUrl,
                            timestamp = notif.CreatedAt
                        };

                        await _mqttService.PublishAsync(topic, message);
                        
                        if (notif.CreatedAt > maxTimestamp)
                        {
                            maxTimestamp = notif.CreatedAt;
                        }
                    }

                    if (sentCount > 0)
                    {
                         _logger.LogInformation($"      -> Sent {sentCount} new notifications to {identifier}");
                         // Update Sync Time
                         if (maxTimestamp > lastSync)
                         {
                             _userLookupService.UpdateLastSyncTime(identifier, maxTimestamp);
                         }
                    }
                }
            }
            else
            {
                 _logger.LogInformation("   -> No new notifications.");
            }
             _logger.LogInformation("--- [Middleware Cycle End] ---");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during polling cycle.");
        }
        finally
        {
        }
    }
}
