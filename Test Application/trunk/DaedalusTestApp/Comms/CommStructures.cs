using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;

namespace DaedalusTestApp.Comms
{
    /// <summary>
    /// This class is intended to provide a mechanism to streamline tasks waiting for a network packet from
    /// a specific endpoint.
    /// </summary>
    internal class PacketRegistryEntry
    {
        internal IPEndPoint destination { get; set; }
        internal IPEndPoint source { get; set; }
        internal ManualResetEvent packetReady { get; set; }
        internal NetPacket packet { get; set; }
    }

}
