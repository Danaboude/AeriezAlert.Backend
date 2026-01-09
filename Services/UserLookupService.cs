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
                Email = "user1@acme.com",
                PhoneNumber = "1234567890",
                CompanyId = "acme",
                UserId = "user123",
                GroupIds = new List<string> { "group-a", "group-b" }
            },
            new User
            {
                Email = "user2@acme.com",
                PhoneNumber = "0987654321",
                CompanyId = "acme",
                UserId = "user456",
                GroupIds = new List<string> { "group-c" }
            },
            new User
            {
                Email = "admin@beta.com",
                PhoneNumber = "5555555555",
                CompanyId = "beta",
                UserId = "user789",
                GroupIds = new List<string> { "all-staff" }
            }
        };
    }

    public User? GetUserByEmail(string email)
    {
        return _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public User? GetUserByPhone(string phoneNumber)
    {
        return _users.FirstOrDefault(u => u.PhoneNumber.Equals(phoneNumber, StringComparison.OrdinalIgnoreCase));
    }
}
