using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hemograma;
class ConfigurationTest {
 static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
 static void Main() {
  string folder=Path.Combine(Path.GetTempPath(),"hemograma-config-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
  try {
   string ini=Path.Combine(folder,"connection.ini");
   var original=new SqlConnectionStringBuilder {DataSource="localhost",InitialCatalog="fixture",UserID="usuario-prueba",Password="clave-sintetica"};
   SqlSettings.Save(original,ini);
   string raw=File.ReadAllText(ini);
   Check(!raw.Contains(original.UserID)&&!raw.Contains(original.Password)&&!raw.Contains("\nUser="),"No plain credentials");
   var decoded=new SqlConnectionStringBuilder(SqlSettings.Connection(ini,"Integrated Security=True"));
   Check(decoded.UserID==original.UserID && decoded.Password==original.Password,"DPAPI round trip");
   File.WriteAllText(ini,"[SQL]\nServer=localhost\nDatabase=fixture\nAuthentication=Sql\nUser="+original.UserID+"\nPasswordProtected="+Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(original.Password),null,DataProtectionScope.CurrentUser)));
   decoded=new SqlConnectionStringBuilder(SqlSettings.Connection(ini,"Integrated Security=True"));
   Check(decoded.UserID==original.UserID&&!File.ReadAllText(ini).Contains("\nUser="),"Legacy migration");
   string request=Path.Combine(folder,"request.ini"); File.WriteAllText(request,"User="+original.UserID+"\nPassword=\n");
   Check(SqlSettings.FromRequest(request,ini).Password==original.Password,"Upgrade preserves password");
   var list=TestSettings.Parse(TestSettings.DefaultValue+";800|Prueba adicional|false");
   Check(list.Length==10&&list[9].Id==800&&!list[9].Selected&&list.Count(t=>t.Selected)==8,"Additional test defaults");
   foreach(string invalid in new[]{"", "72|Uno|true;72|Dos|false", "1);DROP|X|true", "0|X|true", "72||true", "72|X|yes"}) {
    bool failed=false;try{TestSettings.Parse(invalid);}catch(System.Configuration.ConfigurationErrorsException){failed=true;}Check(failed,"Reject invalid config");
   }
   string config=Path.Combine(folder,"app.config"); File.WriteAllText(config,"<configuration><appSettings><add key='custom' value='preserved'/></appSettings></configuration>");
   TestSettings.Ensure(config); string first=File.ReadAllText(config);TestSettings.Ensure(config);
   Check(first==File.ReadAllText(config)&&first.Contains("preserved")&&first.Contains("Hemograma.Pruebas"),"Non-destructive migration");
   Check(Repository.TestIds.Length==10&&Repository.TestIds[9]==800,"Repository reads configured tests");
   using(var c=new SqlConnection("Data Source=localhost;Initial Catalog=eDelphyn;Integrated Security=True;Connect Timeout=8")) {
    c.Open();using(var cmd=new SqlCommand("CREATE TABLE #DONATION(ID_DONATION numeric(15,0),COD_DONATION varchar(20),DAT_DONATION datetime); CREATE TABLE #DONATION_TEST(ID_DONATION numeric(15,0),ID_TEST numeric(15,0),DES_RESULT varchar(20)); INSERT INTO #DONATION VALUES(1,'TEST','20261029'); INSERT INTO #DONATION_TEST VALUES(1,800,'42'),(1,93,'14');",c))cmd.ExecuteNonQuery();
    var repo=new Repository(true);var result=repo.Search(c,new Filter{FromCode="TEST",ToCode="TEST"});
    Check(result.Tests.Rows.Count==2,"Search added test");
    repo.Delete(c,result,new decimal[]{1},folder,new[]{800});
    using(var cmd=new SqlCommand("SELECT COUNT(*) FROM #DONATION_TEST WHERE ID_TEST=93",c))Check((int)cmd.ExecuteScalar()==1,"Unselected test preserved");
    var backup=Repository.LoadBackup(Directory.GetFiles(folder,"*.xml")[0]);
    Check(repo.RestoreBackup(c,backup,new[]{0})==1,"Restore added test");
   }
   Console.WriteLine("PASS: encrypted credentials, migration, validation, dynamic test search/delete/restore (temporary tables only).");
  } finally { foreach(string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
 }
}
