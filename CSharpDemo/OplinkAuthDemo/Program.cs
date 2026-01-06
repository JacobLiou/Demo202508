using System.Security.Principal;
using OplinkAuthDemo.Models;
using OplinkAuthDemo.Services;

namespace OplinkAuthDemo;

/// <summary>
/// 工厂/研发环境文件访问权限控制 Demo
/// 
/// 功能说明：
/// - 研发环境 (R&D): 使用本机当前账户进行文件读写操作
/// - 工厂环境 (Factory): 使用特定域账户进行文件读写操作
/// 
/// 使用方法：
/// 1. 直接运行程序，选择要测试的环境模式
/// 2. 或设置环境变量 OPLINK_ENV=Factory 自动切换到工厂模式
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     工厂/研发环境管控 Demo (文件/进程/升级)              ║");
        Console.WriteLine("║     Factory/R&D Control Demo (File/Process/Update)       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // 显示当前系统信息
        Console.WriteLine($"[系统信息] 当前本机账户: {WindowsIdentity.GetCurrent().Name}");
        Console.WriteLine($"[系统信息] 检测到的环境: {EnvironmentConfig.DetectEnvironment()}");
        Console.WriteLine();

        // 选择测试模式
        Console.WriteLine("请选择测试模式:");
        Console.WriteLine("  1. 研发模式 (R&D) - 使用本机账户");
        Console.WriteLine("  2. 工厂模式 (Factory) - 使用域账户");
        Console.WriteLine("  3. 退出");
        Console.WriteLine();
        Console.Write("请输入选项 (1-3): ");

        var choice = Console.ReadLine()?.Trim();

        switch (choice)
        {
            case "1":
                TestRnDMode();
                break;
            case "2":
                TestFactoryMode();
                break;
            case "3":
                Console.WriteLine("再见!");
                return;
            default:
                Console.WriteLine("无效的选项");
                break;
        }

        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }

    /// <summary>
    /// 测试研发模式 - 包含文件、进程、升级
    /// </summary>
    static void TestRnDMode()
    {
        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════════════════");
        Console.WriteLine("  测试研发模式 (R&D Mode) - 使用本机账户");
        Console.WriteLine("════════════════════════════════════════════════════════════");
        Console.WriteLine();

        var config = new EnvironmentConfig
        {
            CurrentEnvironment = EnvironmentType.RnD
        };

        // 1. 测试文件访问
        TestFileAccess(config);

        // 2. 测试进程启动
        TestProcessLaunch(config);

        // 3. 测试应用程序升级
        TestApplicationUpdate(config);
    }

    /// <summary>
    /// 测试工厂模式 - 包含文件、进程、升级
    /// </summary>
    static void TestFactoryMode()
    {
        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════════════════");
        Console.WriteLine("  测试工厂模式 (Factory Mode) - 使用域账户");
        Console.WriteLine("════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // 获取域账户凭据
        Console.WriteLine("请输入域账户凭据:");
        Console.WriteLine("(注意: 需要有效的域账户才能正常工作)");
        Console.WriteLine();

        Console.Write("域名 (例如: MYDOMAIN): ");
        var domain = Console.ReadLine()?.Trim() ?? "";

        Console.Write("用户名: ");
        var username = Console.ReadLine()?.Trim() ?? "";

        Console.Write("密码: ");
        var password = ReadPassword();
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("[错误] 请输入完整的域账户凭据!");
            return;
        }

        var config = new EnvironmentConfig
        {
            CurrentEnvironment = EnvironmentType.Factory,
            FactoryCredentials = new DomainCredentials
            {
                Domain = domain,
                Username = username,
                Password = password
            }
        };

        // 1. 测试文件访问
        TestFileAccess(config);

        // 2. 测试进程启动
        TestProcessLaunch(config);

        // 3. 测试应用程序升级
        TestApplicationUpdate(config);
    }

    /// <summary>
    /// 测试文件访问功能
    /// </summary>
    static void TestFileAccess(EnvironmentConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("--- 测试 1: 文件访问权限 ---");
        
        try
        {
            using var fileService = new FileAccessService(config);

            // 测试文件路径
            var fileName = config.CurrentEnvironment == EnvironmentType.Factory ? "factory_test.txt" : "rnd_test.txt";
            var testFilePath = Path.Combine(Path.GetTempPath(), "OplinkAuthDemo", fileName);
            var testContent = $"测试文件\n环境模式: {config.CurrentEnvironment}\n创建时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n操作账户: {fileService.GetCurrentIdentity()}";

            // 写入测试
            Console.WriteLine($"[测试] 正在写入文件: {testFilePath}");
            fileService.WriteFile(testFilePath, testContent);
            Console.WriteLine("[测试] ✓ 文件写入成功!");

            // 读取测试
            Console.WriteLine("[测试] 正在读取文件...");
            var readContent = fileService.ReadFile(testFilePath);
            Console.WriteLine("[测试] ✓ 文件读取成功!");
            Console.WriteLine($"[内容] {readContent.Replace("\n", " | ")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 文件访问失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"[详细] {ex.InnerException.Message}");
        }
    }

    /// <summary>
    /// 测试进程启动功能
    /// </summary>
    static void TestProcessLaunch(EnvironmentConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("--- 测试 2: 进程启动管控 ---");

        try
        {
            var processService = new ProcessLaunchService(config);
            
            // 启动 cmd.exe 并执行 whoami 命令，暂停以便查看结果
            // 注意: 在工厂模式下以不同用户启动时，会弹出一个新的控制台窗口
            var fileName = "cmd.exe";
            var arguments = "/c echo 当前进程用户: & whoami & timeout /t 10";

            Console.WriteLine($"[测试] 正在启动进程: {fileName} {arguments}");
            bool result = processService.StartProcess(fileName, arguments);
            
            if (result)
            {
                Console.WriteLine("[测试] ✓ 进程启动调用成功!");
                Console.WriteLine("[提示] 请检查新弹出的窗口（如果有）以验证显示的用户名是否正确");
            }
            else
            {
                Console.WriteLine("[测试] ✗ 进程启动失败");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 进程启动异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试应用程序升级功能
    /// </summary>
    static void TestApplicationUpdate(EnvironmentConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("--- 测试 3: 应用程序升级 (模拟) ---");

        try
        {
            var updateService = new UpdateService(config);

            // 模拟源文件路径
            // 在工厂模式下，这应该是一个 UNC 路径 (例如 \\Server\Share\Update.zip)
            // 为了 Demo 演示方便，我们根据不同模式设置不同的模拟路径
            
            string sourcePath;
            string destPath = Path.Combine(Path.GetTempPath(), "OplinkAuthDemo", "LocalApp_v2.zip");

            if (config.CurrentEnvironment == EnvironmentType.Factory)
            {
                Console.Write("[交互] 请输入模拟的更新源路径 (支持 UNC 路径，留空则跳过真实下载): ");
                var inputPath = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(inputPath))
                {
                    Console.WriteLine("[跳过] 未提供源路径，仅演示流程逻辑。");
                    return;
                }
                sourcePath = inputPath;
            }
            else
            {
                // R&D 模式，创建一个本地临时文件作为源
                sourcePath = Path.Combine(Path.GetTempPath(), "OplinkAuthDemo", "UpdateSource_v2.zip");
                // 确保源文件夹存在
                Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
                
                if (!File.Exists(sourcePath))
                {
                    File.WriteAllText(sourcePath, "这是版本 2.0 的更新包内容 (R&D Mode)");
                }
            }

            Console.WriteLine($"[测试] 准备更新...");
            Console.WriteLine($"[测试] 源: {sourcePath}");
            Console.WriteLine($"[测试] 目标: {destPath}");

            updateService.PerformUpdate(sourcePath, destPath);
            
            Console.WriteLine("[测试] ✓ 更新流程执行成功!");
            
            if (File.Exists(destPath))
            {
                 Console.WriteLine($"[验证] 目标文件存在，大小: {new FileInfo(destPath).Length} 字节");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 更新失败: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"[详细] {ex.InnerException.Message}");
        }
    }

    /// <summary>
    /// 读取密码（隐藏输入）
    /// </summary>
    static string ReadPassword()
    {
        var password = new System.Text.StringBuilder();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

            if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
        } while (key.Key != ConsoleKey.Enter);

        return password.ToString();
    }
}
