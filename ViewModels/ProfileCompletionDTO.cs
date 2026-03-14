namespace web_jobs.ViewModels
{
    public class ProfileCompletionDTO
    {
        public int Percent { get; set; }
        public List<string> MissingItems { get; set; } = new();
    }
}
