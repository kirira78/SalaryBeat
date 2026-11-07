using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using SalaryBeat.Core;
namespace SalaryBeat.Desktop;
public static class UiTheme {
 public static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
 public static void Install(Window window,string theme){
  using var stream=typeof(UiTheme).Assembly.GetManifestResourceStream("Theme.xaml")!;
  window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));Apply(window,theme);
 }
 public static bool Apply(Window window,string theme){
  bool systemDark=false;try{systemDark=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;}catch{}
  bool dark=theme=="Dark"||theme=="System"&&systemDark;
  string[] keys={"Canvas","Surface","Ink","Muted","Line","Soft"};
  string[] colors=dark?new[]{"#142A24","#1D372F","#EEF6F1","#B0C7BA","#355348","#29463B"}:new[]{"#F7F8F7","#FFFFFF","#183C33","#647970","#DDE8E1","#EDF4EF"};
  for(int i=0;i<keys.Length;i++)window.Resources[keys[i]]=Brush(colors[i]);
  window.Foreground=Brush(colors[2]);window.Resources[SystemColors.WindowBrushKey]=Brush(colors[1]);window.Resources[SystemColors.WindowTextBrushKey]=Brush(colors[2]);window.Resources[SystemColors.ControlBrushKey]=Brush(colors[5]);window.Resources[SystemColors.ControlTextBrushKey]=Brush(colors[2]);return dark;
 }
 public static TextBlock Text(string text,double size=13,string color="Ink") {var t=new TextBlock{Text=text,FontSize=size};t.SetResourceReference(TextBlock.ForegroundProperty,color);return t;}
 public static Button Action(string text,Action action,string? style=null){var b=new Button{Content=text};if(style!=null)b.SetResourceReference(FrameworkElement.StyleProperty,style);b.Click+=(_,_)=>action();return b;}
 public static Border Card(UIElement content,int padding=18){var b=new Border{Child=content,Padding=new Thickness(padding)};b.SetResourceReference(FrameworkElement.StyleProperty,"Card");return b;}
 public static TextBlock CalendarLink(int year,string? text=null){var t=Text("",11);var link=new Hyperlink(new Run(text??$"获取 {year} 年官方日历 ↗")){NavigateUri=CalendarLinks.YearPage(year),Foreground=Brush("#158265")};link.RequestNavigate+=(_,e)=>{try{Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});}catch(Exception ex){MessageBox.Show("无法打开浏览器："+ex.Message,"薪跳");}e.Handled=true;};t.Inlines.Add(link);return t;}
}
