using System.Text.Json;
using System.Collections.Concurrent;
using AeriezAlert.Backend.Models;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

namespace AeriezAlert.Backend.Services;

public class UserSession 
{
    public string Identifier { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public DateTime LastSyncTime { get; set; }
}

/// <summary>
/// Service for looking up users and managing their active sessions
/// Implements persistence and smart expiration
/// </summary>
public class UserLookupService
{
    private readonly HttpClient _httpClient;
    private readonly AeriezApiSettings _settings;
    private readonly ILogger<UserLookupService> _logger;
    private const string PersistenceFile = "active_users.json";
    private const string TokenFile = "api_token.dat";
    
    // Configurable expiration days (e.g., 7 days)
    private const int InactivityThresholdDays = 7;
    
    // Key: Identifier (Email/Phone), Value: UserSession
    private readonly ConcurrentDictionary<string, UserSession> _activeUsers = new();

    // Cache of valid users from API
    private HashSet<string> _validUsers = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastValidUsersSync = DateTime.MinValue;

    private readonly IDataProtector _protector;

    // Event triggered when a user becomes active or pings
    public event Action? OnUserActivityDetected;

    public UserLookupService(
        HttpClient httpClient,
        IOptions<AeriezApiSettings> settings,
        ILogger<UserLookupService> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector("AeriezAlert.ApiTokenProtector");

        // Configure HttpClient base address and default headers
        var baseUrl = _settings.BaseUrl.EndsWith("/") ? _settings.BaseUrl : _settings.BaseUrl + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        // Load token from Disk if it exists, otherwise use AppSettings.
        var token = LoadToken();
        if (string.IsNullOrEmpty(token))
        {
             _logger.LogWarning("[UserLookup] API Token not configured. Waiting for configuration via UI.");
        }
        else
        {
             SetHttpClientToken(token);
            // Fire and forget initial sync if we have a token
            _ = RefreshValidUsersAsync();
        }

        LoadSessions();
    }

    private string LoadToken()
    {
        if (File.Exists(TokenFile))
        {
             try
             {
                 var encrypted = File.ReadAllText(TokenFile).Trim();
                 return _protector.Unprotect(encrypted);
             }
             catch (Exception ex)
             {
                 _logger.LogWarning(ex, "[UserLookup] Failed to decrypt API token. It may be missing, corrupt, or created with a different key.");
             }
        }
        return string.Empty; 
    }

