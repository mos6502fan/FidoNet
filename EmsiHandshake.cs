using System;
using System.IO;
using System.Text;

namespace FidoNet
{
    public class EmsiHandshake
    {
        public const byte CR = 0x0D;

        public string RemoteSystemName { get; private set; } = "";
        public string RemoteSysopName { get; private set; } = "";
        public FidoAddress RemoteAddress { get; private set; }
        public bool Verbose { get; set; } = false;

        private readonly FidoAddress _localAddress;
        private readonly string _localSystemName;
        private readonly string _localSysopName;

        public EmsiHandshake(FidoAddress localAddress, string localSystemName, string localSysopName)
        {
            _localAddress = localAddress;
            _localSystemName = localSystemName;
            _localSysopName = localSysopName;
        }

        public bool PerformAsCaller(Stream stream)
        {
            if (!DoHandshakeAsCaller(stream, "CALLER")) return false;
            if (!DoReverseAsAnswerer(stream, "CALLER-REV")) return false;
            return true;
        }

        public bool PerformAsAnswerer(Stream stream)
        {
            if (!DoHandshakeAsAnswerer(stream, "ANSWERER")) return false;
            if (!DoReverseAsCaller(stream, "ANSWERER-REV")) return false;
            return true;
        }

        private bool DoHandshakeAsCaller(Stream stream, string tag)
        {
            SendLine(stream, "**EMSI_INQ");

            string req = ReadLine(stream, 5000);
            if (req == null || !req.StartsWith("**EMSI_REQ"))
                return Fail("Expected EMSI_REQ");

            SendLine(stream, "**EMSI_DAT" + BuildEmsiData());

            string ack = ReadLine(stream, 5000);
            if (ack == null || !ack.StartsWith("**EMSI_ACK"))
                return Fail("Expected EMSI_ACK");

            SendLine(stream, "**EMSI_ACK");
            return true;
        }

        private bool DoHandshakeAsAnswerer(Stream stream, string tag)
        {
            string inq = ReadLine(stream, 10000);
            if (inq == null || !inq.StartsWith("**EMSI_INQ"))
                return Fail("Expected EMSI_INQ");

            SendLine(stream, "**EMSI_REQ");

            string dat = ReadLine(stream, 10000);
            if (dat == null || !dat.StartsWith("**EMSI_DAT"))
                return Fail("Expected EMSI_DAT");

            ParseEmsiData(dat.Substring("**EMSI_DAT".Length));

            SendLine(stream, "**EMSI_ACK");

            string ack = ReadLine(stream, 5000);
            if (ack == null || !ack.StartsWith("**EMSI_ACK"))
                return Fail("Expected final EMSI_ACK");

            return true;
        }

        private bool DoReverseAsCaller(Stream stream, string tag)
        {
            SendLine(stream, "**EMSI_INQ");

            string req = ReadLine(stream, 5000);
            if (req == null || !req.StartsWith("**EMSI_REQ"))
                return Fail("Expected reverse EMSI_REQ");

            SendLine(stream, "**EMSI_DAT" + BuildEmsiData());

            string ack = ReadLine(stream, 5000);
            if (ack == null || !ack.StartsWith("**EMSI_ACK"))
                return Fail("Expected reverse EMSI_ACK");

            SendLine(stream, "**EMSI_ACK");
            return true;
        }

        private bool DoReverseAsAnswerer(Stream stream, string tag)
        {
            string inq = ReadLine(stream, 5000);
            if (inq == null || !inq.StartsWith("**EMSI_INQ"))
                return true;

            SendLine(stream, "**EMSI_REQ");

            string dat = ReadLine(stream, 5000);
            if (dat == null || !dat.StartsWith("**EMSI_DAT"))
                return Fail("Expected reverse EMSI_DAT");

            ParseEmsiData(dat.Substring("**EMSI_DAT".Length));

            SendLine(stream, "**EMSI_ACK");

            string ack = ReadLine(stream, 5000);
            if (ack == null || !ack.StartsWith("**EMSI_ACK"))
                return Fail("Expected final reverse EMSI_ACK");

            return true;
        }

        private string BuildEmsiData()
        {
            string data = " " + _localAddress.ToString() +
                          " SYS " + _localSystemName +
                          " ZYZ " + _localSysopName;

            ushort crc = ComputeCrc16(data.TrimStart());
            data += " " + crc.ToString("X4");
            return data;
        }

        private static ushort ComputeCrc16(string data)
        {
            ushort crc = 0;
            foreach (char c in data)
            {
                crc ^= (ushort)(c << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
                if (!Uri.IsHexDigit(c))
                    return false;
            return true;
        }

        private void ParseEmsiData(string data)
        {
            string[] parts = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 4 && IsHex(p)) continue;
                if (p.Contains(":") && p.Contains("/"))
                {
                    try { RemoteAddress = FidoAddress.Parse(p); }
                    catch { }
                }
                else if (p == "SYS" && i + 1 < parts.Length)
                    RemoteSystemName = parts[i + 1];
                else if (p == "ZYZ" && i + 1 < parts.Length)
                    RemoteSysopName = parts[i + 1];
            }
        }

        private void SendLine(Stream stream, string line)
        {
            byte[] data = Encoding.ASCII.GetBytes(line + "\r");
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        private string ReadLine(Stream stream, int timeoutMs)
        {
            var sb = new StringBuilder();
            stream.ReadTimeout = timeoutMs;
            try
            {
                while (true)
                {
                    int b = stream.ReadByte();
                    if (b == -1) break;
                    if (b == CR) break;
                    if (b != 0) sb.Append((char)b);
                }
            }
            catch { return null; }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private bool Fail(string reason)
        {
            if (Verbose)
                Console.WriteLine($"[EMSI] FAIL: {reason}");
            return false;
        }
    }
}