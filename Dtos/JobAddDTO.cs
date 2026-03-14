namespace web_jobs.Dtos
{
    public class JobAddDTO
    {
        public string JobTitle { get; set; }

        public string JobDescription { get; set; }

        public string Requirements { get; set; }
        public string Salary { get; set; }
        public string Benefits { get; set; }
        public int JobTypeId { get; set; }
        public DateTime PostedDate { get; set; } = DateTime.Now;
        public DateTime? ApplicationDeadline { get; set; }
        public string Locate { get; set; }
        public int CategoryId { get; set; }
    }
}
