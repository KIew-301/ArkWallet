namespace ArkWallet.Application.Contracts.Other
{
    public interface ITaskDispatcher
    {
        Task SendTaskAsync(string taskType, object taskData);
    }
}
