using Asgard.Brigitte.Desktop.Core;
using Asgard.Brigitte.Desktop.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Asgard.Brigitte.Desktop.Tests;
/// <summary>验证品牌欢迎页的恢复、等待和错误状态不会丢失认证操作。</summary>
public sealed class WelcomeRenderTests
{
    /// <summary>验证会话时只显示等待状态，不能重复打开浏览器认证。</summary>
    [Fact]
    public async Task Verifying_session_hides_login_actions()
    {
        var html = await Render(new() { [nameof(WelcomeView.Verifying)] = true });
        Assert.Contains("正在验证登录", html);
        Assert.DoesNotContain("使用 Heimdall 登录", html);
        Assert.DoesNotContain("class=\"brand-login", html);
        await WindowRenderTests.ExportPreview("verifying", "<div class='client light'>" + html + "</div>");
    }
    /// <summary>恢复失败仍能重试、重新登录或注册；执行中的操作均不可重复提交。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_session_keeps_both_recovery_actions_and_disables_them_while_busy(bool busy)
    {
        var html = await Render(new() { [nameof(WelcomeView.RestorePending)] = true, [nameof(WelcomeView.Busy)] = busy, [nameof(WelcomeView.Restoring)] = busy, [nameof(WelcomeView.Error)] = "连接暂时不可用" });
        Assert.Contains(busy ? "正在恢复登录" : "重试恢复登录", html);
        Assert.Contains("重新登录", html);
        Assert.Contains("role=\"alert\">连接暂时不可用", html);
        Assert.Contains("注册新账号", html);
        Assert.Equal(busy ? 3 : 0, html.Split(" disabled").Length - 1);
        await WindowRenderTests.ExportPreview("restore-" + busy, "<div class='client light'>" + html + "</div>");
    }
    private static async Task<string> Render(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection(); services.AddLogging();
        // 欢迎页与原生工具入口使用同一宿主边界，不打开浏览器或访问真实服务。
        services.AddSingleton<IClientHost>(new RenderHost("light"));
        services.AddSingleton<IOptions<ClientOptions>>(Options.Create(new ClientOptions()));
        using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var view = await renderer.RenderComponentAsync<WelcomeView>(ParameterView.FromDictionary(parameters));
            return System.Net.WebUtility.HtmlDecode(view.ToHtmlString());
        });
    }
}
