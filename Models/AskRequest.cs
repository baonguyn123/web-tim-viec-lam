namespace web_jobs.Models
{
    public class AskRequest
    {
        public string CurrentMessage { get; set; }
        public Guid? SessionId { get; set; }

        public Guid? JobId { get; set; }
    }
}
