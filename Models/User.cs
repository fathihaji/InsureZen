namespace InsureZen.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties - EF Core uses these to understand relationships
    public ICollection<Claim> MakerClaims { get; set; } = new List<Claim>();
    public ICollection<Claim> CheckerClaims { get; set; } = new List<Claim>();
}