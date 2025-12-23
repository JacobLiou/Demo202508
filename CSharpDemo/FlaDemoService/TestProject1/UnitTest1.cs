using OFDRCentralControlServer.Protocol;

namespace TestProject1
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            string s = "OP_1.012_2.110_3.291_5.676_8.163_-79.197_-76.391_-68.657_-68.236_-73.937_9_SN9II1_405.668_PO";

            var res = FrameParser.TryParseFrame(s, out var frame);
            Assert.True(res);

            s = "OP_21.275_-57.128_9_SN9II1_97.404_PO";

            res = FrameParser.TryParseFrame(s, out var frame1);
            Assert.True(res);
        }

        [Fact]
        public void Test2()
        {
            var frame = SendFramer.Frame(Const.DEFAULT_START, Const.DEFAULT_END, "2", Const.DEFAULT_ALGO, Const.DEFAULT_WIDTH, Const.DEFAULT_THRESHOLD, Const.DEFAULT_ID, Const.DEFAULT_SN);
            Assert.Equal("SCAN_0.0_30.0_2_2_0.5_-80_12_SN9II1_136.500_NACS", frame);


            var ss = "SCAN_0.5_25_2_2_0.513_-80_09_SN9II1_129.013_NACS";

            frame = SendFramer.Frame("0.5", "25", "2", "2", "0.513", "-80", "09", Const.DEFAULT_SN);

            Assert.Equal(ss, frame);
        }
    }
}