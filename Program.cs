using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage.SQLite;
using HangfireExample;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite 資料庫連線字串
var dbPath = "demo.db";
var cs = "data source=" + dbPath;

// 註冊 DbContext
builder.Services.AddDbContext<MyDbContext>(options => 
    options.UseSqlite(cs)
        .LogTo(Console.WriteLine, LogLevel.Critical)
    );

// 註冊 Hangfire，使用 SQLite 儲存
// 注意：UseSQLiteStorage() 參數為資料庫路徑，不是連線字串
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(dbPath));
builder.Services.AddHangfireServer();

// 使用擴充方法註冊排程工作元件
builder.AddSchTaskWorker();

// 設定 Windows 整合式驗證
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    // 以下可設全站需登入才能使用，匿名 MapGet/MapPost 加 AllowAnonymous() 排除
    //options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

// 測試環境專用：刪除並重建資料庫
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

// 加入認證及授權中介軟體
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard(options: new DashboardOptions
{
    IsReadOnlyFunc = (DashboardContext context) =>
        DashboardAccessAuthFilter.IsReadOnly(context),
    Authorization = new[] { new DashboardAccessAuthFilter() }
});

// 使用擴充方法設定排程工作
app.SetSchTasks();

app.MapGet("/", (MyDbContext dbctx) =>
    string.Join("\n",
        dbctx.LogEntries.Select(le => $"{le.LogTime:HH:mm:ss} {le.Message}").ToArray()));

app.Run();

public class DashboardAccessAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        //依據來源IP、登入帳號決定可否存取
        //例如：已登入者可存取
        var userId = context.GetHttpContext().User.Identity;
        var isAuthed = userId?.IsAuthenticated ?? false;
        if (!isAuthed)
        {
            // 未設 options.FallbackPolicy = options.DefaultPolicy 的話要加這段
            // 發 Challenge 程序，ex: 回傳 401 觸發登入視窗、導向登入頁面..
            context.GetHttpContext().ChallengeAsync()
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return false;
        }
        // 檢查登入者
        return true;
    }
    public static bool IsReadOnly(DashboardContext context)
    {
        var clientIp = context.Request.RemoteIpAddress.ToString();
        var isLocal = "127.0.0.1,::1".Split(',').Contains(clientIp);
        //依據來源IP、登入帳號決定可否存取
        //例如：非本機存取只能讀取
        return !isLocal;
    }
}
