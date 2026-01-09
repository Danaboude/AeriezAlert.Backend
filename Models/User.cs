namespace AeriezAlert.Backend.Models;

public class User
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public List<string> GroupIds { get; set; } = new();
}
