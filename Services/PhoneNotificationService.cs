using AeriezAlert.Backend.Models;

namespace AeriezAlert.Backend.Services
{
    public class PhoneNotificationService
    {
        // In-memory store for connected/registered users
        // Key: Identifier (Email or Phone)
        private readonly HashSet<string> _registeredUsers = new();

        public void RegisterUser(string identifier)
        {
            lock (_registeredUsers)
            {
                if (!_registeredUsers.Contains(identifier))
                {
                    _registeredUsers.Add(identifier);
                }
            }
        }

        public ConnectionPingResult CheckPhones(List<PhonesPings> inputPhones)
        {
            var result = new ConnectionPingResult();
            
            foreach (var input in inputPhones)
            {
                var identifier = !string.IsNullOrEmpty(input.Email) ? input.Email : input.PhoneNumber;
                
                var response = new PhonesPings
                {
                    Email = input.Email,
                    PhoneNumber = input.PhoneNumber
                };

                bool isRegistered;
                lock(_registeredUsers)
                {
                    isRegistered = _registeredUsers.Contains(identifier);
                }

                if (!isRegistered)
                {
                    response.Check = PhonesPingsAnswer.Unknown;
                }
                else
                {
                    // Logic for "notification" vs "none" could depend on if there are pending notifications
                    // For now, if they are online, assume we can send notifications or they are ready.
                    // The prompt implies "Notification" might mean "User exists and we want to notify them" 
                    // or "User exists and has notifications".
                    // Let's assume if registered, we return 'notification' (meaning available to receive).
                    response.Check = PhonesPingsAnswer.notification;
                }
                
                result.Phones.Add(response);
            }

            return result;
        }

        public PhoneWithNotificationResult GetNotificationsGlobal(List<PhonesPings> inputPhones)
        {
             var result = new PhoneWithNotificationResult();

             foreach (var input in inputPhones)
             {
                 // Logic to actually compose the notification result
                 // In a real app, you'd fetch pending messages.
                 // Here we return an empty list or construct one if needed.
                 
                 var pwn = new PhoneWithNotifications
                 {
                     Email = input.Email,
                     PhoneNumber = input.PhoneNumber,
                     Notifications = new List<Notification>()
                 };
                 // Add logic here if we have queued notifications
                 
                 result.PhonesWithNotifications.Add(pwn);
             }

             return result;
        }
    }
}
