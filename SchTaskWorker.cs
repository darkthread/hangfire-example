using System.Linq.Expressions;
using Hangfire;

namespace HangfireExample
{
    public class SchTaskWorker
    {
        private readonly IServiceProvider _services;

        int _counter = 0;

        // 取得 IServiceProvider 稍後建立 Scoped 範圍的  DbContext
        // https://blog.darkthread.net/blog/aspnetcore-use-scoped-in-singleton/
        public SchTaskWorker(IServiceProvider services)
        {
            _services = services;
        }
        // 設定定期排程工作
        public void SetSchTasks()
        {
            SetSchTask("InsertLogEveryMinute", () => InsertLog(), "* * * * *");
        }

        // 先刪再設，避免錯過時間排程在伺服器啟動時執行
        // https://blog.darkthread.net/blog/missed-recurring-job-in-hangfire/
        void SetSchTask(string id, Expression<Action> job, string cron)
        {
            RecurringJob.RemoveIfExists(id);
            RecurringJob.AddOrUpdate(id, job, cron, TimeZoneInfo.Local);
        }
        // 每分鐘寫入一筆 Log 到資料庫
        public void InsertLog()
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                db.LogEntries.Add(new LogEntry { Message = $"Test {_counter++}" });
                db.SaveChanges();
            }
        }
    }
    // 擴充方法，註冊排程工作元件以及設定排程
    public static class SchTaskWorkerExtensions
    {
        public static WebApplicationBuilder AddSchTaskWorker(this WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<SchTaskWorker>();
            return builder;
        }

        public static void SetSchTasks(this WebApplication app)
        {
            var worker = app.Services.GetRequiredService<SchTaskWorker>();
            worker.SetSchTasks();
        }
    }
}
