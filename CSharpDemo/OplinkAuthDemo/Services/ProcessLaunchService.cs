using System.Diagnostics;
using System.Security;
using OplinkAuthDemo.Models;

namespace OplinkAuthDemo.Services;

/// <summary>
/// 进程启动服务实现
/// 根据环境类型采用不同的账户启动进程
/// </summary>
public class ProcessLaunchService : IProcessLaunchService
{
    private readonly EnvironmentConfig _config;

    public ProcessLaunchService(EnvironmentConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 启动新进程
    /// </summary>
    public bool StartProcess(string fileName, string arguments = "")
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            // 工厂模式下，配置域账户凭据
            if (_config.CurrentEnvironment == EnvironmentType.Factory)
            {
                if (_config.FactoryCredentials == null || !_config.FactoryCredentials.IsValid)
                {
                    throw new InvalidOperationException("工厂环境需要配置有效的域账户凭据来启动进程");
                }

                Console.WriteLine($"[ProcessLaunchService] 工厂模式 - 准备使用域账户启动进程: {_config.FactoryCredentials.Username}@{_config.FactoryCredentials.Domain}");
                
                startInfo.Domain = _config.FactoryCredentials.Domain;
                startInfo.UserName = _config.FactoryCredentials.Username;
                startInfo.Password = ConvertToSecureString(_config.FactoryCredentials.Password);
                
                // 注意：使用特定凭据启动进程时，UseShellExecute 必须为 false
                // LoadUserProfile 通常设为 true 以加载用户配置文件
                startInfo.LoadUserProfile = true;
            }
            else
            {
                Console.WriteLine("[ProcessLaunchService] 研发模式 - 使用当前账户启动进程");
            }

            Console.WriteLine($"[ProcessLaunchService] 正在启动: {fileName} {arguments}");
            
            using var process = Process.Start(startInfo);
            
            if (process != null)
            {
                Console.WriteLine($"[ProcessLaunchService] 进程启动成功! Process ID: {process.Id}");
                return true;
            }
            else
            {
                Console.WriteLine("[ProcessLaunchService] 进程启动失败 (Process is null)");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProcessLaunchService] 启动进程发生异常: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[详细] {ex.InnerException.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// 将普通字符串转换为 SecureString
    /// </summary>
    private SecureString ConvertToSecureString(string password)
    {
        var secureString = new SecureString();
        foreach (char c in password)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        return secureString;
    }
}
