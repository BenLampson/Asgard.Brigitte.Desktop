using System.Runtime.CompilerServices;
using Asgard.Brigitte.Desktop.Core;

namespace Asgard.Brigitte.Desktop.Tests;

/// <summary>既有中文断言不依赖 CI 机器语言；英文用例使用各自的异步语言作用域。</summary>
internal static class TestLanguageDefaults
{
    [ModuleInitializer]
    internal static void Initialize() => ClientText.SetLanguage("zh-CN");
}
