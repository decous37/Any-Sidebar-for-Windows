using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AnySidebar.Core;
using Microsoft.Data.Sqlite;
namespace AnySidebar.Infrastructure;
public sealed class SqliteRepository:IRepository {
 readonly SqliteConnection connection;readonly string path;long lastBackup;
 public SqliteRepository(string directory){
  Directory.CreateDirectory(directory);path=Path.Combine(directory,"reading.sqlite3");
  connection=new(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false}.ToString());
  try{connection.Open();using var command=connection.CreateCommand();command.CommandText="PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS state(id INTEGER PRIMARY KEY CHECK(id IN (1,2)), body TEXT NOT NULL, sha TEXT NOT NULL);";command.ExecuteNonQuery();using var check=connection.CreateCommand();check.CommandText="PRAGMA quick_check";if(Convert.ToString(check.ExecuteScalar())!="ok")throw new InvalidDataException();}
  catch{connection.Dispose();throw new AppFault("DATABASE_CORRUPT","本机数据库无法读取。原文件已保留，请保留数据目录并检查备份，不要反复重新安装。",true);}
 }
 public State Load(){using var command=connection.CreateCommand();command.CommandText="SELECT body,sha FROM state WHERE id=1";using var reader=command.ExecuteReader();if(!reader.Read())return new();var body=reader.GetString(0);var sha=reader.GetString(1);if(Hash(body)!=sha)throw new AppFault("DATABASE_CORRUPT","本机记录校验失败，原文件与备份已保留。");try{return JsonSerializer.Deserialize<State>(body,J.Options)??throw new InvalidDataException();}catch{throw new AppFault("DATABASE_CORRUPT","本机记录格式无法读取，原文件与备份已保留。");}}
 static string Hash(string body)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
 public void Save(State state){
  var body=JsonSerializer.Serialize(state,J.Options);try{
   using var transaction=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=transaction;
   command.CommandText="INSERT OR REPLACE INTO state(id,body,sha) SELECT 2,body,sha FROM state WHERE id=1; INSERT OR REPLACE INTO state(id,body,sha) VALUES(1,$body,$sha);";command.Parameters.AddWithValue("$body",body);command.Parameters.AddWithValue("$sha",Hash(body));command.ExecuteNonQuery();transaction.Commit();
  }catch{throw AppFault.Storage();}
  if(Environment.TickCount64-lastBackup>30000){try{using var backup=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path+".backup",Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false}.ToString());backup.Open();connection.BackupDatabase(backup);lastBackup=Environment.TickCount64;}catch{/* A successful primary transaction is not rolled back by a separate backup failure. */}}
 }
 public void Dispose(){connection.Dispose();SqliteConnection.ClearAllPools();}
}
