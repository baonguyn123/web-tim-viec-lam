using Microsoft.AspNetCore.Identity;

namespace web_jobs.Models
{
    public class AppUser : IdentityUser
    {

        public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    }
}
