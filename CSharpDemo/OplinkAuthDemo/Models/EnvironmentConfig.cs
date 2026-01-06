namespace OplinkAuthDemo.Models;

/// <summary>
/// 环境类型枚举
/// </summary>
public enum EnvironmentType
{
    /// <summary>
    /// 研发环境 - 使用本机账户
    /// </summary>
    RnD,

    /// <summary>
    /// 工厂环境 - 需要使用域账户
    /// </summary>
    Factory
}

/// <summary>
/// 域账户凭据配置
/// </summary>
public class DomainCredentials
{
    /// <summary>
    /// 域名
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 验证凭据是否完整
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Domain) 
                        && !string.IsNullOrWhiteSpace(Username) 
                        && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// 环境配置
/// </summary>
public class EnvironmentConfig
{
    /// <summary>
    /// 当前环境类型
    /// </summary>
    public EnvironmentType CurrentEnvironment { get; set; } = EnvironmentType.RnD;

    /// <summary>
    /// 工厂模式下的域账户凭据
    /// </summary>
    public DomainCredentials? FactoryCredentials { get; set; }

    /// <summary>
    /// 从环境变量或配置文件检测当前环境
    /// 可以通过设置环境变量 OPLINK_ENV=Factory 来切换到工厂模式
    /// </summary>
    public static EnvironmentType DetectEnvironment()
    {
        var envValue = Environment.GetEnvironmentVariable("OPLINK_ENV");
        if (string.Equals(envValue, "Factory", StringComparison.OrdinalIgnoreCase))
        {
            return EnvironmentType.Factory;
        }
        return EnvironmentType.RnD;
    }
}
