using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FidoNet
{
    public class RealNodeListEntry
    {
        public string Keyword { get; set; }
        public int Zone { get; set; }
        public int Region { get; set; }
        public int Net { get; set; }
        public int Node { get; set; }
        public string NodeName { get; set; }
        public string SysopName { get; set; }
        public string Location { get; set; }
        public string Phone { get; set; }
        public int Baud { get; set; }
        public List<string> Flags { get; set; } = new List<string>();

        public string GetFlagValue(string flagName)
        {
            foreach (var f in Flags)
                if (f.StartsWith(flagName + ":", StringComparison.OrdinalIgnoreCase))
                    return f.Substring(flagName.Length + 1);
            return null;
        }

        public string Host => GetFlagValue("INA") ?? GetFlagValue("ITU") ?? "";
        public int Port => 24554;

        public FidoAddress ToAddress() => new FidoAddress(Zone, Net, Node);
    }

    public class RealNodeList
    {
        private readonly List<RealNodeListEntry> _entries = new List<RealNodeListEntry>();
        public int Count => _entries.Count;

        public void Load(string path)
        {
            _entries.Clear();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";")) continue;

                var parts = line.Split(',');
                if (parts.Length < 4) continue;

                try
                {
                    var entry = new RealNodeListEntry
                    {
                        Keyword = parts[0].Trim(),
                        NodeName = parts.Length > 3 ? parts[3].Trim() : "",
                        SysopName = parts.Length > 4 ? parts[4].Trim() : "",
                        Location = parts.Length > 5 ? parts[5].Trim() : "",
                        Phone = parts.Length > 6 ? parts[6].Trim() : "",
                        Baud = parts.Length > 7 && int.TryParse(parts[7].Trim(), out int b) ? b : 0
                    };

                    string addrPart = parts[1].Trim();
                    string[] addrParts = addrPart.Split('/');
                    if (addrParts.Length == 2)
                    {
                        entry.Net = int.Parse(addrParts[0]);
                        entry.Node = int.Parse(addrParts[1]);
                    }
                    else if (addrPart.Contains(":"))
                    {
                        var zoneSplit = addrPart.Split(':');
                        entry.Zone = int.Parse(zoneSplit[0]);
                        var netNode = zoneSplit[1].Split('/');
                        entry.Net = int.Parse(netNode[0]);
                        entry.Node = int.Parse(netNode[1]);
                    }

                    if (parts.Length > 8)
                        foreach (var f in parts[8].Split(' '))
                            if (!string.IsNullOrWhiteSpace(f))
                                entry.Flags.Add(f.Trim());

                    _entries.Add(entry);
                }
                catch { }
            }
        }

        public RealNodeListEntry Find(FidoAddress addr)
        {
            return _entries.FirstOrDefault(e =>
                e.Zone == addr.Zone && e.Net == addr.Net && e.Node == addr.Node);
        }

        public static string ResolveDns(FidoAddress addr)
        {
            string host = addr.Point > 0
                ? $"p{addr.Point}.f{addr.Node}.n{addr.Net}.z{addr.Zone}.fidonet.org"
                : $"f{addr.Node}.n{addr.Net}.z{addr.Zone}.fidonet.org";

            try
            {
                var ips = System.Net.Dns.GetHostAddresses(host);
                if (ips.Length > 0)
                    return ips[0].ToString();
            }
            catch { }
            return null;
        }
    }
}