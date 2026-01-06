using AeriezAlert.Backend.Models;

namespace AeriezAlert.Backend.Services;

public class UserLookupService
{
    private readonly List<User> _users;

    public UserLookupService()
    {
        // Mock Database of users
        _users = new List<User>
        {
            new User
            {
                PhoneNumber = "1234567890",
                CompanyId = "acme",
                UserId = "user123",
                GroupIds = new List<string> { "group-a", "group-b" }
            },
            new User
            {
                PhoneNumber = "9876543210",
                CompanyId = "acme",
                UserId = "user456",
                GroupIds = new List<string> { "group-c" }
            },
            new User
            {
                PhoneNumber = "5555555555",
                CompanyId = "beta",
                UserId = "user789",
                GroupIds = new List<string> { "all-staff" }
            }
        };
    }

    public User? GetUserByPhoneNumber(string phoneNumber)
    {
        return _users.FirstOrDefault(u => u.PhoneNumber == phoneNumber);
    }
}
