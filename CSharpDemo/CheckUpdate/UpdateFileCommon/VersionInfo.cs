using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateFileCommon
{
    public class VersionInfo
    {
        public string Version { get; set; }
        public int VersionValue { get; set; }
        public string ReleaseNote { get; set; }
        public bool IsMustUpdate { get; set; }
    }
}
