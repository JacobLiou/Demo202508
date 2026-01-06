namespace OplinkAuthDemo.Services;

/// <summary>
/// 文件访问服务接口
/// </summary>
public interface IFileAccessService
{
    /// <summary>
    /// 读取文件内容
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件内容</returns>
    string ReadFile(string filePath);

    /// <summary>
    /// 写入文件内容
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">要写入的内容</param>
    void WriteFile(string filePath, string content);

    /// <summary>
    /// 获取当前使用的身份信息
    /// </summary>
    /// <returns>当前身份名称</returns>
    string GetCurrentIdentity();
}
