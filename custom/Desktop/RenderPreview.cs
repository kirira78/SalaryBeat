using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SalaryBeat.Core;
namespace SalaryBeat.Desktop;
public sealed partial class Shell {
 // Windows-only UI smoke check. No registry write, tray, profile migration or salary persistence.
 public void RenderPreviews(string folder){
  Directory.CreateDirectory(folder);
  var sample=MonthPlan.Default(DateTime.Now.ToString("yyyy-MM"));sample.Income=16800;sample.Confirmed=true;sample.Overrides[DateTime.Today.ToString("yyyy-MM-dd")]=new DateRule{Work=true};book.SaveMonth(sample);book.Theme="Light";
  BuildWindows();ApplyTheme();Update();
  void SaveVisual(FrameworkElement visual,string name,int width,int height){
   visual.Measure(new Size(width,height));visual.Arrange(new Rect(0,0,width,height));visual.UpdateLayout();
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(folder,name+".png"));encoder.Save(output);
  }
  SaveVisual((FrameworkElement)main.Content,"01-earnings-light",640,400);
  book.Theme="Dark";ApplyTheme();SaveVisual((FrameworkElement)main.Content,"02-earnings-dark",640,400);
  var back=new SettingsView(book,calendar,dataFolder,_=>{},()=>{});
  cardHost.Content=new System.Windows.Controls.Grid{Width=640,Height=400,Children={back}};
  SaveVisual((FrameworkElement)main.Content,"03-settings-dark",640,400);
  book.Theme="Light";ApplyTheme();SaveVisual((FrameworkElement)main.Content,"04-settings-light",640,400);
  SaveVisual((FrameworkElement)mini.Content,"05-mini-on-transparent",420,156);
  main.Close();mini.Close();
 }
}
