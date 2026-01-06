namespace OplinkAuthDemo.Services;

/// <summary>
/// 进程启动服务接口
/// </summary>
public interface IProcessLaunchService
{
    /// <summary>
    /// 启动新进程
    /// </summary>
    /// <param name="fileName">可执行文件路径或命令</param>
    /// <param name="arguments">启动参数</param>
    /// <returns>启动成功返回true，否则false</returns>
    bool StartProcess(string fileName, string arguments = "");
}
