using System.Security.Principal;
using OplinkAuthDemo.Models;
using OplinkAuthDemo.Utils;

namespace OplinkAuthDemo.Services;

/// <summary>
/// 应用程序升级服务实现
/// 支持从网络共享下载更新包（需身份验证）并替换本地文件
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly EnvironmentConfig _config;

    public UpdateService(EnvironmentConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 执行应用程序更新
    /// </summary>
    public void PerformUpdate(string sourcePath, string destPath)
    {
        Console.WriteLine($"[UpdateService] 开始更新流程...");
        Console.WriteLine($"[UpdateService] 源路径: {sourcePath}");
        Console.WriteLine($"[UpdateService] 目标路径: {destPath}");

        // 1. 验证源文件是否存在 (可能需要模拟身份)
        // 在工厂模式下，访问网络资源可能需要域账户凭据
        // 我们使用 WindowsImpersonation (LOGON32_LOGON_NEW_CREDENTIALS) 来处理网络身份验证
        // 注意：LOGON32_LOGON_NEW_CREDENTIALS 允许本地操作继续使用当前用户身份，
        // 而网络连接使用模拟的凭据。这点对于"从网络下载(域账号)保存到本地(本机账号)"的场景非常理想。
        
        ExecuteWithNetworkIdentity(() =>
        {
            Console.WriteLine($"[UpdateService] 正在访问源文件... (当前身份/网络身份上下文: {WindowsIdentity.GetCurrent().Name})");
            
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"找不到更新源文件: {sourcePath}\n请检查路径是否正确，或是否有权限访问网络共享。");
            }
            
            // 2. 将文件从源复制到临时位置或直接目标位置
            // 由于 LOGON32_LOGON_NEW_CREDENTIALS 的特性，本地文件写入依然使用进程的原始身份（本机账户）
            // 这正是我们在该场景下想要的：读取远程用域账号，写入本地用本机账号。
            
            Console.WriteLine($"[UpdateService] 正在下载/复制文件...");
            
            // 确保目标目录存在
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourcePath, destPath, overwrite: true);
            Console.WriteLine($"[UpdateService] 文件复制完成!");
        });

        // 3. 后续操作（如备份、解压、重启进程等）- 这里仅演示文件替换
        Console.WriteLine($"[UpdateService] 更新文件已就绪: {destPath}");
    }

    /// <summary>
    /// 使用配置的网络身份执行操作
    /// </summary>
    private void ExecuteWithNetworkIdentity(Action action)
    {
        // 只有工厂模式且配置了凭据时才进行模拟
        if (_config.CurrentEnvironment == EnvironmentType.Factory 
            && _config.FactoryCredentials != null 
            && _config.FactoryCredentials.IsValid)
        {
            Console.WriteLine($"[UpdateService] 启用网络身份模拟: {_config.FactoryCredentials.Username}@{_config.FactoryCredentials.Domain}");
            
            using var impersonation = new WindowsImpersonation();
            
            // 登录并模拟
            impersonation.Logon(
                _config.FactoryCredentials.Domain,
                _config.FactoryCredentials.Username,
                _config.FactoryCredentials.Password);

            impersonation.RunImpersonated(action);
        }
        else
        {
            // 研发模式或无凭据，直接执行
            action();
        }
    }
}
