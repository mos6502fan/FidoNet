using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FidoNet
{
    public class PeerManager
    {
        private readonly string _path;
        private readonly List<Peer> _peers = new List<Peer>();
        public Peer Active { get; private set; }

        public PeerManager(string basePath)
        {
            Directory.CreateDirectory(basePath);
            _path = Path.Combine(basePath, "peers.txt");
            Load();
        }

        public List<Peer> All => _peers;

        public Peer AddOrUpdate(string name, FidoAddress address, string host, int port)
        {
            var existing = _peers.FirstOrDefault(p => p.Address.Equals(address));
            if (existing != null)
            {
                existing.Name = name;
                existing.Host = host;
                existing.Port = port;
                Save();
                return existing;
            }

            var peer = new Peer
            {
                Name = name,
                Address = address,
                Host = host,
                Port = port
            };
            _peers.Add(peer);
            Save();
            return peer;
        }

        public void SetActive(Peer peer) => Active = peer;

        public Peer FindByAddress(FidoAddress address)
        {
            return _peers.FirstOrDefault(p => p.Address.Equals(address));
        }

        public void MarkSeen(Peer peer)
        {
            peer.LastSeen = DateTime.Now;
            peer.Online = true;
        }

        public void MarkOffline(Peer peer) => peer.Online = false;

        private void Load()
        {
            if (!File.Exists(_path)) return;
            foreach (var line in File.ReadAllLines(_path, Encoding.UTF8))
            {
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;
                try
                {
                    _peers.Add(new Peer
                    {
                        Name = parts[0],
                        Address = FidoAddress.Parse(parts[1]),
                        Host = parts[2],
                        Port = int.Parse(parts[3]),
                        LastSeen = parts.Length > 4 ? DateTime.Parse(parts[4]) : DateTime.MinValue,
                        Online = false
                    });
                }
                catch { }
            }
        }

        private void Save()
        {
            var sb = new StringBuilder();
            foreach (var p in _peers)
                sb.AppendLine($"{p.Name}\t{p.Address}\t{p.Host}\t{p.Port}\t{p.LastSeen:O}");
            File.WriteAllText(_path, sb.ToString(), Encoding.UTF8);
        }
    }
}