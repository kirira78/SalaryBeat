using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SalaryBeat.Core;
namespace SalaryBeat.Desktop;
public static class UiRegression {
 // Actual WPF behavior checks. Only executable on Windows, with no real salary/profile/registry access.
 public static void Run(string folder){
  Directory.CreateDirectory(folder);var log=new List<string>();Window? window=null;
  void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);log.Add("PASS "+name);}
  void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
  byte[] Pixels(FrameworkElement element,string name){
   element.UpdateLayout();int width=Math.Max(1,(int)Math.Ceiling(element.ActualWidth)),height=Math.Max(1,(int)Math.Ceiling(element.ActualHeight));
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(folder,name+".png")))encoder.Save(stream);
   var pixels=new byte[width*height*4];bitmap.CopyPixels(pixels,width*4,0);return pixels;
  }
  IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject{
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var nested in Descendants<T>(child))yield return nested;}
  }
  try{
   if(!SystemParameters.ClientAreaAnimation)throw new InvalidOperationException("系统客户端动画已关闭，无法执行滚动动画回归；未更改系统设置。");
   window=new Window{Title="SalaryBeat UI regression · 示例数据",Width=640,Height=400,SizeToContent=SizeToContent.Manual,Background=Brushes.White,UseLayoutRounding=true};UiTheme.Install(window,"Light");
   var host=new StackPanel();window.Content=host;window.Show();
   int index=0;
   foreach(var (size,family) in new[]{(52d,"Bahnschrift, Segoe UI"),(40d,"SimHei, Microsoft YaHei UI")}){
    foreach(var (from,to) in new[]{(9.98m,9.99m),(9.99m,10m),(99.99m,100m),(999.99m,1000m),(10.01m,10.02m),(12345678.89m,12345678.90m)}){
     host.Children.Clear();var actual=new Odometer{DigitSize=size,DigitFamily=new FontFamily(family),DigitWeight=FontWeights.Bold};var expected=new Odometer{DigitSize=size,DigitFamily=new FontFamily(family),DigitWeight=FontWeights.Bold};host.Children.Add(actual);host.Children.Add(expected);
     actual.SetAmount(from);expected.SetAmount(to);Pump(30);actual.SetAmount(to);Pump(340);
     Check(Pixels(actual,$"{index}-animated").SequenceEqual(Pixels(expected,$"{index}-static")),"animated amount settles to complete static value "+MoneyDisplay.Format(to)+" / "+family);index++;
    }
    // Updates arriving before the previous transition finishes must still settle completely.
    host.Children.Clear();var rapid=new Odometer{DigitSize=size,DigitFamily=new FontFamily(family),DigitWeight=FontWeights.Bold};var final=new Odometer{DigitSize=size,DigitFamily=new FontFamily(family),DigitWeight=FontWeights.Bold};host.Children.Add(rapid);host.Children.Add(final);rapid.SetAmount(8.88m);Pump(20);
    foreach(var value in new[]{8.89m,8.90m,8.91m}){rapid.SetAmount(value);Pump(40);}final.SetAmount(8.91m);Pump(340);Check(Pixels(rapid,$"{index}-rapid").SequenceEqual(Pixels(final,$"{index}-static")),"interrupted updates settle completely / "+family);index++;
   }
   var book=new SettingsBook();book.SaveMonth(MonthPlan.Default(DateTime.Now.ToString("yyyy-MM")) with {Income=12345678.90m,Confirmed=true});
   var settings=new SettingsView(book,HolidayCalendar.LoadBundled(),Path.Combine(Path.GetTempPath(),"SalaryBeat-UI-NeverSaved"),_=>{},()=>{});
   window.Content=new Grid{Width=640,Height=400,Children={settings}};window.SizeToContent=SizeToContent.WidthAndHeight;Pump(50);
   foreach(string name in new[]{"IncomeMonth","MonthlyIncome"}){
    var field=Descendants<TextBox>(settings).Single(x=>x.Name==name);field.ApplyTemplate();field.UpdateLayout();
    var viewport=(ScrollViewer)field.Template.FindName("PART_ContentHost",field);
    var clip=viewport.TransformToAncestor(field).TransformBounds(new Rect(0,0,viewport.ActualWidth,viewport.ActualHeight));
    foreach(int i in new[]{0,field.Text.Length-1}){var rect=field.GetRectFromCharacterIndex(i,true);Check(!rect.IsEmpty&&rect.Top>=clip.Top-1&&rect.Bottom<=clip.Bottom+1&&rect.Left>=clip.Left-1&&rect.Right<=clip.Right+1,name+" character "+i+" fits without scrolling");}
    Check(Math.Abs(viewport.VerticalOffset)<.1,name+" does not scroll vertically");
   }
   Pixels(settings,"settings-inputs");log.Add("RESULT PASS · Windows WPF UI regressions");
  }catch(Exception ex){log.Add("RESULT FAIL · "+ex.Message);throw;}
  finally{File.WriteAllLines(Path.Combine(folder,"ui-regression-results.txt"),log);window?.Close();}
 }
}
