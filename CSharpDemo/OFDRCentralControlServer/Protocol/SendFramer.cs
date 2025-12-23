using System.Text.RegularExpressions;

namespace OFDRCentralControlServer.Protocol
{
    /// [2025-12-22 14:44:30.292]# SEND ASCII>
    //SCAN_0.5_25_2_2_0.513_-80_09_SN9II1_129.013_NACS
    //OP_1.012_2.110_3.291_5.676_8.163_-79.197_-76.391_-68.657_-68.236_-73.937_9_SN9II1_405.668_PO

    //[2025 - 12 - 22 14:47:37.778]# SEND ASCII>
    //SCAN_8.163_25_2_2_0.513_-80_09_SN9II1_136.676_NACS
    //[2025 - 12 - 22 14:47:40.993]# RECV ASCII>
    //OP_21.275_-57.128_9_SN9II1_97.404_PO
    public class SendFramer
    {
        public static string Frame(string start, string end, string count, string algo, string width, string thr, string id, string sn)
        {
            var sum = CalcSum(new[] { start, end, count, algo, width, thr, id }); // 绝对值求和（含 ID/SN 数字部分），容差 0.1
            var nums = GetAllNums(sn);
            sum += nums.Sum();

            var cmd = $"SCAN_{start}_{end}_{count}_{algo}_{width}_{thr}_{id}_{sn}_{sum:F3}_NACS";
            return cmd;
        }

        public static string Fmt5(string raw)
        {
            var s = raw.Trim();
            if (s.Length > 5)
                s = s[..5];

            return s.PadLeft(5, '0');
        }

        public static int[] GetAllNums(string sn)
        {
            // 匹配所有单个数字
            MatchCollection matches = Regex.Matches(sn, @"\d");

            // 转换为 int 数组
            int[] numbers = matches.Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            return numbers;
        }

        public static double CalcSum(IEnumerable<string> parts)
        {
            double total = 0.0;
            foreach (var s in parts)
            {
                float.TryParse(s, out var result);
                total += Math.Abs(result);
            }

            return total;
        }
    }
}