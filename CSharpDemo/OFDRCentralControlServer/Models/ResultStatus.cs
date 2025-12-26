using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OFDRCentralControlServer.Models
{
    public enum ResultStatus
    {
        queued,
        running,
        expired,
        success,
        failed
    }
}
