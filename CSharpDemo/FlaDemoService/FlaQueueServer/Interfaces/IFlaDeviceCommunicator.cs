namespace FlaQueueServer.Interfaces
{
    public interface IFlaDeviceCommunicator
    {
        Task<string> ExecuteScanCommandAsync(string scanCommand);
    }
}