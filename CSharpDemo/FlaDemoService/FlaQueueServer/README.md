# FlaQueueServer

**Console + TCP 长连接 + 排队 + Mock测量** 的最小可用 Demo。

- 监听端口：默认 `5600`（可通过命令行参数覆盖）。
- 多客户端同时连接；提交测量任务走 **FIFO 队列**。
- 共享设备串行执行（用 SemaphoreSlim 保护）。
- 模拟测量：生成 `peak_pos_m`、`peak_db`、`length_m`。

## 运行
```bash
# 需要 .NET 8 SDK
cd FlaQueueServer
# 构建 & 运行（默认 5600）
dotnet run
# 指定端口
dotnet run -- 5800
```

## 协议（JSON-Lines）
每行一个 JSON，对象必须包含 `op` 字段。

### 客户端 → 服务端
- 提交任务
```json
{"command":"submit","channel":3,"mode":"scan","params":{"wr_len":"12.55","x_center":"005.1"}}
```

- 查询状态
```json
{"command":"status","params":{"taskId":"T2025..."}}
```

### 服务端 → 客户端
- 欢迎
```json
{"command":"hello","message":"FlaQueueServer ready"}
```
- ACK
```json
{"command":"ack","taskId":"T2025..."}
```
- STATUS
```json
{"command":"status","taskId":"T2025...","status":"running"}
```
- RESULT
```json
{"command":"result","taskId":"T2025...","success":true,
 "data":{"channel":3,"mode":"scan","peak_pos_m":13.226,"peak_db":-47.820,"length_m":24.98}}
```

## 测试（使用 netcat）
```bash
# 连接
nc 127.0.0.1 5600
# 发送提交
{"command":"submit","channel":1,"mode":"auto_peak","params":{"start_m":"0.5","end_m":"25"}}
```

## 结构
- `Program.cs`：TCP 服务、会话、队列、Worker、Mock测量。
- `FlaQueueServer.csproj`：.NET 8 Console 项目文件。

## 注意
- 这是最小 Demo，异常与断线处理做了基础防护，生产环境请增加日志、鉴权、重试与指标。
- 测量逻辑为 **Mock**，可替换为：串口（光源盒）、TCP（设备 OCI/SCAN/OP）。
