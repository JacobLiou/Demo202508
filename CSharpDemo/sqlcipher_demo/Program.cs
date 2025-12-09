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



        // A. 检查是否真的加载了 SQLCipher
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "PRAGMA cipher_version;";
            var v = check.ExecuteScalar();
            Console.WriteLine($"cipher_version = {v ?? "(null)"}");
            if (v == null) throw new Exception("没有加载 SQLCipher 原生库。请检查 NuGet、Batteries_V2.Init() 和发布输出的原生 DLL。");
        }

        // B. 先设置密钥（第一条语句，不能用参数化）
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
