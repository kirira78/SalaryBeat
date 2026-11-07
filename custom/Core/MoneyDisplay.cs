using System.Globalization;
namespace SalaryBeat.Core;
public static class MoneyDisplay {
 public static string Format(decimal amount)=>decimal.Round(amount,2,MidpointRounding.AwayFromZero).ToString("N2",CultureInfo.GetCultureInfo("zh-CN"));
}
