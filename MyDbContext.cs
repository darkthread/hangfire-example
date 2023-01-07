using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HangfireExample
{
    public class MyDbContext : DbContext
    {
        public DbSet<LogEntry> LogEntries { get; set; }
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {
        }
    }

    public class LogEntry
    {
        [Key]
        public int Id { get; set; }
        public DateTime LogTime { get; set; } = DateTime.Now;
        public string Message { get; set; }
    }
}
