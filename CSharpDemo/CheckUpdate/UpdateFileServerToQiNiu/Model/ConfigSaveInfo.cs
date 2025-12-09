using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UpdateFileCommon;

namespace UpdateFileServerToQiNiu.Model
{
    internal class ConfigSaveInfo
    {
        public string AK { get; set; }
        public string SK { get; set; }
        public string BucketName { get; set; }
        public BindingList<ServerUpdateFileInfo> FileList { get; set; }
    }
}
