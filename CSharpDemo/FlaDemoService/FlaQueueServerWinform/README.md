# OTMS WinForms（真实 FLA + 光开关 + 文件日志）

一个**WinForms 带界面**的中控服务端：
- TCP 长连接网关（JSON-Lines 协议，最多 16 客户端）；
- 队列排队（FIFO）、设备级串行；
- **真实 FLA 设备协议**（TCP 4300，连接后等待 `OCI`、支持 `SR/G/WR/X/SCAN` 与 `SCAN_..._NACS`）；
- **光开关 RS232 控制**（ASCII + CRLF，`SW X SPOS M N`，返回 `OK`、提示符 `>`）；
- **Serilog 文件日志** + 界面日志/错误日志。

## 运行
```bash
# 需要 .NET 8 SDK on Windows
cd WinForms_OTMS_FLA_Switch_Log
dotnet run
```

## 界面说明
- 顶部显示 Server IP/Port，提供 **Init Device**（握手/通道测试）与 **Setting**（参数配置）按钮。
- 左侧：Clients/Request 列表；右侧：Log/ErrorLog（同时写入 `logs/` 文件）。

## 客户端协议示例
```json
{"op":"submit","channel":3,"mode":"scan","params":{"sr_mode":"2","gain":"5","wr_len":"12.55","x_center":"005.1"}}
```
```json
{"op":"submit","channel":7,"mode":"auto_peak",
 "params":{"start_m":"0.5","end_m":"25","count_mode":"2","algo":"2","width_m":"0.513","threshold_db":"-80","id":"09","sn":"SN9II1"}}
```

## 参考协议
- 开关：115200-8N1、ASCII、CRLF 结束、返回 `OK` 与 `>` 提示符、`SW X SPOS M N`。来源：Switch Equipment Program Manual V1.2。
- FLA：TCP 4300、连接后设备发送 `OCI`，`SR/G/WR/X/SCAN`、`SCAN`首行分辨率（随后每点12字节直到`!`）、自动寻峰返回 `OP_..._PO`。来源：FLA远程说明。
