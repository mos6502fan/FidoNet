using System;
using System.Collections.Generic;

namespace FidoNet
{
    public enum FidoMessageType
    {
        Netmail,
        Echomail
    }

    public class FidoMessage
    {
        public FidoAddress From { get; set; }
        public string FromName { get; set; }
        public FidoAddress To { get; set; }
        public string ToName { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public DateTime Date { get; set; }
        public FidoMessageType Type { get; set; }
        public string AreaTag { get; set; }
        public ushort Attribute { get; set; }
        public string MsgId { get; set; }
        public List<string> Kludges { get; set; } = new List<string>();

        public override string ToString()
        {
            string type = Type == FidoMessageType.Echomail ? $"Echo: {AreaTag}" : "Netmail";
            return $"[{Date:dd.MM.yy HH:mm}] {type}\n" +
                   $"From: {FromName} ({From})\n" +
                   $"To:   {ToName} ({To})\n" +
                   $"Subj: {Subject}\n" +
                   $"{Body}\n";
        }
    }
}