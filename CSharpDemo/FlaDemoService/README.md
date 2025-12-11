# FlaDemoService（含 Swagger + Mock）
一个基于 .NET 8 Minimal API + Worker 的后端服务：
- 通过 TCP(4300) 与东隆 FLA 设备通信，握手识别 `OCI`
- 支持 OTDR 常规扫描（`SCAN`）与自动寻峰（`SCAN_..._NACS`）
- 串行排队每台设备，多设备并行
- REST 接口：提交任务、查询状态、获取结果

- TCP(4300) 远控东隆 FLA，握手 `OCI`
- 支持 OTDR `SCAN` 与自动寻峰 `SCAN_..._NACS`
- 设备级串行排队，REST API + Swagger UI（根路径）
- 内置 **Mock FLA TCP Server**（127.0.0.1:4300），可直接联调

## 运行
```bash
cd FlaDemoService
# 首次需要还原 NuGet 包
dotnet restore
# 运行
dotnet run
```

## 配置
`appsettings.json`：
```json
{
  "Devices": {
    "DL-FLA-01": { "Host": "192.168.1.1", "Port": 4300 },
    "DL-FLA-MOCK": { "Host": "127.0.0.1", "Port": 4300 }
  },
  "Mock": { "Enabled": true, "Port": 4300, "ResolutionM": 0.005, "Points": 200 }
}
```

## REST 示例
见 Swagger UI：打开 `http://localhost:5000`。