    private void SetHttpClientToken(string token)
    {
         _httpClient.DefaultRequestHeaders.Remove("Authorization");
         _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public bool IsTokenConfigured()
    {
         return !string.IsNullOrEmpty(LoadToken());
    }

    public async Task<bool> ValidateAndSetTokenAsync(string token)
    {
         if(string.IsNullOrWhiteSpace(token)) return false;
         token = token.Trim();

         // Save original to restore if needed
         var originalAuth = _httpClient.DefaultRequestHeaders.Authorization;
         
         SetHttpClientToken(token);

         try
         {
              // Test the token
              var response = await _httpClient.GetAsync("open-api/users");
              if (response.IsSuccessStatusCode)
              {
                   // It works, save it securely and refresh users
                   var encrypted = _protector.Protect(token);
                   await File.WriteAllTextAsync(TokenFile, encrypted);
                   _logger.LogInformation("[UserLookup] New API Token verified and saved securely.");
                   
                   var json = await response.Content.ReadAsStringAsync();
                   var users = JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                   
                   if (users != null)
                   {
                       lock(_validUsers)
                       {
                           _validUsers = new HashSet<string>(users, StringComparer.OrdinalIgnoreCase);
                       }
                       _lastValidUsersSync = DateTime.UtcNow;
                       _logger.LogInformation($"[UserLookup] Synced {_validUsers.Count} valid users with new token.");
                   }
                   return true;
              }
         }
         catch(Exception ex)
         {
              _logger.LogError(ex, "[UserLookup] Error validating new API token.");
         }

         // Failed, revert
         _httpClient.DefaultRequestHeaders.Authorization = originalAuth;
         return false;
    }

    private async Task RefreshValidUsersAsync()
    {
        if (!IsTokenConfigured()) return;
        try
        {
            _logger.LogInformation("[UserLookup] Refreshing valid users list from API...");
            var response = await _httpClient.GetAsync("open-api/users");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (users != null)
                {
                    lock(_validUsers)
                    {
                        _validUsers = new HashSet<string>(users, StringComparer.OrdinalIgnoreCase);
                    }
                    _lastValidUsersSync = DateTime.UtcNow;
                    
                    var usersListString = string.Join(", ", _validUsers);
                    _logger.LogInformation($"[UserLookup] Fetched Users: {usersListString}");
                    _logger.LogInformation($"[UserLookup] Synced {_validUsers.Count} valid users.");
                }
            }
            else
            {
                _logger.LogWarning($"[UserLookup] Failed to sync users. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserLookup] Error refreshing valid users list");
        }
    }

    private void LoadSessions()
    {
        try 
        {
            if (File.Exists(PersistenceFile))
            {
                var json = File.ReadAllText(PersistenceFile);
                var sessions = JsonSerializer.Deserialize<Dictionary<string, UserSession>>(json);
                if (sessions != null)
                {
                    foreach (var kvp in sessions)
                    {
                        _activeUsers.TryAdd(kvp.Key, kvp.Value);
                    }
                    _logger.LogInformation($"[UserLookup] Loaded {_activeUsers.Count} active sessions from disk.");
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error loading user sessions. Deleting corrupt file.");
            try { File.Delete(PersistenceFile); } catch { }
        }
    }

    private void SaveSessions()
    {
        try 
        {
            var json = JsonSerializer.Serialize(_activeUsers);
            File.WriteAllText(PersistenceFile, json);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error saving user sessions");
        }
    }

    /// <summary>
    /// Registers a user as active when they ping the service via MQTT
    /// Validates user against cached list, refreshing if necessary
    /// </summary>
    public async Task<bool> RegisterActiveUserAsync(string identifier, DateTime? clientTimestamp = null)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return false;

        identifier = identifier.Trim();

        // Check if user is valid
        bool isValid = false;
        lock (_validUsers)
        {
            isValid = _validUsers.Contains(identifier);
        }

        // If not found, force a refresh and check again (maybe new user)
        if (!isValid)
        {
            _logger.LogInformation($"[UserLookup] User {identifier} not in cache. Refreshing...");
            await RefreshValidUsersAsync();
            lock (_validUsers)
            {
                isValid = _validUsers.Contains(identifier);
            }
        }

        if (!isValid)
        {
            _logger.LogWarning($"[UserLookup] Blocked invalid user: {identifier}");
            return false;
        }

        var now = DateTime.UtcNow;

        _activeUsers.AddOrUpdate(identifier, 
            id => new UserSession 
            { 
                Identifier = id, 
                LastSeen = now, 
                LastSyncTime = clientTimestamp ?? now 
            }, 
            (key, existing) => 
            {
                existing.LastSeen = now;
                // Do NOT update LastSyncTime on Ping. 
                // We only update it when we actually send notifications.
                // This ensures we don't skip notifications generated while the app was closed.
                return existing;
            });

        _logger.LogInformation($"[UserLookup] Registered/Updated session: {identifier}");
        SaveSessions(); // Persist changes
        
        // Notify Daemon to wake up
        OnUserActivityDetected?.Invoke();
        return true;
    }
    
    // Kept for compatibility if needed, but synchronous 
    public void RegisterActiveUser(string identifier, DateTime? clientTimestamp = null)
    {
        // Fire and forget async version
        _ = RegisterActiveUserAsync(identifier, clientTimestamp);
    }
    

    public void RemoveActiveUser(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return;
        identifier = identifier.Trim();

        if (_activeUsers.TryRemove(identifier, out _))
        {
            _logger.LogInformation($"[UserLookup] Removed session: {identifier}");
            SaveSessions();
        }
    }
    
    public void UpdateLastSyncTime(string identifier, DateTime timestamp)
    {
        if (_activeUsers.TryGetValue(identifier, out var session))
        {
            session.LastSyncTime = timestamp;
            SaveSessions();
        }
    }

    public UserSession? GetSession(string identifier)
    {
        if (_activeUsers.TryGetValue(identifier, out var session))
        {
            return session;
        }
        return null;
    }

    /// <summary>
    /// Gets the list of currently active users to check for notifications
    /// Filters out inactive users based on threshold
    /// </summary>
    public List<string> GetActiveUsers()
    {
        var cutoff = DateTime.UtcNow.AddDays(-InactivityThresholdDays);
        return _activeUsers.Values
            .Where(u => u.LastSeen > cutoff)
            .Select(u => u.Identifier)
            .ToList();
    }

    public List<string> GetValidUsers()
    {
        lock(_validUsers)
        {
            return _validUsers.ToList();
        }
    }



    public async Task<PhoneWithNotificationResult> GetNotificationsAsync(List<PhonesPings> pings)
    {
        try 
        {
            var pingsJson = JsonSerializer.Serialize(pings);
            // Caution: URL length limits apply here if pings list is large
            var url = $"open-api/notifications?json={Uri.EscapeDataString(pingsJson)}";
            
            var response = await _httpClient.GetAsync(url); 
            
            if (response.IsSuccessStatusCode)
            {
                 var json = await response.Content.ReadAsStringAsync();
                 return JsonSerializer.Deserialize<PhoneWithNotificationResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PhoneWithNotificationResult();
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications via API");
        }
        return new PhoneWithNotificationResult();
    }
}
