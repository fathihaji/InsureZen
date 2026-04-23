namespace InsureZen.Models;

public class InsuranceCompany
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // One company can have many claims
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}