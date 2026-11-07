namespace SalaryBeat.Core;
public record WindowBounds(double Left,double Top,double Width,double Height);
public static class DesktopLayout {
 public static WindowBounds BottomRight(double left,double top,double width,double height){
  if(!double.IsFinite(left)||!double.IsFinite(top)||!double.IsFinite(width)||!double.IsFinite(height)||width<=32||height<=32)throw new ArgumentException("桌面工作区无效。");
  double scale=Math.Min(1,Math.Min((width-32)/640,(height-32)/400));
  double w=640*scale,h=400*scale;return new(left+width-w-16,top+height-h-16,w,h);
 }
}
public static class CalendarLinks {
 public static Uri YearPage(int year){if(year<1900||year>9999)throw new ArgumentOutOfRangeException(nameof(year));return new($"https://github.com/NateScarlet/holiday-cn/blob/master/{year}.json");}
}
public static class ProfileMigration {
 public static bool ImportLegacy(string legacy,string target){
  if(File.Exists(Path.Combine(target,"settings.json"))||File.Exists(Path.Combine(target,"settings.json.bak")))return false;
  if(!File.Exists(Path.Combine(legacy,"settings.json"))&&!File.Exists(Path.Combine(legacy,"settings.json.bak")))return false;
  var book=new SettingsStore(legacy).Load();
  string calendars=Path.Combine(legacy,"calendars");
  if(Directory.Exists(calendars)){
   _=HolidayCalendar.LoadDirectory(calendars);
   string folder=Path.Combine(target,"calendars");Directory.CreateDirectory(folder);
   foreach(string file in Directory.GetFiles(calendars,"*.json")){string dest=Path.Combine(folder,Path.GetFileName(file));if(!File.Exists(dest))File.Copy(file,dest);}
  }
  new SettingsStore(target).Save(book);return true;
 }
}
public sealed class CardNavigation {
 public bool IsSettings {get;private set;}
 public bool IsAnimating {get;private set;}
 bool target;
 public bool BeginFlip(bool settings){if(IsAnimating||IsSettings==settings)return false;target=settings;IsAnimating=true;return true;}
 public void CommitFlip(){if(IsAnimating)IsSettings=target;}
 public void FinishFlip(){IsAnimating=false;}
}
