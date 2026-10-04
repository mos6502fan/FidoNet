using System;

namespace FidoNet
{
    public class FidoAddress
    {
        public int Zone { get; set; }
        public int Net { get; set; }
        public int Node { get; set; }
        public int Point { get; set; }

        public FidoAddress() { }

        public FidoAddress(int zone, int net, int node, int point = 0)
        {
            Zone = zone;
            Net = net;
            Node = node;
            Point = point;
        }

        public static FidoAddress Parse(string s)
        {
            var addr = new FidoAddress();
            string rest = s.Trim();

            int colon = rest.IndexOf(':');
            if (colon >= 0)
            {
                addr.Zone = int.Parse(rest.Substring(0, colon));
                rest = rest.Substring(colon + 1);
            }

            int dot = rest.IndexOf('.');
            if (dot >= 0)
            {
                addr.Point = int.Parse(rest.Substring(dot + 1));
                rest = rest.Substring(0, dot);
            }

            string[] parts = rest.Split('/');
            if (parts.Length != 2)
                throw new FormatException($"Invalid address: {s}");

            addr.Net = int.Parse(parts[0]);
            addr.Node = int.Parse(parts[1]);

            return addr;
        }

        public override string ToString()
        {
            if (Point > 0)
                return $"{Zone}:{Net}/{Node}.{Point}";
            return $"{Zone}:{Net}/{Node}";
        }

        public override bool Equals(object obj)
        {
            if (obj is FidoAddress other)
                return Zone == other.Zone && Net == other.Net &&
                       Node == other.Node && Point == other.Point;
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Zone;
                hash = hash * 31 + Net;
                hash = hash * 31 + Node;
                hash = hash * 31 + Point;
                return hash;
            }
        }
    }
}