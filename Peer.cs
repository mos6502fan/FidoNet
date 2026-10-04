using System;

namespace FidoNet
{
    public class Peer
    {
        public string Name { get; set; }
        public FidoAddress Address { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 24554;
        public DateTime LastSeen { get; set; }
        public bool Online { get; set; }

        public override string ToString()
        {
            string status = Online ? "●" : "○";
            return $"{status} {Name} ({Address}) @ {Host}:{Port}";
        }
    }
}