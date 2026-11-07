// Derived from SalaryGlow's WPF/tray lifecycle (MIT, BarryD-777, 2026).
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SalaryBeat.Core;
using Forms=System.Windows.Forms;
namespace SalaryBeat.Desktop;
public static class Program {
 [STAThread] public static void Main(string[] args) {
  if(args.Length==2 && args[0]=="--ui-regression"){
   var regressionApp=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   try{UiRegression.Run(args[1]);}catch(Exception ex){MessageBox.Show("界面回归失败："+ex.Message,"薪跳");Environment.ExitCode=1;}
   return;
  }
  if(args.Length==2 && args[0]=="--render-preview"){
   var previewApp=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   try {new Shell(previewApp,Path.Combine(Path.GetTempPath(),"SalaryBeat-Preview-"+Guid.NewGuid())).RenderPreviews(args[1]);}
   catch(Exception ex){MessageBox.Show("界面自检失败："+ex.Message,"薪跳");Environment.ExitCode=1;}
   return;
  }
  using var legacyMutex=new System.Threading.Mutex(true,"Local\\SalaryGlow.Leo",out bool legacyOwner);
  if(!legacyOwner){MessageBox.Show("请先从托盘退出旧测试版 SalaryGlow，再打开薪跳。","薪跳");return;}
  using var mutex=new System.Threading.Mutex(true,"Local\\SalaryBeat",out bool owner);
  if(!owner) { MessageBox.Show("薪跳已在运行，请通过右下角托盘图标打开。","薪跳"); return; }
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
  try { new Shell(app).Run(); app.Run(); }
  catch(Exception ex) {MessageBox.Show("薪跳未能启动："+ex.Message,"薪跳");}
 }
}
public sealed partial class Shell {
 readonly Application app; readonly SettingsStore store; readonly HolidayCalendar calendar;
 readonly string dataFolder; SettingsBook book;
 Window main=null!,mini=null!; SettingsView? settings;
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
 Forms.NotifyIcon tray=null!; readonly Odometer big=new(){DigitSize=52},small=new(){DigitSize=40};
 TextBlock miniDetails=null!;
 TextBlock title=null!,otherLabel=null!,otherAmount=null!,rate=null!,status=null!,notice=null!,date=null!,foot=null!,miniTitle=null!;
 Border panel=null!; ProgressBar progress=null!; Button secondary=null!;
 string error=""; bool dark,exiting; IntPtr miniHwnd; HwndSource? miniSource;
 public Shell(Application app,string? previewFolder=null) {
  this.app=app;
  dataFolder=previewFolder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SalaryBeat");
  if(previewFolder==null)ProfileMigration.ImportLegacy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SalaryGlowLeo"),dataFolder);
  store=new SettingsStore(dataFolder); book=store.Load();
  calendar=HolidayCalendar.LoadBundled();
  string userCalendars=Path.Combine(dataFolder,"calendars");
  if(Directory.Exists(userCalendars))foreach(var file in Directory.GetFiles(userCalendars,"*.json").Order())calendar.Import(file);
  if(store.Recovered) error="设置已从备份恢复，请核对并保存。";
 }
 public void Run() {
  BuildWindows(); CreateTray(); ApplyTheme(); ApplyStartup();
  var area=SystemParameters.WorkArea;var dock=DesktopLayout.BottomRight(area.Left,area.Top,area.Width,area.Height);
  main.Width=dock.Width;main.Height=dock.Height;
  if(book.PlacementVersion<1){book.MainLeft=dock.Left;book.MainTop=dock.Top;book.MiniLeft=area.Right-mini.Width-16;book.MiniTop=area.Bottom-mini.Height-16;book.PlacementVersion=1;}
  main.Left=book.MainLeft;main.Top=book.MainTop;mini.Left=book.MiniLeft;mini.Top=book.MiniTop;
  KeepOnScreen(main); KeepOnScreen(mini);
  main.Closing+=HideInstead; mini.Closing+=HideInstead;
  timer.Tick+=(_,_)=>Update(); timer.Start();
  SystemEvents.UserPreferenceChanged+=ThemeChanged;
  Update(); SetMini(book.Mini);
  if(book.Months.Count==0) OpenSettings();
 }
 void ThemeChanged(object sender,UserPreferenceChangedEventArgs e)=>app.Dispatcher.BeginInvoke(new Action(ApplyTheme));
 void HideInstead(object? sender,CancelEventArgs e) {if(exiting)return;e.Cancel=true;((Window)sender!).Hide();}
 IntPtr HitTest(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x84 && book.ClickThrough){handled=true;return new IntPtr(-1);}return IntPtr.Zero;}
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
 void ApplyClickThrough(){if(miniHwnd==IntPtr.Zero)return;long style=GetWindowLongPtr(miniHwnd,-20).ToInt64();style=book.ClickThrough?style|0x20L:style&~0x20L;SetWindowLongPtr(miniHwnd,-20,new IntPtr(style));SetWindowPos(miniHwnd,IntPtr.Zero,0,0,0,0,0x0037);}
 void CreateTray(){
  tray=new Forms.NotifyIcon{Text="薪跳 · 收入进度",Visible=true};using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("App.ico"))tray.Icon=new System.Drawing.Icon(stream!);
  var menu=new Forms.ContextMenuStrip();
  menu.Items.Add("完整看板",null,(_,_)=>SetMini(false));menu.Items.Add("透明迷你窗",null,(_,_)=>SetMini(true));
  menu.Items.Add("锁定／解锁迷你窗鼠标穿透",null,(_,_)=>{book.ClickThrough=!book.ClickThrough;ApplyClickThrough();Persist();Update();});
  menu.Items.Add("切换今日／本月",null,(_,_)=>ToggleAmount());
  menu.Items.Add("置顶／取消置顶",null,(_,_)=>{book.Topmost=!book.Topmost;main.Topmost=mini.Topmost=book.Topmost;Persist();});
  menu.Items.Add("设置",null,(_,_)=>OpenSettings()); menu.Items.Add("退出",null,(_,_)=>Exit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>SetMini(false);
 }
 void KeepOnScreen(Window window){var area=SystemParameters.WorkArea;if(window.Left+window.Width<area.Left+30 || window.Left>area.Right-30 || window.Top+window.Height<area.Top+30 || window.Top>area.Bottom-30){window.Left=area.Right-window.Width-16;window.Top=area.Bottom-window.Height-16;}}
 void SetMini(bool enabled){book.Mini=enabled;if(enabled){main.Hide();mini.Show();ApplyClickThrough();}else{mini.Hide();main.Show();main.Activate();}Persist();}
 void ToggleAmount(){book.PrimaryMonth=!book.PrimaryMonth;Update();Persist();}
 void Persist(){try{book.MainLeft=main.Left;book.MainTop=main.Top;book.MiniLeft=mini.Left;book.MiniTop=mini.Top;store.Save(book);}catch(Exception ex){error="设置保存失败："+ex.Message;}}
 void ApplyStartup(){try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)??Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);if(key==null)throw new IOException("无法访问启动项");if(book.AutoStart){string exe=Environment.ProcessPath??throw new IOException("无法确定程序路径");string command="\""+exe+"\"";if(Path.GetFileNameWithoutExtension(exe).Equals("dotnet",StringComparison.OrdinalIgnoreCase))command+=" \""+Path.Combine(AppContext.BaseDirectory,"SalaryBeat.dll")+"\"";key.SetValue("SalaryBeat",command);}else key.DeleteValue("SalaryBeat",false);
  // Only retire this application's own legacy startup entry after inspecting it.
  var old=key.GetValue("SalaryGlowLeo") as string;if(old!=null && old.Contains("SalaryGlow.Leo",StringComparison.OrdinalIgnoreCase))key.DeleteValue("SalaryGlowLeo",false);}catch(Exception ex){error="开机自启设置失败："+ex.Message;}}
 void OpenSettings(){
  SetMini(false);
  if(navigation.IsAnimating || navigation.IsSettings)return;
  settings=new SettingsView(book,calendar,dataFolder,(changed)=>{store.Save(changed);book=changed;error="";ApplyTheme();ApplyStartup();Update();},()=>FlipSettings(false));
  FlipSettings(true);
 }
 void Exit(){exiting=true;timer.Stop();Persist();SystemEvents.UserPreferenceChanged-=ThemeChanged;tray.Visible=false;tray.Dispose();miniSource?.RemoveHook(HitTest);mini.Close();main.Close();app.Shutdown();}
}
