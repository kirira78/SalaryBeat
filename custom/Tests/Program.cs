using SalaryBeat.Core;

int checks = 0, failures=0;
void Group(string name,Action run) {try {run();Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
void Eq<T>(T expected, T actual, string name) { checks++; if (!Equals(expected, actual)) throw new Exception($"{name}: expected {expected}, got {actual}"); }
void Near(decimal expected, decimal actual, string name) { checks++; if(Math.Abs(expected-actual)>0.000001m) throw new Exception($"{name}: expected {expected}, got {actual}"); }
void Throws(Action action, string name) { checks++; try { action(); } catch(ArgumentException) { return; } throw new Exception(name+": expected validation failure"); }
var calendar=HolidayCalendar.LoadDirectory(AppContext.BaseDirectory);
var p=MonthPlan.Default("2026-11"); p.Income=14700; p.Confirmed=true;
var engine=new PayrollEngine(calendar);
Group("payroll and work/break boundaries",()=> {
// November 2026 has 21 weekdays * 7 hours; natural time, not process uptime.
var v=engine.At(p,new DateTime(2026,11,2,10,0,0));
Eq(529200L,v.TotalSeconds,"month denominator"); Near(100m,v.Today,"one working hour");
Near(14700m/529200m,v.PerSecond,"constant rate");
foreach(var (hour,amount) in new[]{(8,0m),(12,300m),(13,300m),(17,700m),(23,700m)})
 Near(amount,engine.At(p,new DateTime(2026,11,2,hour,0,0)).Today,"work and lunch boundaries");
Near(14700m,engine.At(p,new DateTime(2026,12,1)).Month,"month end exact cap");
Near(0m,engine.At(p,new DateTime(2026,10,31)).Month,"future income not earned");
Near(0m,engine.At(p,new DateTime(2026,11,1,15,0,0)).Today,"rest day");
Eq("休息日",engine.At(p,new DateTime(2026,11,1,15,0,0)).Status,"rest status");
Eq("休息中",engine.At(p,new DateTime(2026,11,2,12,30,0)).Status,"break status");
});
Group("official/manual and alternating calendar",()=> {
Eq(3,calendar.Years.Count,"three bundled official years");
Eq(false,engine.IsWorkday(MonthPlan.Default("2025-05"),new DateOnly(2025,5,1)),"prior-year holiday");
// Official holidays beat Saturday work, and make-up days are counted once.
p=MonthPlan.Default("2026-10"); p.Mode=WorkMode.SingleRest;
Eq(false,engine.IsWorkday(p,new DateOnly(2026,10,3)),"holiday beats weekly schedule");
Eq(true,engine.IsWorkday(p,new DateOnly(2026,10,10)),"official makeup");
p.Overrides["2026-10-03"]=new DateRule { Work=true };
p.Overrides["2026-10-10"]=new DateRule { Work=false };
Eq(true,engine.IsWorkday(p,new DateOnly(2026,10,3)),"manual work highest priority");
Eq(false,engine.IsWorkday(p,new DateOnly(2026,10,10)),"manual rest highest priority");
p=MonthPlan.Default("2026-11"); p.Mode=WorkMode.Alternating; p.DoubleWeekAnchor="2026-11-02";
foreach(var (date,work) in new[]{("2026-10-31",true),("2026-11-07",false),("2026-11-14",true),("2026-11-21",false),("2026-12-05",false)})
 Eq(work,engine.IsWorkday(p,DateOnly.Parse(date)),"continuous alternating "+date);
});
Group("unequal hours and natural-day slots",()=> {
// Unequal hours, fallback Monday, date-level intervals, and zero denominator.
p=MonthPlan.Default("2026-11"); p.Mode=WorkMode.SingleRest;
p.Week[6]=new(){new(540,780)}; p.Income=10000;
Eq(586800L,engine.At(p,new DateTime(2026,11,30,23,59,59)).TotalSeconds,"Saturday four hours");
p.Overrides["2026-11-01"]=new DateRule{Work=true};
Eq(25200L,engine.Intervals(p,new DateOnly(2026,11,1)).Sum(x=>x.Seconds),"Monday fallback");
p.Overrides["2026-11-01"].Intervals=new(){new(0,360),new(540,1020)};
Eq(50400L,engine.Intervals(p,new DateOnly(2026,11,1)).Sum(x=>x.Seconds),"natural-day multiple shifts");
p=MonthPlan.Default("2026-11"); foreach(var key in p.Week.Keys.ToArray()) p.Week[key]=new();
Eq(0m,engine.At(p,DateTime.Now).PerSecond,"zero-safe");
foreach(var text in new[]{"09:00-12:00;13:00-17:00","00:00-06:00;09:00-24:00",""}) Slots.Parse(text);
foreach(var text in new[]{"22:00-06:00","09:00-12:00;11:00-13:00","24:00-24:00","9:99-12:00"})
 Throws(()=>Slots.Parse(text),"invalid times "+text);
});
Group("frozen historical calendar",()=> {
p=MonthPlan.Default("2026-10"); engine.FreezeCalendar(p);
calendar.Days["2026-10-01"]=false;
Eq(false,engine.IsWorkday(p,new DateOnly(2026,10,1)),"frozen historical calendar");
});
var book=new SettingsBook();
Group("month snapshots and future inheritance",()=> {
// Carry forward the most recent prior month, not future values; never copy manual dates.
p=MonthPlan.Default("2026-10"); p.Income=9000; p.Confirmed=true;
p.Overrides["2026-10-11"]=new DateRule{Work=true}; book.SaveMonth(p);
book.SaveMonth(MonthPlan.Default("2026-12") with {Income=12000,Confirmed=true});
var inherited=book.GetMonth("2026-11");
Eq(9000m,inherited.Income,"inherit prior not future"); Eq(false,inherited.Confirmed,"persistent reminder"); Eq(0,inherited.Overrides.Count,"no date inheritance");
inherited.Income=10000; inherited.Confirmed=true; book.SaveMonth(inherited);
Eq(9000m,book.GetMonth("2026-10").Income,"history immutable"); Eq(12000m,book.GetMonth("2026-12").Income,"explicit future preserved");
});
Group("atomic storage and backup recovery",()=> {
var dir=Path.Combine(Path.GetTempPath(),"salaryglow-tests-"+Guid.NewGuid());
var store=new SettingsStore(dir); store.Save(book); book.PrimaryMonth=true; store.Save(book);
Eq(true,store.Load().PrimaryMonth,"storage roundtrip");
File.WriteAllText(Path.Combine(dir,"settings.json"),"broken");
Eq(false,store.Load().PrimaryMonth,"backup recovery"); Eq(true,store.Recovered,"recovery reported");
Directory.Delete(dir,true);
});
Group("existing behavior characterization: validation and month conservation",()=> {
 foreach(var bad in new[]{-1m,100000001m}) {var badPlan=MonthPlan.Default("2026-11");badPlan.Income=bad;Throws(()=>badPlan.Validate(),"invalid salary");}
 var invalid=MonthPlan.Default("2026-11");invalid.Overrides["2026-12-01"]=new(){Work=true};Throws(()=>invalid.Validate(),"out of month override");
 var variable=MonthPlan.Default("2026-11");variable.Income=12345.67m;variable.Mode=WorkMode.SingleRest;variable.Week[6]=new(){new(600,840)};
 decimal sum=0;
 for(int day=1;day<=30;day++)sum+=engine.At(variable,new DateTime(2026,11,day,23,59,59)).Today;
 Near(variable.Income,sum,"daily values conserve month salary");
 var earlier=engine.At(variable,new DateTime(2026,11,2,10,0,0));variable.Income*=2;var doubled=engine.At(variable,new DateTime(2026,11,2,10,0,0));
 Near(earlier.Today*2,doubled.Today,"retroactive recalculation");
 var noPrevious=new SettingsBook();noPrevious.SaveMonth(MonthPlan.Default("2026-12") with {Income=10000});Eq(0m,noPrevious.GetMonth("2026-11").Income,"future salary never leaks backwards");
});
Group("calendar correction replaces stale dates",()=> {
 var file=Path.Combine(Path.GetTempPath(),"salaryglow-calendar-"+Guid.NewGuid()+".json");
 try {
  var corrected=new HolidayCalendar();
  File.WriteAllText(file,"{\"year\":2026,\"days\":[{\"date\":\"2026-11-01\",\"isOffDay\":false}]}");corrected.Import(file);
  Eq(true,corrected.Days.ContainsKey("2026-11-01"),"initial makeup day");
  File.WriteAllText(file,"{\"year\":2026,\"days\":[]}");corrected.Import(file);
  Eq(false,corrected.Days.ContainsKey("2026-11-01"),"removed day must not remain stale");
 } finally {if(File.Exists(file))File.Delete(file);}
});
Group("embedded holiday years and source links",()=> {
 var bundled=HolidayCalendar.LoadBundled();
 Eq(true,bundled.Years.Contains(2026),"2026 must work without sidecar JSON");Eq(true,bundled.Years.Contains(2027),"2027 must work without sidecar JSON");
 Eq(true,bundled.Days["2026-10-01"],"embedded National Day");
 Eq("https://github.com/NateScarlet/holiday-cn/blob/master/2028.json",CalendarLinks.YearPage(2028).AbsoluteUri,"missing-year direct link");
 Throws(()=>CalendarLinks.YearPage(0),"invalid calendar year");
});
Group("compact bottom-right 16:10 placement",()=> {
 var dock=DesktopLayout.BottomRight(0,0,1920,1040);
 Eq(new WindowBounds(1264,624,640,400),dock,"dock above taskbar");
 var negative=DesktopLayout.BottomRight(-1920,0,1920,1040);Eq(-656d,negative.Left,"negative monitor coordinates");
 var tiny=DesktopLayout.BottomRight(0,0,600,350);Near(1.6m,(decimal)(tiny.Width/tiny.Height),"preserve ratio when scaled");
 Eq(true,tiny.Left>=0 && tiny.Top>=0 && tiny.Left+tiny.Width<=600 && tiny.Top+tiny.Height<=350,"fit small work area");
});
Group("rename imports legacy profile without overwriting",()=> {
 string dir=Path.Combine(Path.GetTempPath(),"salarybeat-migration-"+Guid.NewGuid());
 try {
  string old=Path.Combine(dir,"old"),dest=Path.Combine(dir,"new");var legacy=new SettingsBook();legacy.SaveMonth(MonthPlan.Default("2026-11") with {Income=12345,Confirmed=true});new SettingsStore(old).Save(legacy);
  Directory.CreateDirectory(Path.Combine(old,"calendars"));File.WriteAllText(Path.Combine(old,"calendars","2030.json"),"{\"year\":2030,\"days\":[]}");
  Eq(true,ProfileMigration.ImportLegacy(old,dest),"first rename migration");Eq(12345m,new SettingsStore(dest).Load().GetMonth("2026-11").Income,"income retained");
  Eq(true,File.Exists(Path.Combine(old,"settings.json")),"old file preserved");Eq(true,File.Exists(Path.Combine(dest,"calendars","2030.json")),"custom calendar retained");
  Eq(false,ProfileMigration.ImportLegacy(old,dest),"existing new profile not overwritten");
 }finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
});
Group("settings flips inside the same card",()=> {
 var face=new CardNavigation();Eq(false,face.IsSettings,"initial earnings face");Eq(true,face.BeginFlip(true),"start flip");
 Eq(false,face.IsSettings,"keep front until midpoint");Eq(false,face.BeginFlip(false),"ignore repeated clicks during animation");
 face.CommitFlip();Eq(true,face.IsSettings,"settings at midpoint");face.FinishFlip();Eq(false,face.IsAnimating,"animation complete");
 Eq(false,face.BeginFlip(true),"opening already shown face is idempotent");Eq(true,face.BeginFlip(false),"flip back");face.CommitFlip();face.FinishFlip();Eq(false,face.IsSettings,"return to earnings");
});
Group("rounded money stays complete through carry boundaries",()=> {
 foreach(var (value,text) in new[]{(0m,"0.00"),(.0049m,"0.00"),(.005m,"0.01"),(1.005m,"1.01"),(9.994m,"9.99"),(9.995m,"10.00"),(99.995m,"100.00"),(999.995m,"1,000.00"),(-1.005m,"-1.01")})
  Eq(text,MoneyDisplay.Format(value),"round half away from zero "+value);
});
Console.WriteLine($"{checks} assertions attempted; {failures} failing groups");
return failures==0?0:1;
