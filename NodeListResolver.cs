using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FidoNet
{
    public class NodeListEntry
    {
        public FidoAddress Address { get; set; }
        public string NodeName { get; set; }
        public string SysopName { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 24554;
    }

    public class NodeListResolver
    {
        private readonly List<NodeListEntry> _entries = new List<NodeListEntry>();

        public void Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"NodeList not found: {path}");

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                try
                {
                    var addr = FidoAddress.Parse(parts[0].Trim());
                    _entries.Add(new NodeListEntry
                    {
                        Address = addr,
                        NodeName = parts[1].Trim(),
                        SysopName = parts[2].Trim(),
                        Host = parts[3].Trim(),
                        Port = int.Parse(parts[4].Trim())
                    });
                }
                catch { }
            }
        }

        public NodeListEntry Resolve(FidoAddress addr)
        {
            return _entries.FirstOrDefault(e =>
                e.Address.Zone == addr.Zone &&
                e.Address.Net == addr.Net &&
                e.Address.Node == addr.Node &&
                e.Address.Point == addr.Point);
        }

        public NodeListEntry Resolve(string addrStr)
        {
            return Resolve(FidoAddress.Parse(addrStr));
        }
    }
}