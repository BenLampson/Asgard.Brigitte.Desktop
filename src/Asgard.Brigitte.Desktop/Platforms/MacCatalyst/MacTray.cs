using System.Runtime.InteropServices;
using System.Text.Json;
using Foundation;
namespace Asgard.Brigitte.Desktop;
/// <summary>AppKit 扩展拥有 NSStatusItem；Catalyst 业务层不调用不可用的 AppKit 绑定。</summary>
internal sealed class MacTray : IPlatformTray
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Callback(int action,nint project,double x,double y);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Start(Callback callback);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void SetProjects([MarshalAs(UnmanagedType.LPUTF8Str)]string json);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void ShowWindow([MarshalAs(UnmanagedType.LPUTF8Str)]string title,int visible);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void AttachWindow([MarshalAs(UnmanagedType.LPUTF8Str)]string title,int main);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Stop();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int SaveFile([MarshalAs(UnmanagedType.LPUTF8Str)]string name,byte[] bytes,nuint size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int ChooseClose(int canUseTray);
    private static nint _library;
    private readonly DesktopHost _host;
    private readonly Callback _callback;
    /// <summary>扩展加载失败时保持正常窗口，不将用户锁在后台。</summary>
    public bool IsAvailable{get;private set;}
    internal MacTray(DesktopHost host)
    {
        _host=host;_callback=OnAction;
        try{_library=NativeLibrary.Load(Path.Combine(NSBundle.MainBundle.ResourcePath,"native/libBrigitteTray.dylib"));IsAvailable=Function<Start>("BrigitteTrayStart")(_callback)==1;}catch(DllNotFoundException){IsAvailable=false;}
    }
    private static T Function<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library,name));
    private void OnAction(int action,nint project,double x,double y)
    {
        var id=project==0?null:Marshal.PtrToStringUTF8(project);
        MainThread.BeginInvokeOnMainThread(async()=>{switch(action){case 0:_host.ShowPopup(x,y);break;case 1:await _host.OpenAsync(new(DesktopViewKind.Main));break;case 2:_host.RefreshTrayData();break;case 3:_host.Quit();break;case 4:await _host.OpenAsync(new(DesktopViewKind.Main,ProjectId:id));break;case 5:await _host.RequestMainCloseAsync();break;}});
    }
    /// <summary>原始项目名称由原生菜单自动截断，完整值保留 tooltip。</summary>
    public void Refresh()
    {
        if (!IsAvailable) return;
        var strings = new Dictionary<string, string>
        {
            ["title"] = ClientText.T("Brigitte · BUG 管理器"), ["login"] = ClientText.T("登录 Brigitte"),
            ["open"] = ClientText.T("打开主窗口"), ["refresh"] = ClientText.T("刷新"), ["quit"] = ClientText.T("退出"),
            ["closeTitle"] = ClientText.T("关闭 Brigitte"), ["closeHelp"] = ClientText.T("退出程序会停止后台同步和通知。最小化到托盘会继续运行，可点击托盘图标重新打开。"),
            ["noTray"] = ClientText.T("系统托盘暂不可用。你可以退出程序，或取消并保留窗口。"),
            ["tray"] = ClientText.T("最小化到托盘"), ["exit"] = ClientText.T("退出程序"),
            ["cancel"] = ClientText.T("取消"), ["remember"] = ClientText.T("以后不再提示，记住这次选择")
        };
        Function<SetProjects>("BrigitteTraySetProjects")(JsonSerializer.Serialize(new
        {
            authenticated = _host.IsAuthenticated, strings,
            projects = _host.Projects.Select(p => new { id = p.Id, display = ClientText.T("{0} · {1} 未解决", p.Name, _host.ProjectCount(p.Id)) })
        }));
    }
    public void Notify(string title,string message)=>MacNotifications.Show(title,message);
    /// <summary>仅隐藏原生主窗口，应用继续保留状态栏入口。</summary>
    public void Show(Window window,bool visible){if(IsAvailable)Function<ShowWindow>("BrigitteTrayShowWindow")(window.Title,visible?1:0);else if(visible)Application.Current!.ActivateWindow(window);}
    internal static void Attach(string title,bool main){if(_library!=0)Function<AttachWindow>("BrigitteTrayAttachWindow")(title,main?1:0);}
    internal static void Save(string name,byte[] bytes){if(_library==0)throw new InvalidOperationException(ClientText.T("macOS 文件保存扩展未加载。"));Function<SaveFile>("BrigitteTraySaveFile")(name,bytes,(nuint)bytes.Length);}
    /// <summary>最低两位为动作，第三位为记住选择，取消永不写入偏好。</summary>
    internal static (string Action,bool Remember) PromptClose(bool canUseTray)
    {
        if(_library==0)return ("cancel",false);
        var result=Function<ChooseClose>("BrigitteTrayChooseClose")(canUseTray?1:0);
        return ((result&3) switch{1=>"tray",2=>"exit",_=>"cancel"},(result&4)!=0);
    }
    /// <summary>移除状态栏项并还原窗口委托。</summary>
    public void Dispose(){if(IsAvailable)Function<Stop>("BrigitteTrayStop")();IsAvailable=false;GC.KeepAlive(_callback);}
}
