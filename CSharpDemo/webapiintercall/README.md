# 四节点 Console 自托管 Web API 互相通信 Demo

基于 **C# .NET Framework 4.6.1**，四个控制台各自用 **HttpListener** 自托管一个最小 Web API，彼此通过 HTTP 互相收发消息。**无 OWIN、无 Kestrel、无 NuGet 依赖**，仅用 BCL。

## 结构

- **Shared**：公共库
  - `MessageDto`：消息 DTO（From / To / Content / Time）
  - `PeerConfig`：本节点名、监听地址、对等节点基地址列表
  - `JsonHelper`：内置 `DataContractJsonSerializer` 序列化
  - `SimpleHttpServer`：基于 `HttpListener` 的极简 HTTP 服务
  - `PeerClient`：`HttpClient` 向对等节点 POST 消息
  - `ProgramRunner`：通用启动逻辑（启动服务 + 控制台输入广播）
- **Console1 ~ Console4**：各为一个控制台，仅调用 `ProgramRunner.Run("ConsoleN", 500N)`，端口 5001–5004。

## 通用 API（每个节点一致）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/message` | 接收消息，Body 为 `MessageDto` JSON |
| GET  | `/api/peers`    | 返回本节点名 `{"name":"ConsoleN"}` |

## 运行方式

1. 用 Visual Studio 或 MSBuild 编译解决方案。
2. 依次启动四个控制台（顺序任意）：
   - 运行 `Console1\bin\Debug\Console1.exe`（端口 5001）
   - 运行 `Console2\bin\Debug\Console2.exe`（端口 5002）
   - 运行 `Console3\bin\Debug\Console3.exe`（端口 5003）
   - 运行 `Console4\bin\Debug\Console4.exe`（端口 5004）
3. 在任意一个控制台输入一行文字并回车，会**广播**到另外三个节点，它们都会打印 `[收到] From -> To: Content`。
4. 输入 `exit` 退出当前节点。

## 扩展

- 若需点对点发往某一节点：可用 `PeerClient.SendToAsync(peerBaseUrl, msg)`，或扩展 `MessageDto.To` 在服务端解析后只转发给指定节点。
- 若需改用 OWIN 自托管：可替换 `SimpleHttpServer` 为 OWIN + Web API，保留 `MessageDto`、`PeerConfig`、`PeerClient` 和 `ProgramRunner` 的调用方式即可。
