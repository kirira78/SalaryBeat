using System.Globalization;
using System.Text.Json;
namespace SalaryBeat.Core;
public class HolidayCalendar {
 public static HolidayCalendar LoadBundled(){
  var result=new HolidayCalendar();var assembly=typeof(HolidayCalendar).Assembly;
  foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("Calendar.",StringComparison.Ordinal)&&n.EndsWith(".json",StringComparison.Ordinal)).Order()){
   using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);result.ImportJson(reader.ReadToEnd());
  }
  return result;
 }
 readonly Dictionary<int,Dictionary<string,bool>> documents=new();
 public Dictionary<string,bool> Days {get;set;}=new();public HashSet<int> Years {get;set;}=new();
 public static HolidayCalendar LoadDirectory(string directory){var result=new HolidayCalendar();if(Directory.Exists(directory))foreach(var f in Directory.GetFiles(directory,"*.json").Where(f=>System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f),@"^\d{4}\.json$")).Order())result.Import(f);return result;}
 public void Import(string file)=>ImportJson(File.ReadAllText(file));
 public void ImportJson(string json){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  int year=root.GetProperty("year").GetInt32();if(year<1900||year>9999)throw new ArgumentException("日历年份无效。");
  var items=new Dictionary<string,bool>();
  foreach(var entry in root.GetProperty("days").EnumerateArray()){
   string key=entry.GetProperty("date").GetString()??"";
   if(!DateOnly.TryParseExact(key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)||d.Year<year-1||d.Year>year+1||!items.TryAdd(key,entry.GetProperty("isOffDay").GetBoolean()))throw new ArgumentException("日历存在无效或重复日期。");
  }
  // Adjacent-year days may occur in the State Council's annual notices.
  documents[year]=items;Days.Clear();foreach(var document in documents.OrderBy(x=>x.Key))foreach(var (key,off) in document.Value)Days[key]=off;Years.Add(year);
 }
}
public record Earnings(long TotalSeconds,long ElapsedSeconds,long TodaySeconds,long TodayTotalSeconds,decimal PerSecond,decimal Today,decimal Month,int Workdays,string Status);
// Continuous week parity follows SalaryGlow WorkCalendar (MIT); Monday is the week boundary.
public class PayrollEngine(HolidayCalendar calendar) {
 static DateOnly Monday(DateOnly d)=>d.AddDays(-(((int)d.DayOfWeek+6)%7));
 public void FreezeCalendar(MonthPlan plan)=>plan.FrozenOffDays=calendar.Days.Where(x=>x.Key.StartsWith(plan.Month+"-",StringComparison.Ordinal)).ToDictionary();
 public bool IsWorkday(MonthPlan plan,DateOnly date){
  string key=date.ToString("yyyy-MM-dd");if(plan.Overrides.TryGetValue(key,out var rule))return rule.Work;
  var national=plan.FrozenOffDays??calendar.Days;if(national.TryGetValue(key,out bool off))return !off;
  if(date.DayOfWeek==DayOfWeek.Sunday)return false;
  if(date.DayOfWeek!=DayOfWeek.Saturday)return true;
  if(plan.Mode==WorkMode.SingleRest)return true;
  if(plan.Mode==WorkMode.DoubleRest)return false;
  var anchor=DateOnly.ParseExact(plan.DoubleWeekAnchor,"yyyy-MM-dd",CultureInfo.InvariantCulture);
  int weeks=(Monday(date).DayNumber-Monday(anchor).DayNumber)/7;return weeks%2!=0;
 }
 public List<Slot> Intervals(MonthPlan plan,DateOnly date){
  if(!IsWorkday(plan,date))return new();
  if(plan.Overrides.TryGetValue(date.ToString("yyyy-MM-dd"),out var rule)&&rule.Intervals!=null)return rule.Intervals;
  var slots=plan.Week.GetValueOrDefault((int)date.DayOfWeek);
  return slots is {Count:>0}?slots:plan.Week.GetValueOrDefault(1)??new();
 }
 public Earnings At(MonthPlan plan,DateTime now){
  var start=DateOnly.ParseExact(plan.Month+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture);var today=DateOnly.FromDateTime(now);
  long total=0,elapsed=0,todayElapsed=0,todayTotal=0;int workdays=0;
  for(int day=1;day<=DateTime.DaysInMonth(start.Year,start.Month);day++){
   var date=new DateOnly(start.Year,start.Month,day);var slots=Intervals(plan,date);long seconds=slots.Sum(x=>x.Seconds);total+=seconds;if(seconds>0)workdays++;
   long earned=0;
   if(date<today)earned=seconds;
   else if(date==today){long second=now.TimeOfDay.Ticks/TimeSpan.TicksPerSecond;earned=slots.Sum(x=>Math.Clamp(second-x.Start*60L,0L,x.Seconds));todayElapsed=earned;todayTotal=seconds;}
   elapsed+=earned;
  }
  decimal rate=total==0?0:plan.Income/total;
  decimal month=total==0?0:plan.Income*elapsed/total;decimal dayMoney=total==0?0:plan.Income*todayElapsed/total;
  string status="休息日";
  if(today.ToString("yyyy-MM")==plan.Month){var slots=Intervals(plan,today);double minute=now.TimeOfDay.TotalMinutes;
   if(slots.Count>0)status=slots.Any(x=>minute>=x.Start&&minute<x.End)?"计薪中":minute<slots.Min(x=>x.Start)?"尚未上班":minute>=slots.Max(x=>x.End)?"今日已完成":"休息中";
   else if(IsWorkday(plan,today))status="无计薪时段";
  }
  return new(total,elapsed,todayElapsed,todayTotal,rate,dayMoney,month,workdays,status);
 }
}
