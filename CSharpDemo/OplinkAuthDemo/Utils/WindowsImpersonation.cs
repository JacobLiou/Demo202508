using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace OplinkAuthDemo.Utils;

/// <summary>
/// Windows 身份模拟工具类
/// 用于在工厂环境下模拟域账户进行文件操作
/// </summary>
public class WindowsImpersonation : IDisposable
{
    private SafeAccessTokenHandle? _tokenHandle;
    private bool _disposed;

    #region Windows API 声明

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out SafeAccessTokenHandle phToken);

    // 登录类型常量
    private const int LOGON32_LOGON_INTERACTIVE = 2;
    private const int LOGON32_LOGON_NETWORK = 3;
    private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;

    // 登录提供者常量
    private const int LOGON32_PROVIDER_DEFAULT = 0;
    private const int LOGON32_PROVIDER_WINNT50 = 3;

    #endregion

    /// <summary>
    /// 使用域账户凭据进行登录
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>是否登录成功</returns>
    public bool Logon(string domain, string username, string password)
    {
        // 尝试使用 LOGON32_LOGON_NEW_CREDENTIALS 类型，适用于网络资源访问
        bool success = LogonUser(
            username,
            domain,
            password,
            LOGON32_LOGON_NEW_CREDENTIALS,
            LOGON32_PROVIDER_WINNT50,
            out _tokenHandle);

        if (!success)
        {
            // 如果失败，尝试使用 LOGON32_LOGON_NETWORK 类型
            success = LogonUser(
                username,
                domain,
                password,
                LOGON32_LOGON_NETWORK,
                LOGON32_PROVIDER_DEFAULT,
                out _tokenHandle);
        }

        if (!success)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"登录失败，错误代码: {errorCode}");
        }

        return success;
    }

    /// <summary>
    /// 在模拟的身份上下文中执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    public void RunImpersonated(Action action)
    {
        if (_tokenHandle == null || _tokenHandle.IsInvalid)
        {
            throw new InvalidOperationException("未登录或令牌无效，请先调用 Logon 方法");
        }

        WindowsIdentity.RunImpersonated(_tokenHandle, action);
    }

    /// <summary>
    /// 在模拟的身份上下文中执行操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="func">要执行的操作</param>
    /// <returns>操作结果</returns>
    public T RunImpersonated<T>(Func<T> func)
    {
        if (_tokenHandle == null || _tokenHandle.IsInvalid)
        {
            throw new InvalidOperationException("未登录或令牌无效，请先调用 Logon 方法");
        }

        return WindowsIdentity.RunImpersonated(_tokenHandle, func);
    }

    /// <summary>
    /// 获取模拟身份的用户名
    /// </summary>
    public string? GetImpersonatedUserName()
    {
        if (_tokenHandle == null || _tokenHandle.IsInvalid)
        {
            return null;
        }

        return RunImpersonated(() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.Name;
        });
    }

    #region IDisposable 实现

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _tokenHandle?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~WindowsImpersonation()
    {
        Dispose(false);
    }

    #endregion
}
