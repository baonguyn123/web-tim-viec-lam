namespace web_jobs.Models
{
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid ChatSessionId { get; set; }
        public virtual ChatSession ChatSession { get; set; }
        public string Role { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}