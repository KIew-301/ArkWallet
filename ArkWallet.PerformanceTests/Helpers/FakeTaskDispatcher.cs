using ArkWallet.Application.Contracts.Other;

namespace ArkWallet.PerformanceTests.Helpers;

public sealed class FakeTaskDispatcher : ITaskDispatcher
{
    public List<(string TaskType, object TaskData)> Sent { get; } = new();

    public Task SendTaskAsync(string taskType, object taskData)
    {
        Sent.Add((taskType, taskData));
        return Task.CompletedTask;
    }
}
