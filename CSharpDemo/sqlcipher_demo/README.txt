# SQLCipher C# Demo

## 步骤
1. 安装 NuGet 包：
   ```powershell
   dotnet add package SQLitePCLRaw.bundle_sqlcipher
   dotnet add package Microsoft.Data.Sqlite
   ```

2. 将 Program.cs 添加到你的项目中。

3. 运行：
   ```powershell
   dotnet run
   ```

## 功能
- 创建一个 SQLCipher 加密数据库 (C:\data\secure.db)
- 设置加密密钥并插入测试数据

## 验证
- 使用 SQLiteStudio 打开数据库，输入密码 `Your_Strong_Passphrase_!2025`
