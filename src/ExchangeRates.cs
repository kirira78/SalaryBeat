using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace SalaryWidget {
 public class FxQuote {
  public string Base, Date;
  public decimal Rate;
  public DateTime CheckedUtc;
 }
 public sealed class ExchangeRates {
  public static readonly string[] Codes={"CNY","HKD","USD","EUR","JPY","GBP","AUD","CAD","SGD","CHF","NZD","MOP","TWD","KRW"};
  public static readonly string[] Names={"人民币","港币","美元","欧元","日元","英镑","澳元","加元","新加坡元","瑞士法郎","新西兰元","澳门元","新台币","韩元"};
  readonly string folder;
  readonly Dictionary<string,FxQuote> quotes=new Dictionary<string,FxQuote>();
  readonly Dictionary<string,DateTime> attempts=new Dictionary<string,DateTime>();
  readonly HashSet<string> busy=new HashSet<string>();
  readonly HashSet<string> failed=new HashSet<string>();
  readonly HashSet<string> cacheFailed=new HashSet<string>();
  public ExchangeRates(string path) { folder=path; }
  public static bool Supported(string code) { return Array.IndexOf(Codes,code)>=0; }
  public static bool Valid(FxQuote q,string code) {
   DateTime date;
   return q!=null && q.Base==code && q.Rate>0 && q.Rate<1000000 && DateTime.TryParseExact(q.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date) && date.Date<=DateTime.UtcNow.Date.AddDays(1);
  }
  public static FxQuote Parse(string json,string code) {
   var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
   if(!data.ContainsKey("base") || !data.ContainsKey("quote") || !data.ContainsKey("rate") || !data.ContainsKey("date") || (string)data["quote"]!="CNY") throw new InvalidDataException("Invalid currency pair");
   var q=new FxQuote { Base=(string)data["base"], Date=(string)data["date"], Rate=Convert.ToDecimal(data["rate"],CultureInfo.InvariantCulture), CheckedUtc=DateTime.UtcNow };
   if(!Valid(q,code)) throw new InvalidDataException("Invalid rate"); return q;
  }
  public FxQuote Get(string code) {
   if(!Supported(code) || code=="CNY") return null;
   FxQuote q;
   if(quotes.TryGetValue(code,out q)) return q;
   try { q=new JavaScriptSerializer().Deserialize<FxQuote>(File.ReadAllText(Path.Combine(folder,code+".json"))); if(!Valid(q,code)) q=null; } catch { q=null; }
   quotes[code]=q; return q;
  }
  public bool Busy(string code) { return busy.Contains(code); }
  public string Status(string code) {
   var q=Get(code);
   if(q==null) return Busy(code)?"正在获取汇率 · 暂显示原币":"暂无汇率 · 显示原币，可点击刷新";
   string stamp=q.CheckedUtc==default(DateTime)?"缓存":q.CheckedUtc.ToLocalTime().ToString("MM-dd HH:mm");
   return string.Format("1 {0} = {1:0.####} CNY · 数据 {2}\nFrankfurter · 日更参考价 · 检查 {3}{4}",code,q.Rate,q.Date,stamp,failed.Contains(code)?" · 更新失败，沿用缓存":Busy(code)?" · 更新中":cacheFailed.Contains(code)?" · 缓存保存失败":"");
  }
  public async Task Refresh(string code,bool force) {
   if(!Supported(code)||code=="CNY"||Busy(code)) return;
   DateTime last; if(attempts.TryGetValue(code,out last) && DateTime.UtcNow-last<TimeSpan.FromSeconds(force?15:3600)) return;
   attempts[code]=DateTime.UtcNow; busy.Add(code);
   try {
    var q=await Fetch(code); quotes[code]=q; failed.Remove(code);
    try { Directory.CreateDirectory(folder); string path=Path.Combine(folder,code+".json"),tmp=path+".tmp"; File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(q)); if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path); cacheFailed.Remove(code); } catch { cacheFailed.Add(code); }
   } catch { failed.Add(code); } finally { busy.Remove(code); }
  }
  public static async Task<FxQuote> Fetch(string code) {
   if(!Supported(code)||code=="CNY") throw new ArgumentException("Unsupported currency");
   ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
   using(var client=new HttpClient { Timeout=TimeSpan.FromSeconds(12) }) {
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SalaryGlow/3.0");
    var json=await client.GetStringAsync("https://api.frankfurter.dev/v2/rate/"+code+"/CNY"); return Parse(json,code);
   }
  }
 }
}
