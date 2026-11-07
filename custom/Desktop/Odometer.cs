using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SalaryBeat.Core;
namespace SalaryBeat.Desktop;
// Explicit, top-anchored canvas coordinates: layout never re-centres a two-glyph track.
public class Odometer : StackPanel {
 string previous="";
 public FontWeight DigitWeight {get;set;}=FontWeights.SemiBold;
 public FontFamily DigitFamily {get;set;}=new FontFamily("Bahnschrift, Segoe UI");
 public double DigitSize {get;set;}=62;
 public Odometer(){Orientation=Orientation.Horizontal;UseLayoutRounding=true;SnapsToDevicePixels=true;}
 TextBlock Glyph(char c)=>new(){Text=c.ToString(),FontSize=DigitSize,FontFamily=DigitFamily,FontWeight=DigitWeight,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Top,SnapsToDevicePixels=true};
 public void SetAmount(decimal amount){
  string next=MoneyDisplay.Format(amount);if(next==previous)return;
  bool animate=SystemParameters.ClientAreaAnimation&&previous.Length==next.Length;
  // Measure the actual font rather than assuming a fixed fraction of its em-size.
  double digitWidth=0,lineHeight=DigitSize*1.5;
  foreach(char c in "0123456789.,-"){var sample=Glyph(c);sample.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));lineHeight=Math.Max(lineHeight,sample.DesiredSize.Height+4);if(char.IsDigit(c))digitWidth=Math.Max(digitWidth,sample.DesiredSize.Width+2);}
  lineHeight=Math.Ceiling(lineHeight);digitWidth=Math.Ceiling(digitWidth);
  Children.Clear();
  for(int i=0;i<next.Length;i++){
   var target=Glyph(next[i]);target.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
   double width=char.IsDigit(next[i])?digitWidth:Math.Ceiling(target.DesiredSize.Width+2);
   var viewport=new Canvas{Width=width,Height=lineHeight,ClipToBounds=true};
   TextBlock Fit(TextBlock text){text.Width=width;text.Height=lineHeight;return text;}
   if(animate&&previous[i]!=next[i]&&char.IsDigit(next[i])){
    var track=new Canvas{Width=width,Height=2*lineHeight};
    var old=Fit(Glyph(previous[i]));Fit(target);
    Canvas.SetTop(old,0);Canvas.SetTop(target,lineHeight);track.Children.Add(old);track.Children.Add(target);viewport.Children.Add(track);
    var shift=new TranslateTransform();track.RenderTransform=shift;
    var roll=new DoubleAnimation(0,-lineHeight,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
    roll.Completed+=(_,_)=>{
     // Settle to a static target, removing the translated track and its held animation.
     track.Children.Remove(target);Canvas.SetTop(target,0);viewport.Children.Clear();viewport.Children.Add(target);
    };
    shift.BeginAnimation(TranslateTransform.YProperty,roll);
   }else viewport.Children.Add(Fit(target));
   Children.Add(viewport);
  }
  previous=next;System.Windows.Automation.AutomationProperties.SetName(this,"人民币 "+next);
 }
}
