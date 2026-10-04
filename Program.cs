using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FidoNet
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var myAddr = new FidoAddress(2, 5020, 100);
            var session = new FidoSession(myAddr)
            {
                LocalSystemName = "MyFido",
                LocalSysopName = "Sysop",
                Verbose = false
            };

            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            var tosser = new FidoTosser(Path.Combine(basePath, "mailbase"));
            tosser.SubscribedAreas.Add("RU.ANEKDOT");
            tosser.SubscribedAreas.Add("SU.HARDW");
            tosser.SubscribedAreas.Add(Messenger.DefaultArea);

            var router = new FidoRouter();

            string nodeListPath = Path.Combine(basePath, "nodelist.txt");
            var resolver = new NodeListResolver();
            if (File.Exists(nodeListPath))
                resolver.Load(nodeListPath);

            var messenger = new Messenger(Path.Combine(basePath, "chat"));
            messenger.Configure(myAddr, "Vasya");
            RegisterPeers(messenger);

            if (args.Length > 0 && args[0] == "listen")
            {
                Console.WriteLine($"Starting FidoNet hub {myAddr} in listen mode");
                Console.WriteLine($"My name: {messenger.MyName}");
                Console.WriteLine($"Known peers: {messenger.Peers.All.Count}");
                foreach (var p in messenger.Peers.All)
                    Console.WriteLine($"  {p}");
                Console.WriteLine();

                await session.ListenAsync(24554, packet =>
                {
                    tosser.ProcessPacket(packet, myAddr);
                    messenger.AddIncoming(packet);

                    Console.WriteLine($"[hub] packet from {packet.OrigAddress}, {packet.Messages.Count} msg(s)");

                    foreach (var msg in packet.Messages)
                    {
                        Console.WriteLine($"[hub] msg: Type={msg.Type}, Area='{msg.AreaTag}', From={msg.From}, FromName={msg.FromName}");

                        bool isLocalChat =
                            (msg.Type == FidoMessageType.Echomail &&
                             string.Equals(msg.AreaTag, Messenger.DefaultArea, StringComparison.OrdinalIgnoreCase)) ||
                            (msg.Body != null && msg.Body.Contains($"AREA:{Messenger.DefaultArea}"));

                        if (isLocalChat)
                        {
                            Console.WriteLine($"[hub] LOCAL.CHAT detected, forwarding...");
                            ForwardToAllPeers(messenger, msg, myAddr);
                        }
                    }
                });
            }
            else if (args.Length > 0 && args[0] == "chat")
            {
                await RunChat(messenger, args, resolver, myAddr, tosser, router);
            }
            else if (args.Length > 1 && args[0] == "connect")
            {
                await RunConnect(session, messenger, args, resolver, myAddr);
            }
            else
            {
                PrintUsage();
            }

            Console.ReadKey();
        }

        static void RegisterPeers(Messenger messenger)
        {
            messenger.Peers.AddOrUpdate("ClientA", new FidoAddress(2, 5020, 200), "127.0.0.1", 24555);
            messenger.Peers.AddOrUpdate("ClientB", new FidoAddress(2, 5020, 300), "127.0.0.1", 24556);
            messenger.Peers.AddOrUpdate("Hub", new FidoAddress(2, 5020, 100), "127.0.0.1", 24554);
        }

        static void ForwardToAllPeers(Messenger messenger, FidoMessage msg, FidoAddress hubAddr)
        {
            Console.WriteLine($"[hub] forwarding from {msg.FromName} ({msg.From}), MsgId={msg.MsgId}");
            Console.WriteLine($"[hub] known peers: {messenger.Peers.All.Count}");

            foreach (var peer in messenger.Peers.All)
            {
                Console.WriteLine($"[hub] candidate: {peer.Name} ({peer.Address}) @ {peer.Host}:{peer.Port}");

                if (peer.Address.Equals(msg.From))
                {
                    Console.WriteLine($"[hub]   skip: sender");
                    continue;
                }
                if (peer.Address.Equals(hubAddr))
                {
                    Console.WriteLine($"[hub]   skip: hub");
                    continue;
                }

                var fwd = new FidoPacket
                {
                    OrigAddress = hubAddr,
                    DestAddress = peer.Address
                };
                fwd.Messages.Add(new FidoMessage
                {
                    From = msg.From,
                    FromName = msg.FromName,
                    To = peer.Address,
                    ToName = "All",
                    Subject = msg.Subject,
                    Body = msg.Body,
                    Date = msg.Date,
                    Type = FidoMessageType.Echomail,
                    AreaTag = Messenger.DefaultArea,
                    MsgId = msg.MsgId
                });

                Console.WriteLine($"[hub]   forward → {peer.Name} ({peer.Host}:{peer.Port})");
                SendDirectAsync(fwd, peer);
            }
        }

        static void SendDirectAsync(FidoPacket packet, Peer peer)
        {
            Task.Run(async () =>
            {
                var s = new FidoSession(packet.OrigAddress)
                {
                    LocalSystemName = "Hub",
                    LocalSysopName = "Hub",
                    Verbose = false
                };
                try
                {
                    Console.WriteLine($"[hub] connecting to {peer.Host}:{peer.Port}...");
                    bool ok = await s.ConnectAsync(peer.Host, peer.Port);
                    if (ok)
                    {
                        Console.WriteLine($"[hub] connected, sending to {peer.Name}");
                        s.SendPacket(packet);
                        s.Close();
                        Console.WriteLine($"[hub] sent to {peer.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"[hub] FAILED to connect to {peer.Name} at {peer.Host}:{peer.Port}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[hub] EXCEPTION: {ex.Message}");
                }
            });
        }

        static async Task RunChat(Messenger messenger, string[] args, NodeListResolver resolver,
            FidoAddress myAddr, FidoTosser tosser, FidoRouter router)
        {
            string target = args.Length > 1 ? args[1] : "127.0.0.1";
            string host = "127.0.0.1";
            int port = 24554;
            int listenPort = 24555;

            if (target.Contains(":") && target.Contains("/"))
            {
                var entry = resolver.Resolve(target);
                if (entry != null)
                {
                    host = entry.Host;
                    port = entry.Port;
                }
            }
            else
            {
                host = target;
                if (args.Length > 2) port = int.Parse(args[2]);
            }

            if (args.Length > 3 && int.TryParse(args[3], out int lp))
                listenPort = lp;

            FidoAddress peerAddr;
            try { peerAddr = FidoAddress.Parse(target.Contains("/") ? target : "2:5020/100"); }
            catch { peerAddr = new FidoAddress(2, 5020, 100); }

            var peer = messenger.Peers.AddOrUpdate("Hub", peerAddr, host, port);
            messenger.Peers.SetActive(peer);

            var listenerSession = new FidoSession(myAddr)
            {
                LocalSystemName = messenger.Profile.SystemName,
                LocalSysopName = messenger.Profile.SysopName,
                Verbose = false
            };

            var listenerTask = Task.Run(async () =>
            {
                try
                {
                    await listenerSession.ListenAsync(listenPort, packet =>
                    {
                        tosser.ProcessPacket(packet, myAddr);
                        messenger.AddIncoming(packet);
                    });
                }
                catch { }
            });

            messenger.OnIncomingMessage = msg =>
            {
                Console.WriteLine();
                messenger.PrintIncoming(msg);
                Console.Write($"[{messenger.Peers.Active?.Name ?? "?"}]> ");
            };

            Console.WriteLine($"=== FidoNet Messenger ===");
            Console.WriteLine($"Я: {myAddr} ({messenger.MyName})");
            Console.WriteLine($"Активный пир: {peer}");
            Console.WriteLine($"Слушаю входящие на порту: {listenPort}");
            Console.WriteLine();
            Console.WriteLine("Команды: /nick /sysop /system /profile /to /peers /queue /poll /history /clear /help /quit");
            Console.WriteLine();

            messenger.PrintHistory(10);

            while (true)
            {
                Console.Write($"[{messenger.Peers.Active?.Name ?? "?"}]> ");
                string line = Console.ReadLine();
                if (line == null) break;
                line = line.Trim();
                if (line.Length == 0) continue;

                if (line == "/quit" || line == "/exit") break;
                if (line == "/help") { PrintHelp(); continue; }
                if (line == "/history") { messenger.PrintHistory(30); continue; }
                if (line == "/queue") { messenger.PrintQueue(); continue; }
                if (line == "/peers") { messenger.PrintPeers(); continue; }
                if (line == "/profile") { messenger.PrintProfile(); continue; }
                if (line == "/poll") { await messenger.PollAsync(); continue; }
                if (line == "/clear") { Console.Clear(); continue; }

                if (line == "/nick") { Console.WriteLine($"[i] Текущий ник: {messenger.MyName}"); continue; }
                if (line.StartsWith("/nick "))
                {
                    try { messenger.ChangeNick(line.Substring(6).Trim()); }
                    catch (Exception ex) { Console.WriteLine($"[!] {ex.Message}"); }
                    continue;
                }

                if (line == "/sysop") { Console.WriteLine($"[i] Имя сисопа: {messenger.Profile.SysopName}"); continue; }
                if (line.StartsWith("/sysop "))
                {
                    try { messenger.ChangeSysop(line.Substring(7).Trim()); }
                    catch (Exception ex) { Console.WriteLine($"[!] {ex.Message}"); }
                    continue;
                }

                if (line == "/system") { Console.WriteLine($"[i] Имя системы: {messenger.Profile.SystemName}"); continue; }
                if (line.StartsWith("/system "))
                {
                    try { messenger.ChangeSystemName(line.Substring(8).Trim()); }
                    catch (Exception ex) { Console.WriteLine($"[!] {ex.Message}"); }
                    continue;
                }

                if (line.StartsWith("/to "))
                {
                    string newTarget = line.Substring(4).Trim();
                    var existing = messenger.Peers.All.Find(p =>
                        p.Address.ToString() == newTarget ||
                        p.Name.Equals(newTarget, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        messenger.Peers.SetActive(existing);
                        Console.WriteLine($"[i] Активный пир: {existing}");
                    }
                    else
                        Console.WriteLine($"[!] Пир {newTarget} не найден. Используйте /peers");
                    continue;
                }

                await messenger.SendAsync(line);
            }

            listenerSession.StopListener();
            Console.WriteLine("Чат завершён.");
        }

        static void PrintHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Команды:");
            Console.WriteLine("  /nick [new]       показать или сменить ник");
            Console.WriteLine("  /sysop [new]      показать или сменить имя сисопа");
            Console.WriteLine("  /system [new]     показать или сменить имя системы");
            Console.WriteLine("  /profile          показать профиль");
            Console.WriteLine("  /to <addr|name>   переключить активного пира");
            Console.WriteLine("  /peers            список пиров");
            Console.WriteLine("  /queue            очередь исходящих");
            Console.WriteLine("  /poll             опросить активного пира");
            Console.WriteLine("  /history          история сообщений");
            Console.WriteLine("  /clear            очистить экран");
            Console.WriteLine("  /help             эта справка");
            Console.WriteLine("  /quit             выход");
            Console.WriteLine();
        }

        static async Task RunConnect(FidoSession session, Messenger messenger, string[] args,
            NodeListResolver resolver, FidoAddress myAddr)
        {
            string target = args[1];
            string host;
            int port;

            if (target.Contains(":") && target.Contains("/"))
            {
                var entry = resolver.Resolve(target);
                if (entry != null)
                {
                    host = entry.Host;
                    port = args.Length > 2 ? int.Parse(args[2]) : entry.Port;
                }
                else
                {
                    var fidoAddr = FidoAddress.Parse(target);
                    string dnsHost = RealNodeList.ResolveDns(fidoAddr);
                    if (dnsHost != null)
                    {
                        host = dnsHost;
                        port = args.Length > 2 ? int.Parse(args[2]) : 24554;
                    }
                    else
                    {
                        Console.WriteLine($"Cannot resolve {target}");
                        return;
                    }
                }
            }
            else
            {
                host = target;
                port = args.Length > 2 ? int.Parse(args[2]) : 24554;
            }

            Console.WriteLine($"Connecting to {host}:{port}...");
            bool ok = await session.ConnectAsync(host, port);
            if (ok)
            {
                var packet = new FidoPacket
                {
                    OrigAddress = myAddr,
                    DestAddress = new FidoAddress(2, 5020, 200)
                };
                packet.Messages.Add(new FidoMessage
                {
                    From = myAddr,
                    FromName = messenger.MyName,
                    To = new FidoAddress(2, 5020, 200),
                    ToName = "All",
                    Subject = "Test",
                    Body = "Hello via FidoNet Packet System.",
                    Date = DateTime.Now,
                    Type = FidoMessageType.Echomail,
                    AreaTag = Messenger.DefaultArea
                });
                session.SendPacket(packet);
                session.Close();
            }
            Console.WriteLine("Done.");
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  fido listen");
            Console.WriteLine("  fido chat [host] [port] [listenPort]");
            Console.WriteLine("  fido connect <host> [port]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  fido listen");
            Console.WriteLine("  fido chat 127.0.0.1 24554 24555");
            Console.WriteLine("  fido chat 127.0.0.1 24554 24556");
        }
    }
}