using System;

namespace FidoNet
{
    public enum MessageStatus
    {
        Pending,
        Sent,
        Failed,
        Received
    }

    public class ChatMessage
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Text { get; set; }
        public DateTime Date { get; set; }
        public bool IsOutgoing { get; set; }
        public MessageStatus Status { get; set; }
        public string PeerAddress { get; set; }
        public string Area { get; set; } = "LOCAL.CHAT";
    }
}