using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FidoNet
{
    public class FidoTosser
    {
        public string BasePath { get; set; }
        public List<string> SubscribedAreas { get; set; } = new List<string>();

        public FidoTosser(string basePath)
        {
            BasePath = basePath;
            Directory.CreateDirectory(BasePath);
        }

        public void ProcessPacket(FidoPacket packet, FidoAddress myAddress)
        {
            foreach (var msg in packet.Messages)
            {
                if (msg.Type == FidoMessageType.Echomail && !string.IsNullOrEmpty(msg.AreaTag))
                {
                    if (!SubscribedAreas.Contains(msg.AreaTag, StringComparer.OrdinalIgnoreCase))
                        continue;
                    SaveEchomail(msg, myAddress);
                }
                else
                {
                    SaveNetmail(msg, myAddress);
                }
            }
        }

        private void SaveEchomail(FidoMessage msg, FidoAddress myAddress)
        {
            string areaDir = Path.Combine(BasePath, "echo", SanitizeArea(msg.AreaTag));
            Directory.CreateDirectory(areaDir);

            string file = Path.Combine(areaDir, $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.msg");
            File.WriteAllText(file, FormatForStorage(msg, myAddress), Encoding.GetEncoding(866));
        }

        private void SaveNetmail(FidoMessage msg, FidoAddress myAddress)
        {
            if (msg.To.Node != myAddress.Node || msg.To.Net != myAddress.Net ||
                msg.To.Zone != myAddress.Zone)
                return;

            string netDir = Path.Combine(BasePath, "netmail");
            Directory.CreateDirectory(netDir);

            string file = Path.Combine(netDir, $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.msg");
            File.WriteAllText(file, FormatForStorage(msg, myAddress), Encoding.GetEncoding(866));
        }

        private string FormatForStorage(FidoMessage msg, FidoAddress myAddress)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"From: {msg.FromName} <{msg.From}>");
            sb.AppendLine($"To: {msg.ToName} <{msg.To}>");
            sb.AppendLine($"Subject: {msg.Subject}");
            sb.AppendLine($"Date: {msg.Date:yyyy-MM-dd HH:mm:ss}");

            if (msg.Type == FidoMessageType.Echomail)
                sb.AppendLine($"Area: {msg.AreaTag}");

            if (!string.IsNullOrEmpty(msg.MsgId))
                sb.AppendLine($"MsgId: {msg.MsgId}");

            foreach (var k in msg.Kludges)
                sb.AppendLine($"Kludge: {k}");

            sb.AppendLine();
            sb.AppendLine(msg.Body);
            return sb.ToString();
        }

        private string SanitizeArea(string area)
        {
            var sb = new StringBuilder();
            foreach (char c in area)
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                    sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "UNKNOWN";
        }
    }
}