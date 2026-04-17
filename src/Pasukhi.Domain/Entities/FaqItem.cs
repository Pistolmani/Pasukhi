namespace Pasukhi.Domain.Entities;

public class FaqItem : TenantEntity
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public int MatchCount { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
