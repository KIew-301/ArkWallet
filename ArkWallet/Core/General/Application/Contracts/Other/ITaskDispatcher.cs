namespace ArkWallet.Core.General.Application.Contracts.Other
{
    public interface ITaskDispatcher
    {
        Task SendTaskAsync(string taskType, object taskData);
    }
}
