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
                UserId = "user1"
            },
            new User
            {
                Email = "user2@acme.com",
                PhoneNumber = "0987654321",
                UserId = "user2"
            },
            new User
            {
                Email = "admin@beta.com",
                PhoneNumber = "5555555555",
                UserId = "admin"
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

    public IEnumerable<User> GetAllUsers()
    {
        return _users;
    }
}
