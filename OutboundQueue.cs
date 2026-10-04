using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FidoNet
{
    public class OutboundQueue
    {
        private readonly string _path;
        private readonly List<ChatMessage> _messages = new List<ChatMessage>();

        public OutboundQueue(string basePath)
        {
            Directory.CreateDirectory(basePath);
            _path = Path.Combine(basePath, "outbound.txt");
            Load();
        }

        public int Count => _messages.Count;
        public List<ChatMessage> All => _messages;

        public List<ChatMessage> GetForPeer(string peerAddress)
        {
            return _messages.Where(m => m.PeerAddress == peerAddress).ToList();
        }

        public void Enqueue(ChatMessage msg)
        {
            msg.Status = MessageStatus.Pending;
            _messages.Add(msg);
            Save();
        }

        public void MarkSent(ChatMessage msg)
        {
            msg.Status = MessageStatus.Sent;
            _messages.Remove(msg);
            Save();
        }

        private void Load()
        {
            if (!File.Exists(_path)) return;
            foreach (var line in File.ReadAllLines(_path, Encoding.UTF8))
            {
                var parts = line.Split('\t');
                if (parts.Length < 6) continue;
                _messages.Add(new ChatMessage
                {
                    Date = DateTime.Parse(parts[0]),
                    From = parts[1],
                    To = parts[2],
                    PeerAddress = parts[3],
                    Text = parts[4].Replace("\\t", "\t").Replace("\\n", "\n"),
                    Status = (MessageStatus)Enum.Parse(typeof(MessageStatus), parts[5]),
                    IsOutgoing = true
                });
            }
        }

        private void Save()
        {
            var sb = new StringBuilder();
            foreach (var m in _messages)
            {
                string text = m.Text.Replace("\t", "\\t").Replace("\n", "\\n");
                sb.AppendLine($"{m.Date:O}\t{m.From}\t{m.To}\t{m.PeerAddress}\t{text}\t{m.Status}");
            }
            File.WriteAllText(_path, sb.ToString(), Encoding.UTF8);
        }
    }
}