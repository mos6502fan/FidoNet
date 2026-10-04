using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FidoNet
{
    public class FidoSession
    {
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;

        public FidoAddress LocalAddress { get; set; }
        public string LocalSystemName { get; set; } = "MyFido";
        public string LocalSysopName { get; set; } = "Sysop";
        public int LocalPort { get; set; } = 24554;
        public bool Verbose { get; set; } = false;

        public FidoSession(FidoAddress localAddr)
        {
            LocalAddress = localAddr;
        }

        public async Task ListenAsync(int port, Action<FidoPacket> onPacketReceived)
        {
            LocalPort = port;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Log($"Listening on port {port}...");

            while (true)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(); }
                catch { break; }

                _client = client;
                _stream = _client.GetStream();

                var handshake = new EmsiHandshake(LocalAddress, LocalSystemName, LocalSysopName)
                {
                    Verbose = Verbose
                };

                if (handshake.PerformAsAnswerer(_stream))
                {
                    Log($"EMSI OK. Remote: {handshake.RemoteSystemName} ({handshake.RemoteAddress})");
                    try { ReceivePackets(onPacketReceived); }
                    catch (Exception ex) { Log($"Receive error: {ex.Message}"); }
                }

                try { _client?.Client?.Shutdown(SocketShutdown.Both); } catch { }
                _stream?.Close();
                _client?.Close();
            }
        }

        public async Task<bool> ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            try
            {
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();

                var handshake = new EmsiHandshake(LocalAddress, LocalSystemName, LocalSysopName)
                {
                    Verbose = Verbose
                };

                return handshake.PerformAsCaller(_stream);
            }
            catch (Exception ex)
            {
                Log($"Connection error: {ex.Message}");
                return false;
            }
        }

        public void SendPacket(FidoPacket packet)
        {
            SendBundle(new List<FidoPacket> { packet });
        }

        public void SendBundle(List<FidoPacket> packets)
        {
            byte[] countBytes = BitConverter.GetBytes(packets.Count);
            _stream.Write(countBytes, 0, 4);

            foreach (var packet in packets)
            {
                string tempPath = Path.GetTempFileName() + ".pkt";
                packet.Write(tempPath);
                byte[] fileData = File.ReadAllBytes(tempPath);
                File.Delete(tempPath);

                byte[] header = BitConverter.GetBytes(fileData.Length);
                _stream.Write(header, 0, 4);
                _stream.Write(fileData, 0, fileData.Length);
            }
            _stream.Flush();
        }

        private void ReceivePackets(Action<FidoPacket> onPacketReceived)
        {
            _stream.ReadTimeout = 30000;

            byte[] countBuf = new byte[4];
            int countRead;
            try { countRead = ReadExact(_stream, countBuf, 4); }
            catch { return; }
            if (countRead < 4) return;

            int packetCount = BitConverter.ToInt32(countBuf, 0);
            if (packetCount <= 0 || packetCount > 1000) return;

            for (int i = 0; i < packetCount; i++)
            {
                byte[] header = new byte[4];
                int read;
                try { read = ReadExact(_stream, header, 4); }
                catch { break; }
                if (read < 4) break;

                int length = BitConverter.ToInt32(header, 0);
                if (length <= 0 || length > 10_000_000) break;

                byte[] fileData = new byte[length];
                int totalRead = 0;
                try
                {
                    while (totalRead < length)
                    {
                        int n = _stream.Read(fileData, totalRead, length - totalRead);
                        if (n == 0) break;
                        totalRead += n;
                    }
                }
                catch { break; }

                string tempFile = Path.GetTempFileName();
                File.WriteAllBytes(tempFile, fileData);
                var packet = FidoPacket.Read(tempFile);
                onPacketReceived?.Invoke(packet);
                File.Delete(tempFile);
            }
        }

        private static int ReadExact(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n == 0) return total;
                total += n;
            }
            return total;
        }

        public void Close()
        {
            try { _client?.Client?.Shutdown(SocketShutdown.Both); } catch { }
            _stream?.Close();
            _client?.Close();
        }

        public void StopListener()
        {
            try { _listener?.Stop(); } catch { }
        }

        private void Log(string msg)
        {
            if (Verbose)
                Console.WriteLine($"[session] {msg}");
        }
    }
}