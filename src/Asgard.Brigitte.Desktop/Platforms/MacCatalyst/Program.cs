using UIKit;
namespace Asgard.Brigitte.Desktop;
/// <summary>macOS 应用启动。</summary>
public static class Program
{
    /// <summary>进入 UIKit 主线程。</summary>
    public static void Main(string[] args)=>UIApplication.Main(args,null,typeof(AppDelegate));
}
