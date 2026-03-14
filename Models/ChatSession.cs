namespace web_jobs.Models
{
    public class ChatSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<ChatMessage> Messages { get; set; }
    }
}
