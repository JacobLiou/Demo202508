namespace OFDRCentralControlServer.Protocol
{
    public class AutoPeakResult
    {
        public List<double>? PeakPositions { get; set; }

        public List<double>? PeakDbs { get; set; }

        public string? Sn { get; set; }

        public double CheckSum { get; set; }
    }
}
