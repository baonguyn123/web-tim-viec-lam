namespace web_jobs.Models
{
    public class Chat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChatRoomId { get; set; }

        public Guid SenderUser_ID { get; set; }
        public string MessageText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
