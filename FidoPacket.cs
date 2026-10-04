using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FidoNet
{
    public class FidoPacket
    {
        public FidoAddress OrigAddress { get; set; }
        public FidoAddress DestAddress { get; set; }
        public string Password { get; set; } = "";
        public List<FidoMessage> Messages { get; set; } = new List<FidoMessage>();

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                WriteHeader(bw);
                foreach (var msg in Messages)
                    WriteMessage(bw, msg);
                bw.Write((ushort)0);
            }
        }

        private void WriteHeader(BinaryWriter bw)
        {
            bw.Write((ushort)OrigAddress.Node);
            bw.Write((ushort)DestAddress.Node);
            bw.Write((ushort)OrigAddress.Net);
            bw.Write((ushort)DestAddress.Net);

            bw.Write((ushort)0);
            bw.Write((ushort)0);

            bw.Write(BuildDateTime(DateTime.Now));

            bw.Write((ushort)0);
            bw.Write((ushort)0x0100);

            bw.Write((ushort)OrigAddress.Zone);
            bw.Write((ushort)DestAddress.Zone);
            bw.Write((ushort)OrigAddress.Point);
            bw.Write((ushort)DestAddress.Point);

            bw.Write((uint)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);

            byte[] pwd = new byte[8];
            byte[] pwdBytes = Encoding.ASCII.GetBytes(Password ?? "");
            Array.Copy(pwdBytes, pwd, Math.Min(pwdBytes.Length, 8));
            bw.Write(pwd);

            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
        }

        private static byte[] BuildDateTime(DateTime dt)
        {
            byte[] buf = new byte[20];
            string s = dt.ToString("dd MMM yy  HH:mm:ss");
            byte[] raw = Encoding.ASCII.GetBytes(s);
            Array.Copy(raw, buf, Math.Min(raw.Length, 20));
            return buf;
        }

        private void WriteMessage(BinaryWriter bw, FidoMessage msg)
        {
            bw.Write((ushort)msg.From.Node);
            bw.Write((ushort)msg.To.Node);
            bw.Write((ushort)msg.From.Net);
            bw.Write((ushort)msg.To.Net);
            bw.Write(msg.Attribute);
            bw.Write((ushort)0);
            bw.Write(BuildDateTime(msg.Date));
            bw.Write((ushort)msg.To.Node);
            bw.Write((ushort)msg.From.Node);
            bw.Write((ushort)msg.To.Net);
            bw.Write((ushort)msg.From.Net);
            bw.Write((ushort)msg.To.Zone);
            bw.Write((ushort)msg.From.Zone);
            bw.Write((ushort)msg.To.Point);
            bw.Write((ushort)msg.From.Point);
            bw.Write((uint)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);

            WriteNullString(bw, msg.ToName, 36);
            WriteNullString(bw, msg.FromName, 36);
            WriteNullString(bw, msg.Subject, 72);

            string body = msg.Body ?? "";
            var kludges = new List<string>();

            if (msg.From.Point > 0)
                kludges.Add($"FMPT:{msg.From.Point}");
            if (msg.To.Point > 0)
                kludges.Add($"TOPT:{msg.To.Point}");

            string msgId = msg.MsgId ?? $"{msg.From} {DateTime.Now:ddMMyyHHmmss}";
            kludges.Add($"MSGID: {msgId}");

            if (msg.Type == FidoMessageType.Echomail && !string.IsNullOrEmpty(msg.AreaTag))
                kludges.Add($"AREA:{msg.AreaTag}");

            foreach (var k in msg.Kludges)
                kludges.Add(k);

            var sb = new StringBuilder();
            foreach (var k in kludges)
            {
                sb.Append(k);
                sb.Append('\r');
            }
            sb.Append(body);

            string finalBody = sb.ToString();
            if (finalBody.Length > 0 && !finalBody.EndsWith("\r"))
                finalBody += "\r";

            byte[] bodyBytes = Encoding.GetEncoding(866).GetBytes(finalBody);
            bw.Write(bodyBytes);
            bw.Write((byte)0);
        }

        private static void WriteNullString(BinaryWriter bw, string s, int max)
        {
            string val = s ?? "";
            if (val.Length > max - 1)
                val = val.Substring(0, max - 1);
            byte[] bytes = Encoding.GetEncoding(866).GetBytes(val);
            bw.Write(bytes);
            bw.Write((byte)0);
        }

        public static FidoPacket Read(string path)
        {
            var packet = new FidoPacket();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                ReadHeader(br, packet);

                while (fs.Position < fs.Length - 2)
                {
                    long pos = fs.Position;
                    ushort first = br.ReadUInt16();
                    if (first == 0)
                        break;
                    fs.Position = pos;

                    var msg = ReadMessage(br);
                    if (msg == null) break;
                    packet.Messages.Add(msg);
                }
            }
            return packet;
        }

        private static void ReadHeader(BinaryReader br, FidoPacket packet)
        {
            ushort origNode = br.ReadUInt16();
            ushort destNode = br.ReadUInt16();
            ushort origNet = br.ReadUInt16();
            ushort destNet = br.ReadUInt16();

            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadBytes(20);
            br.ReadUInt16();
            br.ReadUInt16();

            ushort origZone = br.ReadUInt16();
            ushort destZone = br.ReadUInt16();
            ushort origPoint = br.ReadUInt16();
            ushort destPoint = br.ReadUInt16();

            br.ReadUInt32();
            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();

            byte[] pwd = br.ReadBytes(8);
            packet.Password = Encoding.ASCII.GetString(pwd).TrimEnd('\0');

            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();

            packet.OrigAddress = new FidoAddress(origZone, origNet, origNode, origPoint);
            packet.DestAddress = new FidoAddress(destZone, destNet, destNode, destPoint);
        }

        private static FidoMessage ReadMessage(BinaryReader br)
        {
            var msg = new FidoMessage();

            ushort origNode = br.ReadUInt16();
            ushort destNode = br.ReadUInt16();
            ushort origNet = br.ReadUInt16();
            ushort destNet = br.ReadUInt16();
            msg.Attribute = br.ReadUInt16();
            br.ReadUInt16();

            byte[] dt = br.ReadBytes(20);
            msg.Date = ParseDateTime(dt);

            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();
            ushort destZone = br.ReadUInt16();
            ushort origZone = br.ReadUInt16();
            ushort destPoint = br.ReadUInt16();
            ushort origPoint = br.ReadUInt16();
            br.ReadUInt32();
            br.ReadUInt16();
            br.ReadUInt16();
            br.ReadUInt16();

            msg.ToName = ReadNullString(br, 36);
            msg.FromName = ReadNullString(br, 36);
            msg.Subject = ReadNullString(br, 72);

            msg.From = new FidoAddress(origZone, origNet, origNode, origPoint);
            msg.To = new FidoAddress(destZone, destNet, destNode, destPoint);

            var bodyBuilder = new List<byte>();
            while (true)
            {
                int b = br.ReadByte();
                if (b == 0) break;
                bodyBuilder.Add((byte)b);
            }

            string body = Encoding.GetEncoding(866).GetString(bodyBuilder.ToArray());

            msg.Body = body;
            msg.Type = FidoMessageType.Netmail;

            string[] lines = body.Split('\r');
            var cleanLines = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("AREA:"))
                {
                    msg.Type = FidoMessageType.Echomail;
                    msg.AreaTag = line.Substring(5).Trim();
                }
                else if (line.StartsWith("MSGID:"))
                {
                    msg.MsgId = line.Substring(6).Trim();
                    msg.Kludges.Add(line);
                }
                else if (line.StartsWith("FMPT:"))
                {
                    if (int.TryParse(line.Substring(5).Trim(), out int p))
                        msg.From.Point = p;
                    msg.Kludges.Add(line);
                }
                else if (line.StartsWith("TOPT:"))
                {
                    if (int.TryParse(line.Substring(5).Trim(), out int p))
                        msg.To.Point = p;
                    msg.Kludges.Add(line);
                }
                else if (line.StartsWith("SEEN-BY") || line.StartsWith("PATH") ||
                         line.StartsWith("PID") || line.StartsWith("CHRS"))
                {
                    msg.Kludges.Add(line);
                }
                else
                {
                    cleanLines.Add(line);
                }
            }
            msg.Body = string.Join("\r", cleanLines).TrimEnd('\r', '\n', ' ');

            return msg;
        }

        private static DateTime ParseDateTime(byte[] buf)
        {
            string s = Encoding.ASCII.GetString(buf).TrimEnd('\0', ' ');
            if (DateTime.TryParseExact(s, "dd MMM yy  HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
            return DateTime.Now;
        }

        private static string ReadNullString(BinaryReader br, int max)
        {
            var bytes = new List<byte>();
            for (int i = 0; i < max; i++)
            {
                byte b = br.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.GetEncoding(866).GetString(bytes.ToArray());
        }
    }
}