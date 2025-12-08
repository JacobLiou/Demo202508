using System;
using System.IO;
using Microsoft.Data.Sqlite;
using SQLitePCL;

class Program
{
    static void Main()
    {
        Batteries_V2.Init(); // 初始化 SQLCipher 原生库绑定

        var dbPath = @"C:\Temp\MIMS_Local.db";
        var passphrase = "Molex_MIMS_Local_!2025"; // 请换成强密码

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        using var conn = new SqliteConnection($"Data Source={dbPath};");
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA key = '{passphrase.Replace("'", "''")}';";
            cmd.ExecuteNonQuery();
        }

        Exec(conn, "PRAGMA cipher_page_size = 4096;");
        Exec(conn, "PRAGMA kdf_iter = 256000;");
        Exec(conn, "PRAGMA journal_mode = WAL;");
        Exec(conn, "PRAGMA synchronous = NORMAL;");

        Exec(conn, @"CREATE TABLE IF NOT EXISTS Test(id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL);");
        Exec(conn, "INSERT INTO Test(name) VALUES('MIMS');");

        Console.WriteLine("SQLCipher 加密数据库已创建并插入测试数据。");
    }

    static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
