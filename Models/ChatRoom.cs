namespace web_jobs.Models
{
    public class ChatRoom
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid Job_ID { get; set; }
        public Guid CandidateUser_ID { get; set; }
        public Guid EmployerUser_ID { get; set; }

        public bool IsActive { get; set; } = false;

        // Inbox
        public string? LastMessage { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
