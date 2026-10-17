param([Parameter(Mandatory)][int]$ProcessId,[ValidateSet('Inspect','TitleColors','CloseMain','HoverTray','ClickTray','RestoreMain','MaximizeMain','MinimizeMain','NormalizeMain')][string]$Action='Inspect')
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class BrigitteSmoke {
 public delegate bool EnumProc(IntPtr window,IntPtr data);
 [DllImport("user32")]static extern bool EnumWindows(EnumProc callback,IntPtr data);
 [DllImport("user32")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder value,int max);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder value,int max);
 [DllImport("user32")]public static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32")]public static extern bool IsZoomed(IntPtr window);
 [DllImport("user32")]public static extern bool IsIconic(IntPtr window);
 [DllImport("user32")]public static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
 [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32")]static extern IntPtr GetWindowDC(IntPtr window);
 [DllImport("user32")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("user32")]static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
 [DllImport("gdi32")]static extern uint GetPixel(IntPtr dc,int x,int y);
 [DllImport("user32")]static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
 [DllImport("gdi32")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32")]static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
 [DllImport("gdi32")]static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
 [DllImport("gdi32")]static extern bool DeleteObject(IntPtr value);
 [DllImport("gdi32")]static extern bool DeleteDC(IntPtr dc);
 public static string[] Colors(IntPtr window) {GetWindowRect(window,out var rect);int width=rect.Right-rect.Left;var screen=GetDC(IntPtr.Zero);var dc=CreateCompatibleDC(screen);var bitmap=CreateCompatibleBitmap(screen,width,rect.Bottom-rect.Top);var previous=SelectObject(dc,bitmap);try {if(!PrintWindow(window,dc,2))throw new InvalidOperationException("窗口未提供可采样的完整合成画面");return new[]{Hex(GetPixel(dc,Math.Min(500,width/2),16)),Hex(GetPixel(dc,width-130,16)),Hex(GetPixel(dc,width-84,16)),Hex(GetPixel(dc,width-38,16))};}finally{SelectObject(dc,previous);DeleteObject(bitmap);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}}
 static string Hex(uint value)=>value==0xffffffff?"unavailable":$"#{value&255:X2}{(value>>8)&255:X2}{(value>>16)&255:X2}";
 public sealed class Window {public long Handle{get;set;}public string Class{get;set;}="";public string Title{get;set;}="";public bool Visible{get;set;}public bool Maximized{get;set;}public bool Minimized{get;set;}public int Left{get;set;}public int Top{get;set;}public int Width{get;set;}public int Height{get;set;}}
 public static Window[] Find(uint process) {var result=new List<Window>();EnumWindows((handle,data)=>{GetWindowThreadProcessId(handle,out uint owner);if(owner==process){var name=new StringBuilder(256);var title=new StringBuilder(256);GetClassName(handle,name,256);GetWindowText(handle,title,256);GetWindowRect(handle,out var rect);result.Add(new Window{Handle=handle.ToInt64(),Class=name.ToString(),Title=title.ToString(),Visible=IsWindowVisible(handle),Maximized=IsZoomed(handle),Minimized=IsIconic(handle),Left=rect.Left,Top=rect.Top,Width=rect.Right-rect.Left,Height=rect.Bottom-rect.Top});}return true;},IntPtr.Zero);return result.ToArray();}
}
'@
$windows=[BrigitteSmoke]::Find($ProcessId)
$main=$windows | Where-Object {$_.Title -eq 'Brigitte · BUG 管理器'} | Select-Object -First 1
$tray=$windows | Where-Object {$_.Class.StartsWith('Brigitte.Tray.')} | Select-Object -First 1
switch($Action) {
 'TitleColors' {if(!$main){throw '没有找到本次客户端主窗口'}; $colors=[BrigitteSmoke]::Colors([IntPtr]$main.Handle);[pscustomobject]@{Title=$colors[0];Minimize=$colors[1];Maximize=$colors[2];Close=$colors[3];Uniform=($colors[0] -eq $colors[1] -and $colors[0] -eq $colors[2] -and $colors[0] -eq $colors[3])}|ConvertTo-Json;return}
 'CloseMain' {if(!$main){throw '没有找到本次客户端主窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$main.Handle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null}
 'MaximizeMain' {if(!$main){throw '没有找到本次客户端主窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$main.Handle,0x112,[IntPtr]0xF030,[IntPtr]::Zero)|Out-Null}
 'MinimizeMain' {if(!$main){throw '没有找到本次客户端主窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$main.Handle,0x112,[IntPtr]0xF020,[IntPtr]::Zero)|Out-Null}
 'NormalizeMain' {if(!$main){throw '没有找到本次客户端主窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$main.Handle,0x112,[IntPtr]0xF120,[IntPtr]::Zero)|Out-Null}
 'HoverTray' {if(!$tray){throw '没有找到本次客户端托盘消息窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$tray.Handle,0x8001,[IntPtr]::Zero,[IntPtr]0x200)|Out-Null}
 'ClickTray' {if(!$tray){throw '没有找到本次客户端托盘消息窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$tray.Handle,0x8001,[IntPtr]::Zero,[IntPtr]0x201)|Out-Null}
 'RestoreMain' {if(!$tray){throw '没有找到本次客户端托盘消息窗口'};[BrigitteSmoke]::PostMessage([IntPtr]$tray.Handle,0x8001,[IntPtr]::Zero,[IntPtr]0x203)|Out-Null}
}
[BrigitteSmoke]::Find($ProcessId)|Where-Object {$_.Title.StartsWith('Brigitte') -or $_.Class.StartsWith('Brigitte.Tray.')}|ConvertTo-Json
