using System;
using System.Collections.Generic;
using System.Globalization;
namespace SalaryWidget {
 public enum DayKind { Work, PaidLeave, Rest }
 public class DayOverride { public string Date; public DayKind Kind; }
 public static class WorkCalendar {
  // State Council 2026 schedule; statutory dates are separate from adjusted rest days.
  // https://www.gov.cn/zhengce/content/202511/content_7047090.htm
  static readonly HashSet<string> Paid = new HashSet<string>(new[]{"01-01","02-16","02-17","02-18","02-19","04-05","05-01","05-02","06-19","09-25","10-01","10-02","10-03"});
  static readonly HashSet<string> Makeup = new HashSet<string>(new[]{"01-04","02-14","02-28","05-09","09-20","10-10"});
  public static DateTime Monday(DateTime date) { return date.Date.AddDays(-(((int)date.DayOfWeek+6)%7)); }
  public static bool DoubleWeek(Preferences p,DateTime date) {
   DateTime anchor=DateTime.ParseExact(p.AlternateDoubleWeek,"yyyy-MM-dd",CultureInfo.InvariantCulture);
   int weeks=(int)((Monday(date)-Monday(anchor)).TotalDays/7);
   return weeks%2==0;
  }
  static bool Holiday(DateTime d) { int n=d.Month*100+d.Day; return n>=101&&n<=103 || n>=215&&n<=223 || n>=404&&n<=406 || n>=501&&n<=505 || n>=619&&n<=621 || n>=925&&n<=927 || n>=1001&&n<=1007; }
  public static DayKind Kind(Preferences p,DateTime d) {
   string key=d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
   if(p.Overrides!=null) foreach(var item in p.Overrides) if(item.Date==key) return item.Kind;
   if(p.China2026 && d.Year==2026) {
    string md=d.ToString("MM-dd",CultureInfo.InvariantCulture);
    if(Makeup.Contains(md)) return DayKind.Work;
    if(Paid.Contains(md)) return DayKind.PaidLeave;
    if(Holiday(d)) return DayKind.Rest;
   }
   bool work;
   if(p.AlternatingWeeks) work=d.DayOfWeek!=DayOfWeek.Sunday && (d.DayOfWeek!=DayOfWeek.Saturday || !DoubleWeek(p,d));
   else work=p.DoubleRest?d.DayOfWeek!=DayOfWeek.Saturday&&d.DayOfWeek!=DayOfWeek.Sunday:(int)d.DayOfWeek!=p.RestDay;
   return work?DayKind.Work:DayKind.Rest;
  }
  public static bool PaidDay(Preferences p,DateTime d) { var k=Kind(p,d); return k==DayKind.Work || k==DayKind.PaidLeave; }
  public static string Label(DayKind k) { return k==DayKind.Work?"出勤 / 补班":k==DayKind.PaidLeave?"带薪休假":"休息 / 不计薪"; }
 }
}
