namespace ArkWallet.Application.Contracts
{
    internal interface ITaskDispatcher
    {
        Task SendTaskAsync(string taskType, object taskData);
    }
}
