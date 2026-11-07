using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using SalaryBeat.Core;
namespace SalaryBeat.Desktop;
public sealed partial class Shell {
 TextBlock progressText=null!,rateCaption=null!;Border rateCard=null!,otherCard=null!;StackPanel noticeArea=null!;TextBlock calendarFallback=null!;
 readonly CardNavigation navigation=new();readonly ContentControl cardHost=new();readonly ScaleTransform flipScale=new(1,1);
 void BuildWindows(){
  main=new Window{Title="薪跳 SalaryBeat",Width=640,Height=400,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=book.Topmost,FontFamily=new FontFamily("Microsoft YaHei UI"),ShowInTaskbar=true};
  UiTheme.Install(main,book.Theme);
  panel=new Border{CornerRadius=new CornerRadius(23),Padding=new Thickness(18),Margin=new Thickness(6),BorderThickness=new Thickness(1),Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=12,ShadowDepth=2,Opacity=.14}};panel.SetResourceReference(Border.BackgroundProperty,"Canvas");panel.SetResourceReference(Border.BorderBrushProperty,"Line");var front=new Grid{Width=640,Height=400};front.Children.Add(panel);frontFace=front;
  cardHost.Content=frontFace;cardHost.RenderTransform=flipScale;cardHost.RenderTransformOrigin=new Point(.5,.5);
  main.Content=new Viewbox{Child=cardHost,Stretch=Stretch.Uniform};
  var root=new Grid();foreach(var h in new[]{new GridLength(42),new GridLength(1,GridUnitType.Star),GridLength.Auto,new GridLength(32)})root.RowDefinitions.Add(new(){Height=h});panel.Child=root;
  var header=new Grid();header.ColumnDefinitions.Add(new(){Width=new GridLength(36)});header.ColumnDefinitions.Add(new(){Width=new GridLength(130)});header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});root.Children.Add(header);
  var mark=new Border{Width=30,Height=30,CornerRadius=new CornerRadius(10),Background=UiTheme.Brush("#076956"),VerticalAlignment=VerticalAlignment.Top,Child=new TextBlock{Text="∿",Foreground=Brushes.White,FontSize=26,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};header.Children.Add(mark);
  var brand=new StackPanel();var brandName=UiTheme.Text("薪跳",15);brandName.FontWeight=FontWeights.Bold;brand.Children.Add(brandName);brand.Children.Add(UiTheme.Text("S A L A R Y B E A T",8,"Muted"));Grid.SetColumn(brand,1);header.Children.Add(brand);
  date=UiTheme.Text("",11,"Muted");date.VerticalAlignment=VerticalAlignment.Center;date.Margin=new Thickness(0,0,12,10);date.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(date,2);header.Children.Add(date);
  var controls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};
  foreach(var b in new[]{UiTheme.Action("↘",()=>SetMini(true),"IconButton"),UiTheme.Action("—",()=>{main.Hide();mini.Hide();},"IconButton"),UiTheme.Action("×",()=>{main.Hide();mini.Hide();},"IconButton")}){b.Margin=new Thickness(2,0,0,0);controls.Children.Add(b);}Grid.SetColumn(controls,3);header.Children.Add(controls);
  header.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is TextBlock || e.OriginalSource==header)try{main.DragMove();}catch(InvalidOperationException){}};
  var body=new Grid{Margin=new Thickness(0,2,0,6)};body.ColumnDefinitions.Add(new(){Width=new GridLength(1.7,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new GridLength(12)});body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(body,1);root.Children.Add(body);
  var hero=new Grid{ClipToBounds=true};var green=new Border{CornerRadius=new CornerRadius(20),Background=UiTheme.Brush("#076956"),Child=hero};body.Children.Add(green);
  hero.Children.Add(new Ellipse{Width=150,Height=150,Fill=UiTheme.Brush("#4BC79B"),Opacity=.18,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,-60,-45,0),IsHitTestVisible=false});
  var heroContent=new Grid{Margin=new Thickness(20)};heroContent.RowDefinitions.Add(new(){Height=new GridLength(26)});heroContent.RowDefinitions.Add(new());heroContent.RowDefinitions.Add(new(){Height=new GridLength(48)});hero.Children.Add(heroContent);
  title=UiTheme.Text("今日已赚  ⇄",14);title.Foreground=Brushes.White;title.FontWeight=FontWeights.SemiBold;heroContent.Children.Add(title);
  var amountRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};amountRow.Children.Add(new TextBlock{Text="¥",FontSize=23,FontWeight=FontWeights.Bold,Foreground=UiTheme.Brush("#A6EBD2"),Margin=new Thickness(0,15,4,0)});big.SetValue(TextElement.ForegroundProperty,Brushes.White);big.DigitWeight=FontWeights.Bold;amountRow.Children.Add(big);
  var moneyView=new Viewbox{Child=amountRow,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left};Grid.SetRow(moneyView,1);heroContent.Children.Add(moneyView);
  var heroFoot=new StackPanel();status=UiTheme.Text("",10);status.Foreground=UiTheme.Brush("#C5F0E0");heroFoot.Children.Add(status);
  progress=new ProgressBar{Height=4,Maximum=1,Foreground=UiTheme.Brush("#80E2B8"),Background=UiTheme.Brush("#348A76"),Margin=new Thickness(0,10,0,7)};heroFoot.Children.Add(progress);
  progressText=UiTheme.Text("本月工作进度",9);progressText.Foreground=UiTheme.Brush("#B8DDCE");heroFoot.Children.Add(progressText);Grid.SetRow(heroFoot,2);heroContent.Children.Add(heroFoot);
  // A real button provides keyboard activation as well as click-to-swap.
  var heroClick=UiTheme.Action("",ToggleAmount);heroClick.Background=Brushes.Transparent;heroClick.Padding=new Thickness(0);heroClick.Content=green;heroClick.HorizontalContentAlignment=HorizontalAlignment.Stretch;heroClick.VerticalContentAlignment=VerticalAlignment.Stretch;System.Windows.Automation.AutomationProperties.SetName(heroClick,"切换今日与本月收入");body.Children.Remove(green);body.Children.Add(heroClick);
  var right=new Grid();right.RowDefinitions.Add(new());right.RowDefinitions.Add(new(){Height=new GridLength(10)});right.RowDefinitions.Add(new());Grid.SetColumn(right,2);body.Children.Add(right);
  var secondaryContent=new StackPanel{VerticalAlignment=VerticalAlignment.Center};otherLabel=UiTheme.Text("本月累计  ↗",11,"Muted");secondaryContent.Children.Add(otherLabel);otherAmount=UiTheme.Text("¥ 0.00",25);otherAmount.FontWeight=FontWeights.Bold;otherAmount.Margin=new Thickness(0,9,0,0);secondaryContent.Children.Add(otherAmount);
  otherCard=UiTheme.Card(secondaryContent,16);secondary=UiTheme.Action("",ToggleAmount);secondary.Padding=new Thickness(0);secondary.Background=Brushes.Transparent;secondary.HorizontalContentAlignment=HorizontalAlignment.Stretch;secondary.VerticalContentAlignment=VerticalAlignment.Stretch;secondary.Content=otherCard;right.Children.Add(secondary);
  var rateContent=new StackPanel{VerticalAlignment=VerticalAlignment.Center};rateCaption=UiTheme.Text("本月秒薪  /  每一秒都算数",10);rateCaption.Foreground=UiTheme.Brush("#39316D");rateContent.Children.Add(rateCaption);rate=UiTheme.Text("¥ 0.000000",22);rate.Foreground=UiTheme.Brush("#272155");rate.FontWeight=FontWeights.Bold;rate.Margin=new Thickness(0,10,0,0);rateContent.Children.Add(rate);rateCard=new Border{Background=UiTheme.Brush("#DAD7FF"),CornerRadius=new CornerRadius(18),Padding=new Thickness(16),Child=rateContent};Grid.SetRow(rateCard,2);right.Children.Add(rateCard);
  noticeArea=new StackPanel{Margin=new Thickness(0,0,0,4),MaxHeight=46};notice=UiTheme.Text("",10);notice.TextWrapping=TextWrapping.Wrap;noticeArea.Children.Add(notice);calendarFallback=UiTheme.CalendarLink(DateTime.Now.Year);noticeArea.Children.Add(calendarFallback);Grid.SetRow(noticeArea,2);root.Children.Add(noticeArea);
  var bottom=new DockPanel{LastChildFill=true};Grid.SetRow(bottom,3);root.Children.Add(bottom);var settingsButton=UiTheme.Action("⚙  设置",OpenSettings,"PrimaryButton");settingsButton.Padding=new Thickness(14,6,14,6);DockPanel.SetDock(settingsButton,Dock.Right);bottom.Children.Add(settingsButton);foot=UiTheme.Text("本地计算 · 已置顶",10,"Muted");foot.VerticalAlignment=VerticalAlignment.Center;bottom.Children.Add(foot);
  BuildMini();
 }
 void BuildMini(){
  mini=new Window{Title="薪跳 · 透明悬浮窗",Width=420,Height=156,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=book.Topmost,ShowInTaskbar=false,FontFamily=new FontFamily("SimHei, Microsoft YaHei UI"),FontWeight=FontWeights.Bold,Foreground=Brushes.White};
  // Transparent surface stays clear; an independent contour surrounds the white glyphs.
  var host=new Grid{Margin=new Thickness(14),Background=UiTheme.Brush("#01000000")};mini.Content=host;
  var stack=new StackPanel();miniTitle=UiTheme.Text("今日已赚",12);miniTitle.Foreground=Brushes.White;miniTitle.FontWeight=FontWeights.Bold;stack.Children.Add(miniTitle);
  small.DigitWeight=FontWeights.Bold;small.DigitFamily=new FontFamily("SimHei, Microsoft YaHei UI");small.SetValue(TextElement.ForegroundProperty,Brushes.White);
  var money=new StackPanel{Orientation=Orientation.Horizontal};money.Children.Add(new TextBlock{Text="¥",Foreground=Brushes.White,FontSize=22,FontWeight=FontWeights.Bold,Margin=new Thickness(0,10,6,0)});money.Children.Add(small);stack.Children.Add(new Viewbox{Child=money,Height=58,HorizontalAlignment=HorizontalAlignment.Left,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly});
  miniDetails=UiTheme.Text("",11);miniDetails.Foreground=Brushes.White;miniDetails.FontWeight=FontWeights.Bold;stack.Children.Add(miniDetails);
  // Two-pass outline: dark shadow separates text from white wallpapers; mint halo carries the brand.
  var contour=new Border{Child=stack,Effect=new DropShadowEffect{Color=Color.FromRgb(5,30,23),BlurRadius=3,ShadowDepth=0,Opacity=1}};
  host.Effect=new DropShadowEffect{Color=Color.FromRgb(29,164,121),BlurRadius=7,ShadowDepth=0,Opacity=1};host.Children.Add(contour);
  host.ToolTip="托盘菜单解锁后可拖动；双击切换今日／本月";host.MouseLeftButtonDown+=(_,e)=>{if(book.ClickThrough)return;if(e.ClickCount==2)ToggleAmount();else if(e.ButtonState==MouseButtonState.Pressed)try{mini.DragMove();}catch(InvalidOperationException){}};
  mini.SourceInitialized+=(_,_)=>{miniHwnd=new WindowInteropHelper(mini).Handle;miniSource=HwndSource.FromHwnd(miniHwnd);miniSource?.AddHook(HitTest);ApplyClickThrough();};
 }
 void Update(){
  var now=DateTime.Now;var plan=book.GetMonth(now.ToString("yyyy-MM"));var value=new PayrollEngine(calendar).At(plan,now);
  title.Text=book.PrimaryMonth?"本月累计  ⇄":"今日已赚  ⇄";big.SetAmount(book.PrimaryMonth?value.Month:value.Today);small.SetAmount(book.PrimaryMonth?value.Month:value.Today);
  date.Text=now.ToString("MM.dd  dddd",CultureInfo.GetCultureInfo("zh-CN"));
  otherLabel.Text=book.PrimaryMonth?"今日已赚  ↗":"本月累计  ↗";otherAmount.Text="¥ "+MoneyDisplay.Format(book.PrimaryMonth?value.Today:value.Month);
  rate.Text=$"¥ {value.PerSecond:F6}";status.Text=$"● {value.Status}   ·   本月 {value.Workdays} 个工作日";
  progress.Value=value.TotalSeconds==0?0:(double)value.ElapsedSeconds/value.TotalSeconds;progressText.Text=$"本月工作进度  {progress.Value:P0}";
  var warnings=new List<string>();if(!plan.Confirmed)warnings.Add("请填写或确认本月收入");if(value.TotalSeconds==0)warnings.Add("本月无计薪时段");if(error.Length>0)warnings.Add(error);
  bool missing=!calendar.Years.Contains(now.Year);notice.Text=string.Join(" · ",warnings);notice.Foreground=UiTheme.Brush(dark?"#FFBD93":"#9A420A");notice.Visibility=warnings.Count>0?Visibility.Visible:Visibility.Collapsed;
  calendarFallback.Visibility=missing?Visibility.Visible:Visibility.Collapsed;
  if(missing && !Equals(calendarFallback.Tag,now.Year)){var fresh=UiTheme.CalendarLink(now.Year,$"缺少 {now.Year} 年日历 · 点击打开对应年度页面 ↗");fresh.Tag=now.Year;int index=noticeArea.Children.IndexOf(calendarFallback);noticeArea.Children.RemoveAt(index);noticeArea.Children.Insert(index,fresh);calendarFallback=fresh;}
  noticeArea.Visibility=warnings.Count>0||missing?Visibility.Visible:Visibility.Collapsed;
  foot.Text=$"离线日历 {string.Join(" / ",calendar.Years.Where(y=>y>=2026))}  ·  {(book.Topmost?"已置顶":"未置顶")}";
  miniTitle.Text=$"{(book.PrimaryMonth?"本月累计":"今日已赚")}  ·  {value.Status}"+(book.ClickThrough?"":" · 已解锁")+(warnings.Count>0||missing?"  ⚠":"");miniTitle.ToolTip=notice.Text;
  miniDetails.Text=$"{(book.PrimaryMonth?"今日":"本月")} ¥ {MoneyDisplay.Format(book.PrimaryMonth?value.Today:value.Month)}   ·   秒薪 ¥ {value.PerSecond:F6}";
 }
 FrameworkElement frontFace=null!;
 void FlipSettings(bool show){
  if(!navigation.BeginFlip(show))return;
  void Swap(){navigation.CommitFlip();cardHost.Content=show?new Grid{Width=640,Height=400,Children={settings!}}:frontFace;}
  void Finish(){navigation.FinishFlip();cardHost.IsHitTestVisible=true;if(!show)settings=null;}
  if(!SystemParameters.ClientAreaAnimation){Swap();Finish();return;}
  cardHost.IsHitTestVisible=false;
  var shrink=new System.Windows.Media.Animation.DoubleAnimation(1,0,TimeSpan.FromMilliseconds(130)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseIn}};
  shrink.Completed+=(_,_)=>{Swap();var expand=new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(170)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}};expand.Completed+=(_,_)=>Finish();flipScale.BeginAnimation(ScaleTransform.ScaleXProperty,expand);};
  flipScale.BeginAnimation(ScaleTransform.ScaleXProperty,shrink);
 }
 void ApplyTheme(){dark=UiTheme.Apply(main,book.Theme);}
}
