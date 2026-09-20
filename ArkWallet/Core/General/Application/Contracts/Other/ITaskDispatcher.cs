namespace ArkWallet.Core.General.Application.Contracts.Other
{
    /// <summary>Диспетчер задач: отправляет задачи для асинхронной обработки по типу.</summary>
    public interface ITaskDispatcher
    {
        /// <summary>Отправляет задачу типа <paramref name="taskType"/> с данными <paramref name="taskData"/> на обработку.</summary>
        Task SendTaskAsync(string taskType, object taskData);
    }
}
