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



        public async Task<PhoneWithNotificationResult> GetNotificationsGlobal(List<PhonesPings> inputPhones)
        {
             using (var scope = _serviceProvider.CreateScope())
             {
                 var userLookup = scope.ServiceProvider.GetRequiredService<UserLookupService>();
                 return await userLookup.GetNotificationsAsync(inputPhones);
             }
        }
    }
}
