namespace ArkWallet.Domain.ValueObjects
{
    /// <summary>
    /// Тип чата в Telegram для управления доступом и кнопками
    /// </summary>
    public enum ChatType
    {
        Private,
        Group,
        Supergroup
    }

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
        public List<QuickButton>? Buttons { get; set; }
        public string? SentFilePath { get; set; }

        public static StepResult Ok(string nextStep, string? message = null) => new() { Success = true, NextStep = nextStep, Message = message };
        public static StepResult Error(string message) => new() { Success = false, Message = message };
    }

    public class WizardResult
    {
        public string? Message { get; set; }
        public List<QuickButton>? Buttons { get; set; }
        public string? SentFilePath { get; set; }
        
        /// <summary>
        /// Тип чата, в котором была вызвана команда.
        /// Используется для фильтрации кнопок (в группах только "Обновить").
        /// </summary>
        public ChatType? ChatType { get; set; }
    }

    public class UserSession
    {
        public long Id { get; set; }
        public string? CurrentCommand { get; set; }
        public string? CurrentStep { get; set; }
        public Dictionary<string, object> Data { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public interface IUserSessionStore
    {
        bool TryGet(long userId, out UserSession? session);
        void Set(long userId, UserSession session);
        bool Remove(long userId);
    }
}
