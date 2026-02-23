namespace AeriezAlert.Backend.Models;

/// <summary>
/// Configuration settings for connecting to the main Aeriez API
/// </summary>
public class AeriezApiSettings
{
    /// <summary>
    /// Base URL of the Aeriez API (e.g., http://localhost:5000/api)
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public API Token for tenant authentication
    /// </summary>
    public string PublicApiToken { get; set; } = string.Empty;

    /// <summary>
    /// How often to refresh the user list from the API (in minutes)
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 30;
}
