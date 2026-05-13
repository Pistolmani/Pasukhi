namespace Pasukhi.Application.BotReadiness;

public class BotReadinessOptions
{
    public List<BotReadinessSection> Sections { get; set; } = [];
}

public class BotReadinessSection
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<BotReadinessQuestion> Questions { get; set; } = [];
}

public class BotReadinessQuestion
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public string InputType { get; set; } = "text";
    public bool Required { get; set; }
    public int Weight { get; set; } = 1;
}
