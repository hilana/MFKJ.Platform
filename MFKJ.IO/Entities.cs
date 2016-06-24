using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFKJ.IO
{
    public class RevEventArg : EventArgs
    {
        public long AllFileLength { get; set; }
        public long CurrentFileLength { get; set; }
        public int SpeedKb { get; set; }
        public string State { get; set; }
    }
}
