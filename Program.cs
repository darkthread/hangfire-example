using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage.SQLite;
using HangfireExample;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite ��Ʈw�s�u�r��
var dbPath = "demo.db";
var cs = "data source=" + dbPath;

// ���U DbContext
builder.Services.AddDbContext<MyDbContext>(options => 
    options.UseSqlite(cs)
        .LogTo(Console.WriteLine, LogLevel.Critical)
    );

// ���U Hangfire�A�ϥ� SQLite �x�s
// �`�N�GUseSQLiteStorage() �ѼƬ���Ʈw���|�A���O�s�u�r��
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(dbPath));
builder.Services.AddHangfireServer();

// �ϥ��X�R��k���U�Ƶ{�u�@����
builder.AddSchTaskWorker();

// �]�w Windows ��X������
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
builder.Services.AddAuthorization(options =>
{
    // �H�U�i�]�����ݵn�J�~��ϥΡA�ΦW MapGet/MapPost �[ AllowAnonymous() �ư�
    //options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

// �������ұM�ΡG�R���í��ظ�Ʈw
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

// �[�J�{�Ҥα��v�����n��
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard(options: new DashboardOptions
{
    IsReadOnlyFunc = (DashboardContext context) =>
        DashboardAccessAuthFilter.IsReadOnly(context),
    Authorization = new[] { new DashboardAccessAuthFilter() }
});

// �ϥ��X�R��k�]�w�Ƶ{�u�@
app.SetSchTasks();

app.MapGet("/", (MyDbContext dbctx) =>
    string.Join("\n",
        dbctx.LogEntries.Select(le => $"{le.LogTime:HH:mm:ss} {le.Message}").ToArray()));

app.Run();

public class DashboardAccessAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        //�̾ڨӷ�IP�B�n�J�b���M�w�i�_�s��
        //�Ҧp�G�w�n�J�̥i�s��
        var userId = context.GetHttpContext().User.Identity;
        var isAuthed = userId?.IsAuthenticated ?? false;
        if (!isAuthed)
        {
            // ���] options.FallbackPolicy = options.DefaultPolicy ���ܭn�[�o�q
            // �o Challenge �{�ǡAex: �^�� 401 Ĳ�o�n�J�����B�ɦV�n�J����..
            context.GetHttpContext().ChallengeAsync()
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return false;
        }
        // �ˬd�n�J��
        return true;
    }
    public static bool IsReadOnly(DashboardContext context)
    {
        var clientIp = context.Request.RemoteIpAddress.ToString();
        var isLocal = "127.0.0.1,::1".Split(',').Contains(clientIp);
        //�̾ڨӷ�IP�B�n�J�b���M�w�i�_�s��
        //�Ҧp�G�D�����s���u��Ū��
        return !isLocal;
    }
}
