using System;
using System.Collections.Generic;
using System.Linq;

namespace FidoNet
{
    public class RouteEntry
    {
        public int DestZone { get; set; }
        public int DestNet { get; set; }
        public FidoAddress NextHop { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 24554;
    }

    public class FidoRouter
    {
        private readonly List<RouteEntry> _routes = new List<RouteEntry>();

        public void AddRoute(RouteEntry route) => _routes.Add(route);

        public RouteEntry FindRoute(FidoAddress dest)
        {
            var exact = _routes.FirstOrDefault(r => r.DestZone == dest.Zone && r.DestNet == dest.Net);
            if (exact != null) return exact;

            var zoneRoute = _routes.FirstOrDefault(r => r.DestZone == dest.Zone && r.DestNet == 0);
            if (zoneRoute != null) return zoneRoute;

            return _routes.FirstOrDefault(r => r.DestZone == 0);
        }

        public List<FidoPacket> RoutePacket(FidoPacket packet, FidoAddress myAddress)
        {
            var outbound = new List<FidoPacket>();

            foreach (var msg in packet.Messages)
            {
                var dest = msg.To;
                if (dest.Zone == myAddress.Zone && dest.Net == myAddress.Net &&
                    dest.Node == myAddress.Node && dest.Point == myAddress.Point)
                    continue;

                var route = FindRoute(dest);
                if (route == null) continue;

                var outPacket = new FidoPacket
                {
                    OrigAddress = myAddress,
                    DestAddress = route.NextHop
                };
                outPacket.Messages.Add(msg);
                outbound.Add(outPacket);
            }

            return outbound;
        }
    }
}