namespace OFDRCentralControlServer.Protocol
{
    public static class Const
    {
        // ------------------ 固化的默认参数（最佳参数） ------------------
        public const string DEFAULT_START = "0.0";

        public const string DEFAULT_END = "30.0";

        public const string MAX_END = "300.0";

        public const string DEFAULT_ALGO = "2";

        public const string DEFAULT_WIDTH = "0.5";   // m

        public const string DEFAULT_THRESHOLD = "-80";    // => -80dB

        public const string DEFAULT_ID = "12";

        public const string DEFAULT_SN = "SN9II1";

        // 扫描时对客户端 lengthHint 的安全裕度（单位：米）
        public const double SCAN_END_MARGIN_M = 5.0;
    }
}