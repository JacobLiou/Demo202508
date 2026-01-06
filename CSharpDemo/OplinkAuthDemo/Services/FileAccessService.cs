using System.Security.Principal;
using OplinkAuthDemo.Models;
using OplinkAuthDemo.Utils;

namespace OplinkAuthDemo.Services;

/// <summary>
/// 文件访问服务实现
/// 根据环境类型采用不同的账户策略
/// </summary>
public class FileAccessService : IFileAccessService, IDisposable
{
    private readonly EnvironmentConfig _config;
    private readonly WindowsImpersonation? _impersonation;
    private bool _disposed;

    public FileAccessService(EnvironmentConfig config)
    {
        _config = config;

        // 如果是工厂环境，则初始化身份模拟
        if (_config.CurrentEnvironment == EnvironmentType.Factory)
        {
            if (_config.FactoryCredentials == null || !_config.FactoryCredentials.IsValid)
            {
                throw new InvalidOperationException("工厂环境需要配置有效的域账户凭据");
            }

            _impersonation = new WindowsImpersonation();
            _impersonation.Logon(
                _config.FactoryCredentials.Domain,
                _config.FactoryCredentials.Username,
                _config.FactoryCredentials.Password);

            Console.WriteLine($"[FileAccessService] 工厂模式 - 已模拟域账户: {_impersonation.GetImpersonatedUserName()}");
        }
        else
        {
            Console.WriteLine($"[FileAccessService] 研发模式 - 使用本机账户: {WindowsIdentity.GetCurrent().Name}");
        }
    }

    /// <summary>
    /// 读取文件内容
    /// </summary>
    public string ReadFile(string filePath)
    {
        return ExecuteWithProperIdentity(() =>
        {
            Console.WriteLine($"[ReadFile] 当前身份: {WindowsIdentity.GetCurrent().Name}");
            return File.ReadAllText(filePath);
        });
    }

    /// <summary>
    /// 写入文件内容
    /// </summary>
    public void WriteFile(string filePath, string content)
    {
        ExecuteWithProperIdentity(() =>
        {
            Console.WriteLine($"[WriteFile] 当前身份: {WindowsIdentity.GetCurrent().Name}");
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
        });
    }

    /// <summary>
    /// 获取当前使用的身份信息
    /// </summary>
    public string GetCurrentIdentity()
    {
        return ExecuteWithProperIdentity(() => WindowsIdentity.GetCurrent().Name);
    }

    /// <summary>
    /// 根据环境类型使用正确的身份执行操作
    /// </summary>
    private T ExecuteWithProperIdentity<T>(Func<T> action)
    {
        if (_config.CurrentEnvironment == EnvironmentType.Factory && _impersonation != null)
        {
            // 工厂模式：使用模拟的域账户
            return _impersonation.RunImpersonated(action);
        }
        else
        {
            // 研发模式：直接使用本机账户
            return action();
        }
    }

    /// <summary>
    /// 根据环境类型使用正确的身份执行操作（无返回值）
    /// </summary>
    private void ExecuteWithProperIdentity(Action action)
    {
        if (_config.CurrentEnvironment == EnvironmentType.Factory && _impersonation != null)
        {
            // 工厂模式：使用模拟的域账户
            _impersonation.RunImpersonated(action);
        }
        else
        {
            // 研发模式：直接使用本机账户
            action();
        }
    }

    #region IDisposable 实现

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _impersonation?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
