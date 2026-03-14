namespace web_jobs.Dtos
{
    public class EmployerRegisterDto
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; } = "Employer";
    }
}
