using OFDRCentralControlServer.Protocol;

namespace TestProject1
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            string s = "OP_1.012_2.110_3.291_5.676_8.163_-79.197_-76.391_-68.657_-68.236_-73.937_9_SN9II1_405.668_PO";

          var res =   FrameParser.TryParseFrame(s, out var frame);
            Assert.True(res);

            s = "OP_21.275_-57.128_9_SN9II1_97.404_PO";

            res = FrameParser.TryParseFrame(s, out var frame1);
            Assert.True(res);
        }
    }
}