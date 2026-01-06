namespace OplinkAuthDemo.Services;

/// <summary>
/// 应用程序升级服务接口
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 执行应用程序更新
    /// </summary>
    /// <param name="sourcePath">更新包源路径 (可以是网络共享路径)</param>
    /// <param name="destPath">本地目标路径</param>
    /// <returns>操作结果信息</returns>
    void PerformUpdate(string sourcePath, string destPath);
}
