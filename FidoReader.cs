using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FidoNet
{
    public static class FidoReader
    {
        public static FidoMessage ParseMessage(string[] lines)
        {
            var msg = new FidoMessage();
            var body = new StringBuilder();
            bool inBody = false;

            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();

                if (line.Length == 0 && !inBody)
                {
                    inBody = true;
                    continue;
                }

                if (inBody)
                {
                    body.AppendLine(line);
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon < 0) continue;

                string key = line.Substring(0, colon).Trim().ToUpper();
                string val = line.Substring(colon + 1).Trim();

                switch (key)
                {
                    case "FROM":
                        msg.FromName = val;
                        int lt = val.IndexOf('<');
                        if (lt >= 0)
                        {
                            msg.FromName = val.Substring(0, lt).Trim();
                            msg.From = FidoAddress.Parse(val.Substring(lt + 1).TrimEnd('>'));
                        }
                        break;

                    case "TO":
                        msg.ToName = val;
                        int lt2 = val.IndexOf('<');
                        if (lt2 >= 0)
                        {
                            msg.ToName = val.Substring(0, lt2).Trim();
                            msg.To = FidoAddress.Parse(val.Substring(lt2 + 1).TrimEnd('>'));
                        }
                        break;

                    case "SUBJ":
                    case "SUBJECT":
                        msg.Subject = val;
                        break;

                    case "DATE":
                        if (DateTime.TryParse(val, out var d))
                            msg.Date = d;
                        break;

                    case "AREA":
                        msg.Type = FidoMessageType.Echomail;
                        msg.AreaTag = val;
                        break;

                    case "SEEN-BY":
                    case "PATH":
                        msg.Kludges.Add($"{key}: {val}");
                        break;
                }
            }

            msg.Body = body.ToString().TrimEnd();
            if (msg.Date == default)
                msg.Date = DateTime.Now;

            return msg;
        }

        public static List<FidoMessage> ParseBundle(string path)
        {
            var messages = new List<FidoMessage>();
            string[] allLines = File.ReadAllLines(path, Encoding.GetEncoding(1251));
            var currentMsgLines = new List<string>();

            foreach (string line in allLines)
            {
                if (line.Trim().Length == 0 && currentMsgLines.Count > 0)
                {
                    if (currentMsgLines.Exists(l => l.Trim().Length > 0))
                    {
                        messages.Add(ParseMessage(currentMsgLines.ToArray()));
                        currentMsgLines.Clear();
                    }
                }
                else
                {
                    currentMsgLines.Add(line);
                }
            }

            if (currentMsgLines.Count > 0 && currentMsgLines.Exists(l => l.Trim().Length > 0))
                messages.Add(ParseMessage(currentMsgLines.ToArray()));

            return messages;
        }
    }
}