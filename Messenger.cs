using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FidoNet
{
    public class Messenger
    {
        public FidoAddress MyAddress { get; set; }
        public string MyName { get; set; }
        public PeerManager Peers { get; }
        public OutboundQueue Queue { get; }
        public Profile Profile { get; }
        public EchoAreaStore Echoes { get; }
        public Action<ChatMessage> OnIncomingMessage { get; set; }

        private readonly string _historyPath;
        private readonly List<ChatMessage> _history = new List<ChatMessage>();

        public const string DefaultArea = "LOCAL.CHAT";

        public Messenger(string basePath)
        {
            Directory.CreateDirectory(basePath);
            _historyPath = Path.Combine(basePath, "chat_history.txt");
            Peers = new PeerManager(basePath);
            Queue = new OutboundQueue(basePath);
            Profile = new Profile(basePath);
            Echoes = new EchoAreaStore(Path.Combine(basePath, "echo_store"));
            LoadHistory();
        }

        public void Configure(FidoAddress my, string defaultNick)
        {
            MyAddress = my;
            Profile.Address = my;

            if (string.IsNullOrEmpty(Profile.Nick) || Profile.Nick == "Anonymous")
                Profile.ChangeNick(defaultNick);

            MyName = Profile.Nick;
            Profile.Save();
        }

        public void ChangeNick(string newNick)
        {
            Profile.ChangeNick(newNick);
            MyName = Profile.Nick;
            Console.WriteLine($"[i] Ник изменён на: {MyName}");
        }

        public void ChangeSysop(string name)
        {
            Profile.ChangeSysop(name);
            Console.WriteLine($"[i] Имя сисопа: {Profile.SysopName}");
        }

        public void ChangeSystemName(string name)
        {
            Profile.ChangeSystemName(name);
            Console.WriteLine($"[i] Имя системы: {Profile.SystemName}");
        }

        public void AddIncoming(FidoPacket packet)
        {
            foreach (var msg in packet.Messages)
            {
                var peer = Peers.FindByAddress(msg.From);
                if (peer != null)
                    Peers.MarkSeen(peer);

                var chat = new ChatMessage
                {
                    From = msg.FromName,
                    To = msg.ToName,
                    Text = msg.Body,
                    Date = msg.Date,
                    IsOutgoing = false,
                    Status = MessageStatus.Received,
                    PeerAddress = msg.From.ToString(),
                    Area = msg.AreaTag ?? DefaultArea
                };
                _history.Add(chat);

                if (OnIncomingMessage != null)
                    OnIncomingMessage(chat);
                else
                    PrintMessage(chat);
            }
            SaveHistory();
        }

        public void StoreLocalEcho(FidoMessage msg)
        {
            Echoes.Append(DefaultArea, msg);
        }

        public async Task<bool> SendAsync(string text)
        {
            if (Peers.Active == null)
            {
                Console.WriteLine("[!] Нет активного пира. Используйте /to <address>");
                return false;
            }

            var peer = Peers.Active;

            var chat = new ChatMessage
            {
                From = MyName,
                To = "All",
                Text = text,
                Date = DateTime.Now,
                IsOutgoing = true,
                Status = MessageStatus.Pending,
                PeerAddress = peer.Address.ToString(),
                Area = DefaultArea
            };

            bool ok = await TrySendAsync(peer, text);

            if (ok)
            {
                chat.Status = MessageStatus.Sent;
                Peers.MarkSeen(peer);
                PrintMessage(chat);
                _history.Add(chat);
                SaveHistory();
                return true;
            }

            chat.Status = MessageStatus.Pending;
            Queue.Enqueue(chat);
            Peers.MarkOffline(peer);
            PrintMessage(chat);
            _history.Add(chat);
            SaveHistory();
            Console.WriteLine($"[i] Пир offline. Сообщение в очереди ({Queue.Count}).");
            return false;
        }

        private async Task<bool> TrySendAsync(Peer peer, string text)
        {
            var session = new FidoSession(MyAddress)
            {
                LocalSystemName = Profile.SystemName,
                LocalSysopName = Profile.SysopName,
                Verbose = false
            };

            bool ok = await session.ConnectAsync(peer.Host, peer.Port);
            if (!ok) return false;

            var packet = new FidoPacket
            {
                OrigAddress = MyAddress,
                DestAddress = peer.Address
            };
            packet.Messages.Add(new FidoMessage
            {
                From = MyAddress,
                FromName = MyName,
                To = peer.Address,
                ToName = "All",
                Subject = $"Chat {DateTime.Now:HH:mm:ss}",
                Body = text,
                Date = DateTime.Now,
                Type = FidoMessageType.Echomail,
                AreaTag = DefaultArea
            });

            session.SendPacket(packet);
            session.Close();
            return true;
        }

        public async Task PollAsync()
        {
            if (Peers.Active == null)
            {
                Console.WriteLine("[!] Нет активного пира.");
                return;
            }

            var peer = Peers.Active;
            Console.WriteLine($"[*] Poll {peer.Name} ({peer.Host}:{peer.Port})...");

            var session = new FidoSession(MyAddress)
            {
                LocalSystemName = Profile.SystemName,
                LocalSysopName = Profile.SysopName,
                Verbose = false
            };

            bool ok = await session.ConnectAsync(peer.Host, peer.Port);
            if (!ok)
            {
                Peers.MarkOffline(peer);
                Console.WriteLine($"[!] {peer.Name} недоступен.");
                return;
            }

            Peers.MarkSeen(peer);

            var pending = Queue.GetForPeer(peer.Address.ToString());
            if (pending.Count > 0)
            {
                Console.WriteLine($"[*] Отправка {pending.Count} из очереди...");
                var packets = new List<FidoPacket>();
                foreach (var m in pending)
                {
                    var p = new FidoPacket
                    {
                        OrigAddress = MyAddress,
                        DestAddress = peer.Address
                    };
                    p.Messages.Add(new FidoMessage
                    {
                        From = MyAddress,
                        FromName = MyName,
                        To = peer.Address,
                        ToName = "All",
                        Subject = $"Chat {m.Date:HH:mm:ss}",
                        Body = m.Text,
                        Date = m.Date,
                        Type = FidoMessageType.Echomail,
                        AreaTag = DefaultArea
                    });
                    packets.Add(p);
                }
                session.SendBundle(packets);
                foreach (var m in pending)
                    Queue.MarkSent(m);
            }

            session.Close();
            Console.WriteLine($"[✓] Poll завершён.");
        }

        public void PrintHistory(int count = 20)
        {
            int skip = Math.Max(0, _history.Count - count);
            var recent = _history.Skip(skip).ToList();
            Console.WriteLine($"\n=== Последние {recent.Count} сообщений ===");
            foreach (var m in recent)
                PrintMessage(m, withDate: true);
            Console.WriteLine("==============================\n");
        }

        public void PrintQueue()
        {
            Console.WriteLine($"\n=== Очередь ({Queue.Count}) ===");
            if (Queue.Count == 0)
                Console.WriteLine("  пусто");
            else
                foreach (var m in Queue.All)
                    Console.WriteLine($"  ⏳ [{m.Date:HH:mm}] → {m.PeerAddress}: {m.Text}");
            Console.WriteLine("==============================\n");
        }

        public void PrintPeers()
        {
            Console.WriteLine($"\n=== Пиры ({Peers.All.Count}) ===");
            foreach (var p in Peers.All)
            {
                string marker = Peers.Active == p ? "*" : " ";
                Console.WriteLine($" {marker} {p}");
            }
            Console.WriteLine("==============================\n");
        }

        public void PrintProfile()
        {
            Console.WriteLine();
            Console.WriteLine("=== Профиль ===");
            Console.WriteLine($"  Ник:        {MyName}");
            Console.WriteLine($"  Адрес:      {MyAddress}");
            Console.WriteLine($"  Имя сисопа: {Profile.SysopName}");
            Console.WriteLine($"  Имя системы:{Profile.SystemName}");
            Console.WriteLine("===============\n");
        }

        public void PrintIncoming(ChatMessage m) => PrintMessage(m);

        private void PrintMessage(ChatMessage m, bool withDate = false)
        {
            string dateStr = withDate ? $"[{m.Date:dd.MM HH:mm}] " : "";
            string dir;
            ConsoleColor color;

            if (m.IsOutgoing)
            {
                switch (m.Status)
                {
                    case MessageStatus.Sent: dir = ">>"; color = ConsoleColor.Cyan; break;
                    case MessageStatus.Pending: dir = "⏳"; color = ConsoleColor.Yellow; break;
                    case MessageStatus.Failed: dir = "✗ "; color = ConsoleColor.Red; break;
                    default: dir = ">>"; color = ConsoleColor.Cyan; break;
                }
            }
            else
            {
                dir = "<<";
                color = ConsoleColor.Green;
            }

            Console.ForegroundColor = color;
            Console.WriteLine($"{dateStr}{dir} {m.From}: {m.Text}");
            Console.ResetColor();
        }

        private void LoadHistory()
        {
            if (!File.Exists(_historyPath)) return;
            foreach (var line in File.ReadAllLines(_historyPath, Encoding.UTF8))
            {
                var parts = line.Split('\t');
                if (parts.Length < 7) continue;
                _history.Add(new ChatMessage
                {
                    Date = DateTime.Parse(parts[0]),
                    IsOutgoing = parts[1] == "1",
                    From = parts[2],
                    To = parts[3],
                    PeerAddress = parts[4],
                    Area = parts[5],
                    Text = parts[6].Replace("\\t", "\t").Replace("\\n", "\n")
                });
            }
        }

        private void SaveHistory()
        {
            var sb = new StringBuilder();
            foreach (var m in _history)
            {
                string text = m.Text.Replace("\t", "\\t").Replace("\n", "\\n");
                sb.AppendLine($"{m.Date:O}\t{(m.IsOutgoing ? "1" : "0")}\t{m.From}\t{m.To}\t{m.PeerAddress}\t{m.Area}\t{text}");
            }
            File.WriteAllText(_historyPath, sb.ToString(), Encoding.UTF8);
        }
    }
}