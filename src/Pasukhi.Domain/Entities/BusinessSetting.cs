namespace Pasukhi.Domain.Entities;

public class BusinessSetting : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
