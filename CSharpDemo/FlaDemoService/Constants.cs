/// <summary>
/// 应用程序常量定义
/// </summary>
public static class Constants
{
    /// <summary>
    /// FLA 设备协议常量
    /// </summary>
    public static class FlaProtocol
    {
        public const string HandshakeToken = "OCI";
        public const string ScanCommand = "SCAN";
        public const string QuitCommand = "QUIT";
        public const string ByeResponse = "BYE";
        public const string SetOkResponse = "SET OK";
        public const string InputErrorResponse = "INPUT_ERROR";
        public const string UnsupportedResponse = "UNSUPPORTED";
        public const string PayloadTerminator = "!";
        public const int DefaultPort = 4300;
        public const int ChunkSize = 12;
    }

    /// <summary>
    /// 操作模式
    /// </summary>
    public static class OperationMode
    {
        public const string Scan = "scan";
        public const string AutoPeak = "auto_peak";
    }

    /// <summary>
    /// 默认超时配置（秒）
    /// </summary>
    public static class Timeouts
    {
        public const int HandshakeTimeoutSeconds = 5;
        public const int ConnectionTimeoutSeconds = 10;
        public const int ReadTimeoutSeconds = 30;
        public const int WriteTimeoutSeconds = 10;
    }

    /// <summary>
    /// 参数默认值
    /// </summary>
    public static class DefaultParams
    {
        public const string OpMode = "scan";
        public const string SrMode = "0";
        public const string Gain = "1";
        public const string WrLen = "10.00";
        public const string XCenter = "000.0";
        public const string CountMode = "2";
        public const string Algo = "2";
    }

    /// <summary>
    /// 增益映射
    /// </summary>
    public static readonly Dictionary<string, string> GainMap = new()
    {
        ["1"] = "1",
        ["2"] = "2",
        ["5"] = "3",
        ["10"] = "4"
    };

    /// <summary>
    /// 结果存储配置
    /// </summary>
    public static class ResultStore
    {
        /// <summary>
        /// 结果保留时间（小时）
        /// </summary>
        public const int RetentionHours = 24;
        
        /// <summary>
        /// 清理间隔（分钟）
        /// </summary>
        public const int CleanupIntervalMinutes = 60;
    }
}

