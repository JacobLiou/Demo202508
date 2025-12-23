using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFDRCentralControlServer.Protocol
{
    public record AutoScanResult(
         double Resolution,          // 解析到的空间分辨率 n（首行或兜底）
         double WindowLength,        // m（调用方传入，来自 WR）
         int PointCount,             // 实际解析的点数
         double[] Y,                 // 纵坐标数据
         int RawBytes,               // 实际读取的有效数据区字节数（不含 '!'）
         bool TerminatedByBang       // 是否读到 '!' 结束符
     );
}
