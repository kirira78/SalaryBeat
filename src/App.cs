using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SalaryWidget {
 public class Preferences {
  public decimal Salary = 12000m;
  public bool DoubleRest = true;
  public bool AlternatingWeeks = false;
  public string AlternateDoubleWeek = WorkCalendar.Monday(DateTime.Today).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  public int RestDay = 0;
  public int Start = 540, End = 1080, BreakStart = 720, BreakEnd = 780;
  public bool ExcludeBreak = true, Configured = false, Pinned = false, Compact = false;
  public double Left = -10000, Top = -10000;
  public bool AttendanceMode = false, China2026 = true, MaskAmounts = false, Discreet = false;
  public decimal DailyRate = 500m;
  public string Currency = "CNY";
  public bool ConvertToCny = true;
  public double WindowOpacity = 1;
  public List<DayOverride> Overrides = new List<DayOverride>();
  public Preferences Copy() { var p=(Preferences)MemberwiseClone(); p.Overrides=new List<DayOverride>(); if(Overrides!=null) foreach(var d in Overrides) p.Overrides.Add(new DayOverride { Date=d.Date, Kind=d.Kind }); return p; }
  public string Validate() {
   if(Salary <= 0 || Salary > 10000000m) return "月薪请输入大于 0、不超过 1000 万的金额。";
   if(!ExchangeRates.Supported(Currency)) return "请选择支持的工资币种。";
   if(DailyRate <= 0 || DailyRate > 1000000m) return "日薪请输入大于 0、不超过 100 万的金额。";
   if(double.IsNaN(WindowOpacity) || WindowOpacity<0.35 || WindowOpacity>1) return "透明度须在 35% 至 100% 之间。";
   var dates=new HashSet<string>();
   if(Overrides!=null) foreach(var item in Overrides) { DateTime d; if(item==null || !DateTime.TryParseExact(item.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d) || !Enum.IsDefined(typeof(DayKind),item.Kind) || !dates.Add(item.Date)) return "日历设置包含无效或重复日期。"; }
   DateTime anchor;
   if(AlternatingWeeks && !DateTime.TryParseExact(AlternateDoubleWeek,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out anchor)) return "请选择一个双休周内的日期。";
   if(RestDay < 0 || RestDay > 6) return "请选择有效的休息日。";
   if(Start < 0 || End >= 1440 || Start >= End) return "下班时间须晚于上班时间，暂不支持跨日班次。";
   if(ExcludeBreak && (BreakStart < Start || BreakEnd > End || BreakStart >= BreakEnd || BreakEnd-BreakStart >= End-Start)) return "午休须在班次内，且不能覆盖整个班次。";
   return null;
  }
 }
 public class Totals {
  public int Days, CompletedDays, WorkDays, LeaveDays;
  public double PaidSeconds, ElapsedSeconds, Progress;
  public decimal Today, Month, Daily, Hourly;
  public bool IsWorkday, IsPaidLeave;
 }
 public static class Calculator {
  public static bool Workday(Preferences p, DateTime date) { return WorkCalendar.Kind(p,date)==DayKind.Work; }
  public static Totals At(Preferences p, DateTime now) {
   var t = new Totals();
   for(int d=1; d<=DateTime.DaysInMonth(now.Year,now.Month); d++) {
    var date=new DateTime(now.Year,now.Month,d); var kind=WorkCalendar.Kind(p,date);
    if(kind==DayKind.Work) t.WorkDays++; if(kind==DayKind.PaidLeave) t.LeaveDays++;
    if(WorkCalendar.PaidDay(p,date)) { t.Days++; if(d < now.Day) t.CompletedDays++; }
   }
   t.IsWorkday = Workday(p,now); t.IsPaidLeave = WorkCalendar.Kind(p,now)==DayKind.PaidLeave;
   t.PaidSeconds = (p.End-p.Start-(p.ExcludeBreak?p.BreakEnd-p.BreakStart:0))*60d;
   double elapsed = Math.Max(0,Math.Min((p.End-p.Start)*60d,now.TimeOfDay.TotalSeconds-p.Start*60d));
   if(p.ExcludeBreak) elapsed -= Math.Max(0,Math.Min((p.BreakEnd-p.BreakStart)*60d,now.TimeOfDay.TotalSeconds-p.BreakStart*60d));
   t.ElapsedSeconds = (t.IsWorkday || t.IsPaidLeave) ? Math.Max(0,elapsed) : 0;
   t.Progress = t.ElapsedSeconds/t.PaidSeconds;
   t.Daily = p.AttendanceMode ? p.DailyRate : (t.Days==0 ? 0 : p.Salary/t.Days);
   t.Hourly = t.Daily/(decimal)(t.PaidSeconds/3600d);
   t.Today = t.Daily*(decimal)t.Progress;
   t.Month = t.Daily*t.CompletedDays+t.Today; if(!p.AttendanceMode) t.Month=Math.Min(p.Salary,t.Month);
   return t;
  }
 }
 public static class DisplayControls {
  public static double OpacityAt(double x,double width) { return Math.Round(35+65*Math.Max(0,Math.Min(1,(x-19)/Math.Max(1,width-38)))); }
 }
 public static class Program {
  static readonly bool UiTest=Array.IndexOf(Environment.GetCommandLineArgs(),"--ui-test")>=0;
  static readonly string Folder = UiTest ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-profile") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SalaryGlow");
  static readonly string FileName = Path.Combine(Folder,"settings.xml");
  static Preferences prefs;
  static Window main, tiny;
  static Forms.NotifyIcon tray;
  static DispatcherTimer timer;
  static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");
  static bool loadFailed, firstRender=true;
  static string saveError;
  static ExchangeRates fx;
  static DispatcherTimer fxTimer;
  static Window activeSettings;
  static HwndSource hotkeySource;
  static bool hotkeyRegistered;
  const int LowKeyId=0x5347;
  [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd,int id);
  static T Find<T>(Window w,string name) where T:FrameworkElement { return (T)w.FindName(name); }
  static void Text(string name,string value) { Find<TextBlock>(main,name).Text=value; }
  static string Money(decimal value) { return value.ToString("N2",Zh); }
  static string Clock(int minutes) { return string.Format("{0:00}:{1:00}",minutes/60,minutes%60); }
  static Window Load(string name) { using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) return (Window)XamlReader.Load(stream); }
  [STAThread] public static int Main(string[] args) {
   if(args.Length > 0 && args[0]=="--test") return Tests.Run();
   if(args.Length > 0 && args[0]=="--render-test") {
    prefs=new Preferences { Configured=true, Currency="HKD" }; fx=new ExchangeRates(Path.Combine(Path.GetTempPath(),"SalaryGlow-Render"));
    main=Load("Main.xaml"); tiny=Load("Discreet.xaml"); Update();
    var surface=(FrameworkElement)main.Content; surface.Measure(new Size(390,640)); surface.Arrange(new Rect(0,0,390,640)); surface.UpdateLayout();
    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(390,640,96,96,PixelFormats.Pbgra32); bitmap.Render(surface);
    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    using(var output=File.Create(args[1])) encoder.Save(output);
    var settings=Load("Settings.xaml");
    var slider=Find<Slider>(settings,"OpacitySlider"); slider.Value=65; Find<TextBlock>(settings,"OpacityValue").Text="65%";
    var scroller=(ScrollViewer)settings.Content; scroller.Measure(new Size(450,660)); scroller.Arrange(new Rect(0,0,450,660)); scroller.UpdateLayout(); scroller.ScrollToBottom(); scroller.UpdateLayout();
    var sb=new System.Windows.Media.Imaging.RenderTargetBitmap(450,660,96,96,PixelFormats.Pbgra32); sb.Render(scroller);
    var se=new System.Windows.Media.Imaging.PngBitmapEncoder(); se.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sb)); using(var output=File.Create(args[1]+".settings.png")) se.Save(output);
    slider.ApplyTemplate(); if(slider.Template.FindName("PART_Track",slider)==null) throw new Exception("Missing slider track");
    Console.WriteLine("XAML loaded; slider template, main and settings rendered"); return 0;
   }
   if(args.Length > 0 && args[0]=="--fx-live") { try { var quote=ExchangeRates.Fetch("HKD").GetAwaiter().GetResult(); Console.WriteLine("HKD/CNY "+quote.Rate+" | provider date "+quote.Date); return 0; } catch(Exception ex) { Console.WriteLine(ex.Message); return 1; } }
   try {
    var app = new Application();
    app.ShutdownMode=ShutdownMode.OnMainWindowClose;
    prefs=Read(); fx=new ExchangeRates(Path.Combine(Folder,"rates")); main=Load("Main.xaml"); app.MainWindow=main;
    tiny=Load("Discreet.xaml"); if(UiTest) tiny.ShowInTaskbar=true;
    Find<Grid>(tiny,"TinyDrag").MouseLeftButtonDown += delegate(object sender,MouseButtonEventArgs e) { if(e.LeftButton==MouseButtonState.Pressed) try { tiny.DragMove(); } catch(InvalidOperationException) {} };
    Find<Button>(tiny,"TinyOpen").Click += delegate { SetDiscreet(false); };
    tiny.PreviewKeyDown += Keys; main.PreviewKeyDown += Keys;
    Find<Button>(main,"Mask").Click += delegate { prefs.MaskAmounts=!prefs.MaskAmounts; Update(); SaveQuiet(); };
    main.Topmost=prefs.Pinned;
    if(!double.IsNaN(prefs.Left) && !double.IsInfinity(prefs.Left) && !double.IsNaN(prefs.Top) && !double.IsInfinity(prefs.Top) && prefs.Left >= SystemParameters.VirtualScreenLeft && prefs.Top >= SystemParameters.VirtualScreenTop && prefs.Left+390 <= SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth && prefs.Top+594 <= SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight) { main.WindowStartupLocation=WindowStartupLocation.Manual; main.Left=prefs.Left; main.Top=prefs.Top; }
    Find<Grid>(main,"DragArea").MouseLeftButtonDown += delegate(object sender,MouseButtonEventArgs e) { if(e.OriginalSource is TextBlock || e.OriginalSource is Grid || e.OriginalSource is Border) { try { main.DragMove(); } catch(InvalidOperationException) {} } };
    Find<Button>(main,"Pin").Click += delegate { prefs.Pinned=!prefs.Pinned; main.Topmost=prefs.Pinned; ApplyView(); SaveQuiet(); };
    Find<Button>(main,"Minimize").Click += delegate { main.Hide(); tiny.Hide(); };
    Find<Button>(main,"Close").Click += delegate { main.Close(); };
    Find<Button>(main,"Compact").Click += delegate { prefs.Compact=!prefs.Compact; ApplyView(); SaveQuiet(); };
    Find<Button>(main,"Settings").Click += delegate { Settings(); };
    Find<Button>(main,"DiscreetToggle").Click += delegate { ToggleDiscreet(); };
    main.SourceInitialized += delegate {
     hotkeySource=HwndSource.FromHwnd(new WindowInteropHelper(main).Handle); hotkeySource.AddHook(HotkeyHook);
     hotkeyRegistered=RegisterHotKey(hotkeySource.Handle,LowKeyId,0x4003,0x44);
     if(!hotkeyRegistered) Find<Button>(main,"DiscreetToggle").ToolTip="全局 Ctrl+Alt+D 已被占用；可点击此按钮或在窗口内按 Ctrl+D";
    };
    Find<Button>(main,"FxRefresh").Click += async delegate { await RefreshFx(true); };
    CreateTray(); ApplyView(); Update(); if(prefs.Discreet) { main.Opacity=0; main.ShowInTaskbar=false; }
    timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) }; timer.Tick += delegate { Update(); }; timer.Start();
    fxTimer=new DispatcherTimer { Interval=TimeSpan.FromMinutes(1) }; fxTimer.Tick += async delegate { await RefreshFx(false); }; fxTimer.Start();
    main.Closing += delegate { if(hotkeySource!=null) { if(hotkeyRegistered) UnregisterHotKey(hotkeySource.Handle,LowKeyId); hotkeySource.RemoveHook(HotkeyHook); } prefs.Left=main.Left; prefs.Top=main.Top; SaveQuiet(); timer.Stop(); fxTimer.Stop(); tiny.Close(); tray.Visible=false; tray.Dispose(); };
    main.ContentRendered += async delegate { if(!firstRender) return; firstRender=false; if(prefs.Discreet) { main.Hide(); tiny.Show(); main.Opacity=prefs.WindowOpacity; main.ShowInTaskbar=true; } if(loadFailed) MessageBox.Show(main,"原设置文件无法读取，已使用示例数据。保存新设置前会保留一份旧文件备份。","薪光"); await RefreshFx(false); };
    return app.Run(main);
   } catch(Exception e) { MessageBox.Show("启动失败："+e.Message,"薪光"); return 1; }
  }
  static Preferences Read() {
   try { if(File.Exists(FileName)) using(var s=File.OpenRead(FileName)) { var p=(Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(s); if(p.Validate()!=null) throw new InvalidDataException(); return p; } }
   catch { loadFailed=true; }
   return new Preferences();
  }
  static void Save(Preferences p) {
   Directory.CreateDirectory(Folder);
   string tmp=FileName+".tmp";
   using(var s=File.Create(tmp)) new XmlSerializer(typeof(Preferences)).Serialize(s,p);
   if(File.Exists(FileName)) File.Replace(tmp,FileName,FileName+".bak"); else File.Move(tmp,FileName);
  }
  static void SaveQuiet() { try { Save(prefs); saveError=null; } catch { saveError="设置未能保存，请检查本机文件权限。"; } }
  static void ShowMain() { tiny.Hide(); main.Show(); main.WindowState=WindowState.Normal; main.Activate(); }
  static void CreateTray() {
   tray=new Forms.NotifyIcon();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("App.ico")) tray.Icon=new System.Drawing.Icon(stream);
   tray.Text="今日进度"; tray.Visible=true;
   tray.DoubleClick += delegate { ShowMain(); };
   var menu=new Forms.ContextMenuStrip();
   menu.Items.Add("打开面板",null,delegate { ShowMain(); });
   menu.Items.Add("切换低调模式  Ctrl+Alt+D",null,delegate { ToggleDiscreet(); });
   menu.Items.Add("隐藏 / 显示金额",null,delegate { prefs.MaskAmounts=!prefs.MaskAmounts; Update(); SaveQuiet(); });
   menu.Items.Add("薪资设置",null,delegate { ShowMain(); Settings(); });
   menu.Items.Add("退出",null,delegate { main.Close(); });
   tray.ContextMenuStrip=menu;
  }
  static IntPtr HotkeyHook(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled) {
   if(message==0x0312 && wp.ToInt32()==LowKeyId) { ToggleDiscreet(); handled=true; } return IntPtr.Zero;
  }
  static void ToggleDiscreet() {
   if(activeSettings!=null) { activeSettings.Activate(); return; }
   SetDiscreet(!tiny.IsVisible);
  }
  static void SetDiscreet(bool enabled) {
   prefs.Discreet=enabled; ApplyView();
   if(enabled) {
    if(main.IsVisible) { tiny.Left=main.Left; tiny.Top=main.Top; }
    main.Hide(); tiny.Show(); tiny.Activate();
   } else ShowMain();
   SaveQuiet();
  }
  static void Keys(object sender,KeyEventArgs e) {
   if(e.Key==Key.D && Keyboard.Modifiers==ModifierKeys.Control) { if(!e.IsRepeat) ToggleDiscreet(); e.Handled=true; return; }
   if(e.Key==Key.Escape) { main.Hide(); tiny.Hide(); e.Handled=true; }
   if(e.Key==Key.H && Keyboard.Modifiers==ModifierKeys.Control) { prefs.MaskAmounts=!prefs.MaskAmounts; Update(); SaveQuiet(); e.Handled=true; }
  }
  static void ApplyView() {
   main.Opacity=prefs.WindowOpacity; tiny.Opacity=prefs.WindowOpacity; tiny.Topmost=prefs.Pinned;
   tray.Icon = prefs.Discreet ? System.Drawing.SystemIcons.Application : tray.Icon;
   main.Height=prefs.Compact?372:640;
   Find<StackPanel>(main,"Details").Visibility=prefs.Compact?Visibility.Collapsed:Visibility.Visible;
   Find<Button>(main,"Compact").Content=prefs.Compact?"↙  展开详情":"↗  迷你模式";
   Find<Button>(main,"Pin").Content=prefs.Pinned?"◆":"◇";
   Find<Button>(main,"Pin").ToolTip=prefs.Pinned?"取消置顶":"窗口置顶";
  }
  static async System.Threading.Tasks.Task RefreshFx(bool force) {
   if(!prefs.ConvertToCny || prefs.Currency=="CNY") { Update(); return; }
   var pending=fx.Refresh(prefs.Currency,force); Update(); await pending; Update();
  }
  static void Update() {
   DateTime now=DateTime.Now; var t=Calculator.At(prefs,now); double minute=now.TimeOfDay.TotalMinutes;
   var quote=prefs.ConvertToCny?fx.Get(prefs.Currency):null;
   bool converted=prefs.Currency!="CNY" && prefs.ConvertToCny && quote!=null;
   decimal factor=converted?quote.Rate:1m;
   string code=converted?"CNY":prefs.Currency;
   Text("CurrencyMark",code=="CNY"?"¥":code);
   Text("EarnedTitle",converted?"今日已赚 · 约合人民币":"今日已赚 · "+code);
   Text("FxInfo",prefs.Currency=="CNY"?"人民币计薪 · 无需汇率换算":!prefs.ConvertToCny?"按 "+prefs.Currency+" 原币显示 · 可在设置中开启换算":fx.Status(prefs.Currency));
   Find<Button>(main,"FxRefresh").Visibility=prefs.Currency!="CNY" && prefs.ConvertToCny?Visibility.Visible:Visibility.Collapsed;
   Text("DateLabel",now.ToString("MM月dd日  dddd",Zh));
   Text("Earned",prefs.MaskAmounts?"••••••":Money(t.Today*factor));
   Text("Rate",prefs.MaskAmounts?"金额已隐藏 · Ctrl+H 切换显示":prefs.Configured?string.Format("每小时 {0} {1} · 每一分钟，都有回报",code,Money(t.Hourly*factor)):"示例数据 · 点击薪资设置开始");
   Text("ProgressLabel",(t.Progress*100).ToString("0.0")+"%");
   Find<Border>(main,"ProgressFill").Width=266*t.Progress;
   Text("ShiftLabel",Clock(prefs.Start)+" — "+Clock(prefs.End));
   Text("DayTarget",prefs.MaskAmounts?"今日目标 ••••":"目标 "+code+" "+Money(t.Daily*factor));
   Text("MonthEarned",prefs.MaskAmounts?"••••••":code+" "+Money(t.Month*factor));
   Text("Workdays",string.Format("出勤 {0} 天 · 带薪假 {1} 天",t.WorkDays,t.LeaveDays));
   string status; DateTime deadline;
   if(!t.IsWorkday) { status=t.IsPaidLeave?"●  带薪休假":"●  今日休息"; deadline=NextStart(now); Text("CountdownTitle","距离下次上班"); Text("CountdownNote",t.IsPaidLeave?"带薪假按设定时段累积":"好好休息，也是一种积累"); }
   else if(minute<prefs.Start) { status="●  尚未上班"; deadline=now.Date.AddMinutes(prefs.Start); Text("CountdownTitle","距离上班"); Text("CountdownNote","新的一天，即将开始"); }
   else if(minute>=prefs.End) { status="●  今日已收工"; deadline=NextStart(now); Text("CountdownTitle","距离下次上班"); Text("CountdownNote","今天辛苦了，享受生活吧"); }
   else { bool lunch=prefs.ExcludeBreak && minute>=prefs.BreakStart && minute<prefs.BreakEnd; status=lunch?"●  午休暂停":"●  正在积累"; deadline=now.Date.AddMinutes(prefs.End); Text("CountdownTitle","距离下班"); Text("CountdownNote",lunch?"午休时，金额暂停增长":"离自由，又近了一点"); }
   Text("Status",status);
   var remaining=deadline-now;
   Text("Countdown",remaining.TotalHours>=24?string.Format("{0}天 {1:00}时",(int)remaining.TotalDays,remaining.Hours):string.Format("{0:00}:{1:00}:{2:00}",(int)remaining.TotalHours,remaining.Minutes,remaining.Seconds));
   Text("Hint",saveError ?? (prefs.China2026 && now.Year!=2026?"本年节假日未内置，请在日历中设置。":prefs.Configured?(prefs.AttendanceMode?"日薪 × 计薪天数 · 按日历推算":"固定月薪 · 含带薪假，按计薪日均摊"):"当前为示例数据，设置你的第一份薪资。"));
   if(prefs.AlternatingWeeks && saveError==null && !(prefs.China2026 && now.Year!=2026)) Text("Hint","大小周 · 本周基础排班"+(WorkCalendar.DoubleWeek(prefs,now)?"双休":"单休")+" · 日历覆盖优先");
   Find<TextBlock>(tiny,"TinyClock").Text=now.ToString("HH:mm");
   Find<TextBlock>(tiny,"TinyProgress").Text=(t.IsPaidLeave?"休假 ":t.IsWorkday?"今日 ":"休息 ")+(t.Progress*100).ToString("0")+"%";
   Find<Border>(tiny,"TinyFill").Width=200*t.Progress;
   Find<Button>(main,"Mask").Content=prefs.MaskAmounts?"◉":"◎";
  }
  static DateTime NextStart(DateTime now) { var date=now.Date.AddDays(1); for(int i=0;i<3660 && !Calculator.Workday(prefs,date);i++) date=date.AddDays(1); return date.AddMinutes(prefs.Start); }
  static bool ParseTime(string input,out int value) { DateTime time; bool ok=DateTime.TryParseExact(input.Trim(),new[]{"H:mm","HH:mm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out time); value=time.Hour*60+time.Minute; return ok; }
  static void EditCalendar(Window owner,Preferences draft) {
   var w=new Window { Title="出勤与假期日历", Width=430, Height=570, Owner=owner, WindowStartupLocation=WindowStartupLocation.CenterOwner, Background=new SolidColorBrush(Color.FromRgb(245,245,239)), FontFamily=new FontFamily("Microsoft YaHei UI"), ResizeMode=ResizeMode.NoResize };
   var panel=new StackPanel { Margin=new Thickness(24) }; w.Content=panel;
   panel.Children.Add(new TextBlock { Text="选择日期，按公司安排调整", FontSize=19, Margin=new Thickness(0,0,0,12) });
   var calendar=new System.Windows.Controls.Calendar { SelectedDate=DateTime.Today }; panel.Children.Add(calendar);
   var info=new TextBlock { Margin=new Thickness(0,12,0,10), TextWrapping=TextWrapping.Wrap }; panel.Children.Add(info);
   var kinds=new ComboBox { Margin=new Thickness(0,0,0,12), Padding=new Thickness(8) };
   kinds.Items.Add("出勤 / 补班"); kinds.Items.Add("带薪休假"); kinds.Items.Add("休息 / 不计薪"); panel.Children.Add(kinds);
   Action refresh=delegate { var d=calendar.SelectedDate ?? DateTime.Today; kinds.SelectedIndex=(int)WorkCalendar.Kind(draft,d); info.Text=d.ToString("yyyy-MM-dd")+" · "+WorkCalendar.Label(WorkCalendar.Kind(draft,d))+(draft.Overrides.Exists(x=>x.Date==d.ToString("yyyy-MM-dd"))?"（自定义）":"（默认）"); };
   calendar.SelectedDatesChanged += delegate { refresh(); };
   var apply=new Button { Content="应用到所选日期", Padding=new Thickness(10), Margin=new Thickness(0,0,0,8) }; panel.Children.Add(apply);
   apply.Click += delegate { var d=calendar.SelectedDate??DateTime.Today; string key=d.ToString("yyyy-MM-dd"); draft.Overrides.RemoveAll(x=>x.Date==key); draft.Overrides.Add(new DayOverride { Date=key, Kind=(DayKind)kinds.SelectedIndex }); refresh(); };
   var reset=new Button { Content="恢复所选日期的默认安排", Padding=new Thickness(8) }; panel.Children.Add(reset);
   reset.Click += delegate { string key=(calendar.SelectedDate??DateTime.Today).ToString("yyyy-MM-dd"); draft.Overrides.RemoveAll(x=>x.Date==key); refresh(); };
   panel.Children.Add(new TextBlock { Text="法定假日、调休休息、补班分别处理。自定义优先。非 2026 年请手动设置。\n更改需回到设置页点击保存才会生效。", TextWrapping=TextWrapping.Wrap, Foreground=Brushes.Gray, Margin=new Thickness(0,14,0,0), FontSize=11 });
   refresh(); w.ShowDialog();
  }
  static void Settings() {
   if(activeSettings!=null) { activeSettings.Activate(); return; }
   var w=Load("Settings.xaml"); w.Owner=main; activeSettings=w;
   var draft=prefs.Copy();
   var currency=Find<ComboBox>(w,"Currency");
   for(int i=0;i<ExchangeRates.Codes.Length;i++) currency.Items.Add(ExchangeRates.Codes[i]+" · "+ExchangeRates.Names[i]);
   currency.SelectedIndex=Array.IndexOf(ExchangeRates.Codes,prefs.Currency);
   Find<CheckBox>(w,"ConvertToCny").IsChecked=prefs.ConvertToCny;
   var mode=Find<ComboBox>(w,"PayMode"); mode.SelectedIndex=prefs.AttendanceMode?1:0;
   Find<TextBox>(w,"DailyRate").Text=prefs.DailyRate.ToString("0.##",CultureInfo.InvariantCulture);
   Find<CheckBox>(w,"ChinaCalendar").IsChecked=prefs.China2026;
   Find<CheckBox>(w,"MaskAmounts").IsChecked=prefs.MaskAmounts;
   Find<CheckBox>(w,"DiscreetMode").IsChecked=prefs.Discreet;
   var opacitySlider=Find<Slider>(w,"OpacitySlider");
   opacitySlider.Value=prefs.WindowOpacity*100;
   Find<TextBlock>(w,"OpacityValue").Text=opacitySlider.Value.ToString("0")+"%";
   opacitySlider.ValueChanged += delegate { double value=opacitySlider.Value/100; main.Opacity=value; tiny.Opacity=value; Find<TextBlock>(w,"OpacityValue").Text=opacitySlider.Value.ToString("0")+"%"; };
   Action<MouseEventArgs> moveOpacity=delegate(MouseEventArgs e) { double x=e.GetPosition(opacitySlider).X; opacitySlider.Value=DisplayControls.OpacityAt(x,opacitySlider.ActualWidth); };
   opacitySlider.PreviewMouseLeftButtonDown += delegate(object sender,MouseButtonEventArgs e) { opacitySlider.Focus(); opacitySlider.CaptureMouse(); moveOpacity(e); e.Handled=true; };
   opacitySlider.PreviewMouseMove += delegate(object sender,MouseEventArgs e) { if(opacitySlider.IsMouseCaptured && e.LeftButton==MouseButtonState.Pressed) { moveOpacity(e); e.Handled=true; } };
   opacitySlider.PreviewMouseLeftButtonUp += delegate(object sender,MouseButtonEventArgs e) { if(opacitySlider.IsMouseCaptured) { moveOpacity(e); opacitySlider.ReleaseMouseCapture(); e.Handled=true; } };
   w.Closed += delegate { opacitySlider.ReleaseMouseCapture(); main.Opacity=prefs.WindowOpacity; tiny.Opacity=prefs.WindowOpacity; activeSettings=null; };
   Action updateMode=delegate { bool daily=mode.SelectedIndex==1; Find<TextBox>(w,"Salary").IsEnabled=!daily; Find<TextBox>(w,"DailyRate").IsEnabled=daily; Find<TextBlock>(w,"ModeHelp").Text=daily?"按日薪 ×（出勤日 + 带薪假）估算，月总额随天数变化。请在日历中核对实际出勤。":"月薪总额不随节假日数量变化，按出勤日与带薪假均摊。"; };
   mode.SelectionChanged += delegate { updateMode(); }; updateMode();
   Find<Button>(w,"CalendarButton").Click += delegate { draft.DoubleRest=Find<ComboBox>(w,"Schedule").SelectedIndex==0; draft.AlternatingWeeks=Find<ComboBox>(w,"Schedule").SelectedIndex==2;
    if(draft.AlternatingWeeks) { var selected=Find<DatePicker>(w,"AlternateAnchor").SelectedDate; if(!selected.HasValue) { Find<TextBlock>(w,"Error").Text="请先选择一个双休周内的日期。"; return; } draft.AlternateDoubleWeek=WorkCalendar.Monday(selected.Value).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
    draft.RestDay=Find<ComboBox>(w,"RestDay").SelectedIndex; draft.China2026=Find<CheckBox>(w,"ChinaCalendar").IsChecked==true; EditCalendar(w,draft); };
   Find<TextBox>(w,"Salary").Text=prefs.Salary.ToString("0.##",CultureInfo.InvariantCulture);
   var schedule=Find<ComboBox>(w,"Schedule"); var rest=Find<ComboBox>(w,"RestDay");
   var anchorPicker=Find<DatePicker>(w,"AlternateAnchor");
   DateTime savedAnchor; anchorPicker.SelectedDate=DateTime.TryParseExact(prefs.AlternateDoubleWeek,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out savedAnchor)?savedAnchor:DateTime.Today;
   Action updateSchedule=delegate {
    rest.IsEnabled=schedule.SelectedIndex==1;
    Find<StackPanel>(w,"AlternatePanel").Visibility=schedule.SelectedIndex==2?Visibility.Visible:Visibility.Collapsed;
    var date=anchorPicker.SelectedDate;
    Find<TextBlock>(w,"AlternateInfo").Text=date.HasValue?WorkCalendar.Monday(date.Value).ToString("yyyy-MM-dd")+" 所在周：周六、周日休；下一周仅周日休。按周一开始交替，跨月跨年不重置。":"请选择你确定是双休的那一周中的任意一天。";
   };
   schedule.SelectedIndex=prefs.AlternatingWeeks?2:prefs.DoubleRest?0:1; rest.SelectedIndex=prefs.RestDay;
   schedule.SelectionChanged += delegate { updateSchedule(); }; anchorPicker.SelectedDateChanged += delegate { updateSchedule(); }; updateSchedule();
   Find<TextBox>(w,"Start").Text=Clock(prefs.Start); Find<TextBox>(w,"End").Text=Clock(prefs.End);
   Find<TextBox>(w,"BreakStart").Text=Clock(prefs.BreakStart); Find<TextBox>(w,"BreakEnd").Text=Clock(prefs.BreakEnd);
   var exclude=Find<CheckBox>(w,"ExcludeBreak"); exclude.IsChecked=prefs.ExcludeBreak;
   Find<Grid>(w,"BreakFields").IsEnabled=prefs.ExcludeBreak;
   exclude.Checked += delegate { Find<Grid>(w,"BreakFields").IsEnabled=true; }; exclude.Unchecked += delegate { Find<Grid>(w,"BreakFields").IsEnabled=false; };
   Find<Button>(w,"Save").Click += async delegate {
    var p=draft.Copy(); p.Currency=ExchangeRates.Codes[currency.SelectedIndex]; p.ConvertToCny=Find<CheckBox>(w,"ConvertToCny").IsChecked==true; p.AttendanceMode=mode.SelectedIndex==1; p.China2026=Find<CheckBox>(w,"ChinaCalendar").IsChecked==true; p.MaskAmounts=Find<CheckBox>(w,"MaskAmounts").IsChecked==true; p.Discreet=Find<CheckBox>(w,"DiscreetMode").IsChecked==true; p.WindowOpacity=Find<Slider>(w,"OpacitySlider").Value/100; var err=Find<TextBlock>(w,"Error");
    if(!decimal.TryParse(Find<TextBox>(w,"Salary").Text.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out p.Salary)) { err.Text="请输入有效的月薪，例如 12000。"; return; }
    if(!decimal.TryParse(Find<TextBox>(w,"DailyRate").Text.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out p.DailyRate)) { err.Text="请输入有效日薪，例如 500。"; return; }
    p.DoubleRest=schedule.SelectedIndex==0; p.AlternatingWeeks=schedule.SelectedIndex==2;
    if(p.AlternatingWeeks) { if(!anchorPicker.SelectedDate.HasValue) { err.Text="请选择一个双休周内的日期。"; return; } p.AlternateDoubleWeek=WorkCalendar.Monday(anchorPicker.SelectedDate.Value).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
    p.RestDay=rest.SelectedIndex; p.ExcludeBreak=exclude.IsChecked==true;
    if(!ParseTime(Find<TextBox>(w,"Start").Text,out p.Start)||!ParseTime(Find<TextBox>(w,"End").Text,out p.End)) { err.Text="时间格式为 09:00、18:00，请检查上下班时间。"; return; }
    if(p.ExcludeBreak && (!ParseTime(Find<TextBox>(w,"BreakStart").Text,out p.BreakStart)||!ParseTime(Find<TextBox>(w,"BreakEnd").Text,out p.BreakEnd))) { err.Text="请检查午休时间，格式为 12:00。"; return; }
    string error=p.Validate(); if(error!=null) { err.Text=error; return; }
    p.Configured=true;
    try { Save(p); prefs=p; saveError=null; ApplyView(); Update(); w.Close(); if(prefs.Discreet) { main.Hide(); tiny.Show(); } await RefreshFx(false); } catch(Exception) { err.Text="保存失败，请检查本机文件权限后重试。"; }
   };
   w.ShowDialog();
  }
 }
 public static class Tests {
  static int count;
  static void Check(bool value,string name) { if(!value) throw new Exception(name); count++; Console.WriteLine("PASS "+name); }
  static bool Near(decimal a,decimal b) { return Math.Abs(a-b)<0.00001m; }
  public static int Run() {
   try {
    var p=new Preferences();
    Check(p.Validate()==null,"valid defaults");
    var t=Calculator.At(p,new DateTime(2026,11,3,9,0,0)); Check(t.Days==21,"November double-rest days"); Check(t.Today==0,"shift start zero");
    t=Calculator.At(p,new DateTime(2026,11,3,8,0,0)); Check(t.Today==0,"before shift zero");
    var noon=Calculator.At(p,new DateTime(2026,11,3,12,0,0)); var lunch=Calculator.At(p,new DateTime(2026,11,3,12,59,59)); Check(noon.Today==lunch.Today,"unpaid lunch frozen");
    t=Calculator.At(p,new DateTime(2026,11,3,14,0,0)); Check(Near(t.Today,t.Daily/2),"half paid shift");
    t=Calculator.At(p,new DateTime(2026,11,3,23,0,0)); Check(Near(t.Today,t.Daily),"after shift capped");
    t=Calculator.At(p,new DateTime(2026,11,7,14,0,0)); Check(t.Today==0,"rest day zero");
    t=Calculator.At(p,new DateTime(2026,11,30,23,0,0)); Check(Near(t.Month,p.Salary),"month total equals salary");
    t=Calculator.At(p,new DateTime(2026,12,1,8,0,0)); Check(t.Month==0,"new month reset");
    p.DoubleRest=false; t=Calculator.At(p,new DateTime(2026,11,7,14,0,0)); Check(t.Days==25 && t.Today>0,"single rest Saturday paid");
    p.RestDay=3; Check(!Calculator.Workday(p,new DateTime(2026,11,4)),"custom rest day");
    p=new Preferences { ExcludeBreak=false }; t=Calculator.At(p,new DateTime(2026,11,3,13,30,0)); Check(Near(t.Today,t.Daily/2),"paid lunch included");
    p.Start=1200; Check(p.Validate()!=null,"overnight rejected"); p=new Preferences { Salary=0 }; Check(p.Validate()!=null,"zero salary rejected");
    p=new Preferences { BreakStart=540, BreakEnd=1080 }; Check(p.Validate()!=null,"full shift lunch rejected");
    foreach(int year in new[]{2024,2026}) for(int month=1;month<=12;month++) foreach(bool dbl in new[]{false,true}) { p=new Preferences { DoubleRest=dbl }; t=Calculator.At(p,new DateTime(year,month,DateTime.DaysInMonth(year,month),23,59,59)); Check(Near(t.Month,p.Salary),"month conservation "+year+"/"+month+" "+dbl); }
    p=new Preferences();
    Check(WorkCalendar.Kind(p,new DateTime(2026,10,1))==DayKind.PaidLeave,"national paid holiday");
    Check(WorkCalendar.Kind(p,new DateTime(2026,10,6))==DayKind.Rest,"adjusted rest not extra paid");
    Check(WorkCalendar.Kind(p,new DateTime(2026,10,10))==DayKind.Work,"makeup Saturday");
    t=Calculator.At(p,new DateTime(2026,10,1,18,0,0)); Check(t.Today>0 && !t.IsWorkday && t.IsPaidLeave,"holiday accrual without work");
    t=Calculator.At(p,new DateTime(2026,10,31,23,0,0)); Check(Near(t.Month,p.Salary),"fixed monthly preserved with holidays");
    p.AttendanceMode=true; p.DailyRate=500; t=Calculator.At(p,new DateTime(2026,10,1,18,0,0)); Check(t.Today==500,"daily holiday pay");
    var oct=Calculator.At(p,new DateTime(2026,10,31,23,0,0)); var nov=Calculator.At(p,new DateTime(2026,11,30,23,0,0)); Check(oct.Month==oct.Days*500 && nov.Month==nov.Days*500,"daily month follows paid days");
    p.Overrides.Add(new DayOverride { Date="2026-10-10", Kind=DayKind.Rest }); Check(!Calculator.Workday(p,new DateTime(2026,10,10)),"custom override beats official calendar");
    var copy=p.Copy(); copy.Overrides.Clear(); Check(p.Overrides.Count==1,"cancel edits isolated");
    for(int d=1;d<=30;d++) p.Overrides.Add(new DayOverride { Date="2026-11-"+d.ToString("00"), Kind=DayKind.Rest });
    p.AttendanceMode=false; t=Calculator.At(p,new DateTime(2026,11,30,23,0,0)); Check(t.Month==0 && t.Daily==0,"zero paid days safe");
    p=new Preferences { China2026=false }; Check(WorkCalendar.Kind(p,new DateTime(2026,10,1))==DayKind.Work,"calendar disabled fallback");
    Check(WorkCalendar.Kind(new Preferences(),new DateTime(2027,10,1))==DayKind.Work,"unknown year weekly fallback");
    var serializer=new XmlSerializer(typeof(Preferences)); var buffer=new StringWriter(); p=new Preferences { MaskAmounts=true, Discreet=true, WindowOpacity=0.5, AttendanceMode=true }; p.Overrides.Add(new DayOverride { Date="2026-11-03", Kind=DayKind.PaidLeave }); serializer.Serialize(buffer,p); var restored=(Preferences)serializer.Deserialize(new StringReader(buffer.ToString())); Check(restored.Discreet && restored.MaskAmounts && restored.WindowOpacity==0.5 && restored.Overrides.Count==1 && restored.AttendanceMode,"new settings round trip");
    var legacy=(Preferences)serializer.Deserialize(new StringReader("<Preferences><Salary>8000</Salary></Preferences>")); Check(legacy.Salary==8000 && legacy.DailyRate==500 && legacy.China2026 && legacy.Validate()==null,"legacy settings migration");
    var fxJson="{\"base\":\"HKD\",\"quote\":\"CNY\",\"rate\":0.9,\"date\":\"2026-01-01\"}";
    var fq=ExchangeRates.Parse(fxJson,"HKD"); Check(fq.Rate*10000m==9000m,"FX multiply correct direction");
    bool rejected=false; try { ExchangeRates.Parse(fxJson,"USD"); } catch { rejected=true; } Check(rejected,"FX currency mismatch rejected");
    rejected=false; try { ExchangeRates.Parse(fxJson.Replace("0.9","0"),"HKD"); } catch { rejected=true; } Check(rejected,"FX zero rejected");
    rejected=false; try { ExchangeRates.Parse(fxJson.Replace("CNY","USD"),"HKD"); } catch { rejected=true; } Check(rejected,"FX quote mismatch rejected");
    Check(legacy.Currency=="CNY" && legacy.ConvertToCny,"legacy currency defaults");
    var cacheDir=Path.Combine(Path.GetTempPath(),"SalaryGlow-FxTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(cacheDir);
    var cachedQuote=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(fq); File.WriteAllText(Path.Combine(cacheDir,"HKD.json"),cachedQuote);
    var svc=new ExchangeRates(cacheDir); Check(svc.Get("HKD").Rate==0.9m,"FX cache read"); Check(svc.Get("USD")==null,"FX cache isolated by currency");
    File.WriteAllText(Path.Combine(cacheDir,"EUR.json"),"invalid"); Check(svc.Get("EUR")==null,"FX corrupt cache fallback");
    File.Delete(Path.Combine(cacheDir,"HKD.json")); File.Delete(Path.Combine(cacheDir,"EUR.json")); Directory.Delete(cacheDir);
    p=new Preferences { Currency="HKD" }; t=Calculator.At(p,new DateTime(2026,11,30,23,0,0)); Check(Near(t.Month,p.Salary),"FX keeps original salary accounting");
    Check(DisplayControls.OpacityAt(-100,374)==35,"slider drag clamps left");
    Check(DisplayControls.OpacityAt(900,374)==100,"slider drag clamps right");
    Check(DisplayControls.OpacityAt(187,374)==68,"slider middle maps continuously");
    Check(DisplayControls.OpacityAt(187,374)<DisplayControls.OpacityAt(215,374),"slider drag increases across track");
    Check(DisplayControls.OpacityAt(20,0)>=35,"slider narrow layout safe");
    p=new Preferences { AlternatingWeeks=true, AlternateDoubleWeek="2026-11-02", China2026=false };
    Check(p.Validate()==null,"alternating schedule valid");
    Check(!Calculator.Workday(p,new DateTime(2026,11,7)),"anchor Saturday off");
    Check(Calculator.Workday(p,new DateTime(2026,11,14)),"following Saturday works");
    Check(!Calculator.Workday(p,new DateTime(2026,11,21)),"third Saturday off");
    Check(!Calculator.Workday(p,new DateTime(2026,11,8)) && !Calculator.Workday(p,new DateTime(2026,11,15)),"both Sundays off");
    Check(Calculator.Workday(p,new DateTime(2026,10,31)),"prior week negative parity");
    Check(!Calculator.Workday(p,new DateTime(2026,10,24)),"two prior weeks negative parity");
    t=Calculator.At(p,new DateTime(2026,11,30,23,0,0)); Check(t.Days==23 && Near(t.Month,p.Salary),"alternating month count and fixed salary");
    p.AttendanceMode=true; t=Calculator.At(p,new DateTime(2026,11,30,23,0,0)); Check(t.Month==23*p.DailyRate,"alternating daily pay");
    p.AlternateDoubleWeek="2026-11-09"; t=Calculator.At(p,new DateTime(2026,11,7,18,0,0)); Check(t.Today==p.DailyRate,"phase switch changes Saturday pay");
    p.AlternateDoubleWeek="2026-11-08"; Check(!Calculator.Workday(p,new DateTime(2026,11,7)),"Sunday anchor normalizes Monday");
    p.AlternateDoubleWeek="2026-12-28"; Check(!Calculator.Workday(p,new DateTime(2027,1,2)) && Calculator.Workday(p,new DateTime(2027,1,9)),"year boundary preserves alternating phase");
    p.AlternateDoubleWeek="2026-10-05"; p.China2026=true; Check(Calculator.Workday(p,new DateTime(2026,10,10)),"official makeup overrides double weekend");
    p.Overrides.Add(new DayOverride {Date="2026-10-10",Kind=DayKind.Rest}); Check(!Calculator.Workday(p,new DateTime(2026,10,10)),"manual override beats alternating and official");
    p.Overrides.Add(new DayOverride {Date="2026-11-14",Kind=DayKind.PaidLeave}); Check(WorkCalendar.Kind(p,new DateTime(2026,11,14))==DayKind.PaidLeave,"paid leave override");
    buffer=new StringWriter(); serializer.Serialize(buffer,p); restored=(Preferences)serializer.Deserialize(new StringReader(buffer.ToString())); Check(restored.AlternatingWeeks && restored.AlternateDoubleWeek==p.AlternateDoubleWeek,"alternating settings round trip");
    Check(!legacy.AlternatingWeeks,"legacy weekly schedule retained");
    p.AlternateDoubleWeek="invalid"; Check(p.Validate()!=null,"invalid anchor rejected");
    Console.WriteLine("All "+count+" tests passed."); return 0;
   } catch(Exception e) { Console.WriteLine("FAIL "+e.Message); return 1; }
  }
 }
}
