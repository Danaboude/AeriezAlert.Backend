using System.Collections.Generic;

namespace AeriezAlert.Backend.Models
{
    public class PhoneWithNotificationResult
    {
        public List<PhoneWithNotifications> PhonesWithNotifications { get; set; } = new();
    }

    public class PhoneWithNotifications
    {
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public List<Notification> Notifications { get; set; } = new();
    }

    public class Notification
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty; // Added this based on requirement
    }

    public class ConnectionPingResult
    {
        public List<PhonesPings> Phones { get; set; } = new();
    }

    public class PhonesPings
    {
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public PhonesPingsAnswer Check { get; set; }
    }

    public enum PhonesPingsAnswer
    {
        none,
        notification,
        Unknown
    }
}
