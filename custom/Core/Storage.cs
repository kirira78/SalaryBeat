using System.Text.Json;
namespace SalaryBeat.Core;
public class SettingsStore(string directory) {
 public bool Recovered {get;private set;}
 string FileName=>Path.Combine(directory,"settings.json");
 static SettingsBook Read(string file){var book=JsonSerializer.Deserialize<SettingsBook>(File.ReadAllText(file))??throw new InvalidDataException("配置为空");book.Validate();return book;}
 public SettingsBook Load(){
  Recovered=false;
  if(!File.Exists(FileName)&&!File.Exists(FileName+".bak"))return new();
  try{return Read(FileName);}catch(Exception ex) when(ex is IOException or JsonException or ArgumentException){
   try{var book=Read(FileName+".bak");Recovered=true;return book;}catch(Exception back) when(back is IOException or JsonException or ArgumentException){throw new InvalidDataException("主配置及备份均无法读取。原文件已保留，请检查 "+directory,back);}
  }
 }
 public void Save(SettingsBook book){
  book.Validate();Directory.CreateDirectory(directory);string temp=FileName+".tmp";
  using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,book,new JsonSerializerOptions{WriteIndented=true});stream.Flush(true);}
  if(File.Exists(FileName)){
   if(Recovered){File.Copy(FileName,FileName+".corrupt",true);File.Move(temp,FileName,true);}
   else File.Replace(temp,FileName,FileName+".bak");
  }else File.Move(temp,FileName);
  Recovered=false;
 }
}
