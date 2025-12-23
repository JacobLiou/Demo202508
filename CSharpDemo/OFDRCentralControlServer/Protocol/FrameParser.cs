using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OFDRCentralControlServer.Protocol
{
    public class FrameParser
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>
        /// 解析 OP 回复帧到 AutoPeakResult，并返回校验是否在容差内（默认 0.1）。
        /// 帧格式示例：
        /// OP_1.012_2.110_3.291_5.676_8.163_-79.197_-76.391_-68.657_-68.236_-73.937_9_SN9II1_405.668_PO
        /// OP_21.275_-57.128_9_SN9II1_97.404_PO
        /// </summary>
        public static bool TryParseFrame(string frame, out AutoPeakResult result, double tolerance = 0.1)
        {
            result = new AutoPeakResult();

            if (string.IsNullOrWhiteSpace(frame) || !frame.StartsWith("OP_") || !frame.EndsWith("_PO"))
                return false;

            // 去掉前缀/结尾
            string content = frame.Substring(3, frame.Length - 3 - 3); // 去掉 "OP_" 和 "_PO"
            var parts = content.Split('_');
            if (parts.Length < 3) return false;

            // 找到 SN 的位置（仅支持合并写法：SNxxxx；如需支持 SN_XXXX，可再加判断）
            int snIndex = Array.FindIndex(parts, p => p.StartsWith("SN", StringComparison.Ordinal));
            if (snIndex < 0 || snIndex + 1 >= parts.Length) return false;

            // 解析 SN 与校验和
            string snRaw = parts[snIndex].Substring(2); // 去掉 "SN"
            if (!double.TryParse(parts[snIndex + 1], NumberStyles.Float, CI, out double checksum))
                return false;

            // 解析 SN 之前的数值
            var allValues = new List<double>();

            //去除9 它是工位号
            double.TryParse(  parts[snIndex - 1], out var id);

            for (int i = 0; i < snIndex - 1; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float | NumberStyles.AllowLeadingSign, CI, out double v))
                    return false;
                allValues.Add(v);
            }

            // 拆分到 Positions / Dbs（规则：非负 -> Positions；负数 -> Dbs）
            var positions = new List<double>();
            var dbs = new List<double>();
            foreach (var v in allValues)
            {
                if (v < 0) dbs.Add(v);
                else positions.Add(v);
            }

            var r = new AutoPeakResult
            {
                PeakPositions = positions,
                PeakDbs = dbs,
                Sn = parts[snIndex],
                CheckSum = checksum
            };

            result = r;

            // 校验：绝对值求和 + SN 的数字位求和
            double expected = positions.Sum(x => Math.Abs(x)) + dbs.Sum(x => Math.Abs(x)) + GetAllSnInts(snRaw).Sum() + id;
            return Math.Abs(expected - checksum) <= tolerance;
        }

        /// <summary>
        /// 计算 SN 中所有数字位的和（逐位，如 "9II1" -> 9 + 1    /// 计算 SN 中所有数字位的和（逐位，如 "9II1" -> 9 + 1 = 10）
        /// </summary>
        public static int SumDigits(string snRaw)
        {
            int s = 0;
            foreach (char c in snRaw)
            {
                if (char.IsDigit(c)) s += (c - '0');
            }
            return s;
        }

        public static int[] GetAllSnInts(string sn)
        {
            // 匹配所有单个数字
            MatchCollection matches = Regex.Matches(sn, @"\d");

            // 转换为 int 数组
            int[] numbers = matches.Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();

            return numbers;
        }

        // 解析首行分辨率：优先数值（可带单位 m），否则回退
        public static double TryParseResolution(string line, double fallback)
        {
            string s = line.Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && n > 0)
                return n;
            return fallback;
        }

        // 将固定宽度 ASCII 12B 解析为 double
        public static double ParseChunkAsAsciiDouble(List<byte> buf, int offset, int len)
        {
            var slice = buf.Skip(offset).Take(len).ToArray();
            string s = Encoding.ASCII.GetString(slice).Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return v;

            // 如果你的设备的 12B 是二进制而非 ASCII，请把这里替换为二进制解析。
            throw new FormatException($"无法解析 12B 数据块为数值：\"{s}\"");
        }
    }
}