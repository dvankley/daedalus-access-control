using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace DaedalusTestApp.Comms
{
    internal class NetPacket
    {
        internal IPEndPoint source;
        internal IPEndPoint destination;
        internal TransportType transportType;
        internal byte[] payload;
    }
}
