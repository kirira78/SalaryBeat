using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SalaryBeat.Core;
public enum WorkMode { DoubleRest, SingleRest, Alternating }
public record Slot(int Start,int End) {public long Seconds=>(End-Start)*60L;}
public static class Slots {
 public static List<Slot> Parse(string text) {
  var result=new List<Slot>();if(string.IsNullOrWhiteSpace(text))return result;
  foreach(string part in text.Replace('；',';').Split(';',StringSplitOptions.RemoveEmptyEntries)){
   var match=Regex.Match(part.Trim(),@"^(\d{2}):(\d{2})\s*[-–]\s*(\d{2}):(\d{2})$");
   if(!match.Success)throw new ArgumentException("时段格式应为 09:00-12:00;13:00-17:00。");
   int sh=int.Parse(match.Groups[1].Value),sm=int.Parse(match.Groups[2].Value),eh=int.Parse(match.Groups[3].Value),em=int.Parse(match.Groups[4].Value);
   if(sh>23||sm>59||eh>24||em>59||eh==24&&em!=0)throw new ArgumentException("时间超出 00:00–24:00 范围。");
   result.Add(new(sh*60+sm,eh*60+em));
  }
  Validate(result);return result.OrderBy(x=>x.Start).ToList();
 }
 public static void Validate(List<Slot> slots) {
  int end=-1;
  foreach(var slot in slots.OrderBy(x=>x.Start)){
   if(slot.Start<0||slot.End>1440||slot.Start>=slot.End||slot.Start<end)throw new ArgumentException("时段不可跨午夜、重叠或倒置，请按自然日拆分。");
   end=slot.End;
  }
 }
 public static string Format(IEnumerable<Slot> slots)=>string.Join(";",slots.Select(x=>$"{x.Start/60:00}:{x.Start%60:00}-{x.End/60:00}:{x.End%60:00}"));
}
public class DateRule {public bool Work {get;set;}public List<Slot>? Intervals {get;set;}}
public record MonthPlan {
 public string Month {get;set;}="";public decimal Income {get;set;}public bool Confirmed {get;set;}public WorkMode Mode {get;set;}
 public string DoubleWeekAnchor {get;set;}="2026-11-02";
 public Dictionary<string,bool>? FrozenOffDays {get;set;}
 public Dictionary<int,List<Slot>> Week {get;set;}=new();
 public Dictionary<string,DateRule> Overrides {get;set;}=new();
 public static MonthPlan Default(string month)=>new(){Month=month,Week=Enumerable.Range(0,7).ToDictionary(x=>x,x=>x>=1&&x<=5?new List<Slot>{new(540,720),new(780,1020)}:new())};
 public MonthPlan Copy()=>JsonSerializer.Deserialize<MonthPlan>(JsonSerializer.Serialize(this))!;
 public void Validate(){
  if(!DateOnly.TryParseExact(Month+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException("月份格式错误。");
  if(Income<0||Income>100000000m)throw new ArgumentException("收入须为 0 至 1 亿之间的金额。");
  if(!Enum.IsDefined(Mode)||!DateOnly.TryParseExact(DoubleWeekAnchor,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException("排班模式或基准日期无效。");
  if(Week==null||Week.Count!=7||Enumerable.Range(0,7).Any(x=>!Week.ContainsKey(x)||Week[x]==null))throw new ArgumentException("须包含完整周一至周日时段。");
  foreach(var slots in Week.Values)Slots.Validate(slots);
  if(Overrides==null)throw new ArgumentException("日期覆盖不能为空。");
  foreach(var (key,rule) in Overrides){if(!DateOnly.TryParseExact(key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)||d.ToString("yyyy-MM")!=Month||rule==null)throw new ArgumentException("额外日期须在所选月份内。");if(rule.Intervals!=null)Slots.Validate(rule.Intervals);}
  if(FrozenOffDays!=null && FrozenOffDays.Keys.Any(k=>!DateOnly.TryParseExact(k,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)||d.ToString("yyyy-MM")!=Month))throw new ArgumentException("冻结日历日期无效。");
 }
}
public class SettingsBook {
 public Dictionary<string,MonthPlan> Months {get;set;}=new();
 public string Theme {get;set;}="System";public bool Mini {get;set;}public bool PrimaryMonth {get;set;}public bool ClickThrough {get;set;}=true;
 public bool AutoStart {get;set;}=true;public bool Topmost {get;set;}=true;
 public int PlacementVersion {get;set;}
 public double MainLeft {get;set;}=100;public double MainTop {get;set;}=100;public double MiniLeft {get;set;}=100;public double MiniTop {get;set;}=100;
 public MonthPlan GetMonth(string month){
  if(Months.TryGetValue(month,out var exact))return exact.Copy();
  var prior=Months.Where(x=>string.CompareOrdinal(x.Key,month)<0).OrderByDescending(x=>x.Key,StringComparer.Ordinal).FirstOrDefault().Value;
  if(prior==null)return MonthPlan.Default(month);
  var result=prior.Copy();result.Month=month;result.Confirmed=false;result.Overrides.Clear();result.FrozenOffDays=null;return result;
 }
 public void SaveMonth(MonthPlan plan){plan.Validate();Months[plan.Month]=plan.Copy();}
 public void Validate(){
  if(Months==null||Theme is not ("System" or "Light" or "Dark")||new[]{MainLeft,MainTop,MiniLeft,MiniTop}.Any(x=>!double.IsFinite(x)))throw new ArgumentException("配置无效。");
  foreach(var (month,plan) in Months){if(plan==null||month!=plan.Month)throw new ArgumentException("月份配置不一致。");plan.Validate();}
 }
}
