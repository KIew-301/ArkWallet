using System.ComponentModel.DataAnnotations;

namespace ArkWallet.Domain.Entities;

internal class GlobalGoal
{
    [Key]
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Target { get; set; }
    public decimal Actual { get; set; }
    public decimal Progress { get; set; }
    public int AchievedCount { get; set; }
    public List<GlobalGoalHistory> Histories { get; set; } = new();
    public List<GlobalGoalStep> Steps { get; set; } = new();

    public static GlobalGoal Create(
        long id, string name, string description, decimal target, decimal actual, decimal progress,
        int achievedCount)
    {
        return new GlobalGoal
        {
            Id = id,
            Name = name,
            Description = description,
            Target = target,
            Actual = actual,
            Progress = progress,
            AchievedCount = achievedCount
        };
    }
}
