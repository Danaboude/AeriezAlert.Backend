using AeriezAlert.Backend.Models;

namespace AeriezAlert.Backend.Services
{
    public class PhoneNotificationService
    {
        // In-memory store for connected/registered users
        // Key: Identifier (Email or Phone)
        private readonly HashSet<string> _registeredUsers = new();
        private readonly IServiceProvider _serviceProvider;

        public PhoneNotificationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

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
            
            // Resolve scope to get UserLookupService (it's a Singleton but good practice to resolve if needed, though here we can likely inject directly if registered as Singleton)
            // Since we are in a Singleton service, and UserLookupService is Singleton, we can inject it in constructor. 
            // However, to avoid circular deps if any (there aren't yet), let's use provider or just inject it.
            // Let's use direct injection in constructor for simplicity if possible. 
            // But wait, I'm editing the class structure. I'll just use the provider pattern or better yet, change constructor.
            
            using (var scope = _serviceProvider.CreateScope())
            {
                var userLookup = scope.ServiceProvider.GetRequiredService<UserLookupService>();

                foreach (var input in inputPhones)
                {
                    var identifier = !string.IsNullOrEmpty(input.Email) ? input.Email : input.PhoneNumber;
                    
                    var response = new PhonesPings
                    {
                        Email = input.Email,
                        PhoneNumber = input.PhoneNumber
                    };

                    if (string.IsNullOrEmpty(identifier))
                    {
                        response.Check = PhonesPingsAnswer.Unknown;
                        result.Phones.Add(response);
                        continue;
                    }

                    // 1. Check if user exists in our "Database"
                    var user = identifier.Contains("@") 
                        ? userLookup.GetUserByEmail(identifier) 
                        : userLookup.GetUserByPhone(identifier);

                    if (user == null)
                    {
                        // User does not exist in our system -> Unknown
                        response.Check = PhonesPingsAnswer.Unknown;
                    }
                    else
                    {
                        // User exists. Return 'notification' (meaning "Known/Available")
                        // In the future, we might split this into "Online" vs "KnownButOffline". 
                        // For now, "notification" implies known valid user.
                        response.Check = PhonesPingsAnswer.notification;
                    }
                    
                    result.Phones.Add(response);
                }
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
