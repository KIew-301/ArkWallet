namespace ArkWallet.Domain.ValueObjects
{
    public class WizardStep
    {
        public string Name { get; set; }
        public string Question { get; set; }
        public string? ValidationPattern { get; set; }
        public string NextStep { get; set; }
        public bool OneStep { get; set; } = false;
        public List<QuickButton> Buttons { get; set; } = new();
        public Func<UserSession, string, Task<StepResult>>? Handler { get; set; }
    }

    public class QuickButton
    {
        public string? Text { get; set; }
        public string? Value { get; set; }
    }

    public class StepResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? NextStep { get; set; }
        public Dictionary<long, string>? AdditionMessage { get; set; }

        public static StepResult Ok(string nextStep, string? message = null, Dictionary<long, string>? additions = null) => new() { Success = true, NextStep = nextStep, Message = message, AdditionMessage = additions };
        public static StepResult Error(string message) => new() { Success = false, Message = message };
    }

    public class AdditionMessage
    {
        public long ChatId { get; set; }
        public string? Message { get; set; }

    }


    public class UserSession
    {
        public long Id { get; set; }
        public string? CurrentCommand { get; set; }
        public string? CurrentStep { get; set; }
        public Dictionary<string, object> Data { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
