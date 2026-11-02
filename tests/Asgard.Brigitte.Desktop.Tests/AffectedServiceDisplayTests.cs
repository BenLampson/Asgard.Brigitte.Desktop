using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>已知服务随界面语言显示，自定义服务保留原名。</summary>
public sealed class AffectedServiceDisplayTests
{
    [Theory]
    [InlineData("frontend", "前端", "Frontend")]
    [InlineData("FrontEnd", "前端", "Frontend")]
    [InlineData("backend", "后端", "Backend")]
    [InlineData("database", "数据库", "Database")]
    [InlineData("nginx", "Nginx", "Nginx")]
    [InlineData("configuration", "配置", "Configuration")]
    [InlineData("scheduled-job", "定时任务", "Scheduled job")]
    [InlineData("message-queue", "消息队列", "Message queue")]
    [InlineData("frontend-api", "frontend-api", "frontend-api")]
    [InlineData("自定义服务", "自定义服务", "自定义服务")]
    public void Labels_only_known_service_identifiers(string value, string chinese, string english)
    {
        using (ClientText.UseLanguage("zh-CN")) Assert.Equal(chinese, AffectedServiceDisplay.Label(value));
        using (ClientText.UseLanguage("en")) Assert.Equal(english, AffectedServiceDisplay.Label(value));
    }
}
