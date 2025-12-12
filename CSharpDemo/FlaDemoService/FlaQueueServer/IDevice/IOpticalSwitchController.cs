namespace FlaQueueServer.IDevice
{
    public interface IOpticalSwitchController
    {
        /// <summary>
        /// 输入端口（有些设备固定，如你的 RS232 实现中为 _inputChannel）
        /// </summary>
        int InputChannel { get; }

        /// <summary>
        /// 支持的输出通道范围或集合
        /// </summary>
        IReadOnlyCollection<int> SupportedOutputChannels { get; }

        /// <summary>
        /// 当前的输出通道（若不可查询可返回 -1 或抛出 NotSupported）
        /// </summary>
        int CurrentOutputChannel { get; }

        /// <summary>
        /// 切换到指定输出通道<
        /// /summary>
        Task SetChannelAsync(int outputChannel, CancellationToken ct = default);

        /// <summary>
        /// 尝试查询当前输出通道
        /// </summary>
        Task<int> QueryChannelAsync(CancellationToken ct = default);

        /// <summary>
        /// 设备复位或回到默认路由（可选）</summary
        /// <summary>设备复位或回到默认路由（可选）</summary>
        Task ResetAsync(CancellationToken ct = default);
    }
}