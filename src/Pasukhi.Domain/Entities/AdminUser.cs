using Microsoft.AspNetCore.Identity;

namespace Pasukhi.Domain.Entities;

public class AdminUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalProviderId { get; set; }
}
