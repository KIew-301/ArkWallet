namespace ArkWallet.Application.Contracts.Other
{
    internal interface ITaskDispatcher
    {
        Task SendTaskAsync(string taskType, object taskData);
    }
}
