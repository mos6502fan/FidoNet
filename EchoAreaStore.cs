using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FidoNet
{
    public class EchoAreaStore
    {
        private readonly string _basePath;

        public EchoAreaStore(string basePath)
        {
            _basePath = basePath;
            Directory.CreateDirectory(_basePath);
        }

        public void Append(string area, FidoMessage msg)
        {
            string areaDir = Path.Combine(_basePath, SanitizeArea(area));
            Directory.CreateDirectory(areaDir);

            string file = Path.Combine(areaDir, $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.msg");
            var sb = new StringBuilder();
            sb.AppendLine($"From: {msg.FromName} <{msg.From}>");
            sb.AppendLine($"To: {msg.ToName} <{msg.To}>");
            sb.AppendLine($"Subject: {msg.Subject}");
            sb.AppendLine($"Date: {msg.Date:O}");
            if (!string.IsNullOrEmpty(msg.MsgId))
                sb.AppendLine($"MsgId: {msg.MsgId}");
            sb.AppendLine();
            sb.AppendLine(msg.Body);
            File.WriteAllText(file, sb.ToString(), Encoding.GetEncoding(866));
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