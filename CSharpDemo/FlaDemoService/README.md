# FlaDemoService（东隆科技 FLA 适配版）

一个基于 .NET 8 Minimal API + Worker 的后端服务：
- 通过 TCP(4300) 与东隆 FLA 设备通信，握手识别 `OCI`
- 支持 OTDR 常规扫描（`SCAN`）与自动寻峰（`SCAN_..._NACS`）
- 串行排队每台设备，多设备并行
- REST 接口：提交任务、查询状态、获取结果

## 运行
```bash
# 需要 .NET 8 SDK
cd FlaDemoService
dotnet run
```

## 配置
- 在 `appsettings.json` 中配置设备：IP、端口（默认 4300）、适配器类型。

## REST 示例
### 常规扫描
```bash
curl -X POST http://localhost:5000/api/measure \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId":"DL-FLA-01",
    "type":"Otdr",
    "params":{
      "op_mode":"scan",
      "sr_mode":"2",
      "gain":"5",
      "wr_len":"12.55",
      "x_center":"005.1"
    }
  }'
```

### 自动寻峰
```bash
curl -X POST http://localhost:5000/api/measure \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId":"DL-FLA-01",
    "type":"Otdr",
    "params":{
      "op_mode":"auto_peak",
      "start_m":"0.5",
      "end_m":"25",
      "count_mode":"2",
      "algo":"2",
      "width_m":"0.513",
      "threshold_db":"-80",
      "id":"09",
      "sn":"SN9II1"
    }
  }'
```

## 注意
- 需要在 FLA 软件中开启 Remote Control，连接后设备将发出 `OCI`；若未收到，适配器会报错。
- `WR_` 与 `X_` 指令均需 **5 字符**（含小数点），适配器已自动左补零和截断。
- `SCAN` 返回首行分辨率，随后每点 **12 字节**，尾部 `!`，适配器会解析为 `(distance, reflectance)` 曲线 JSON。
- 自动寻峰会计算并附带校验数（包含 ID 和 SN 中的数字），若设备返回 `INPUT_ERROR` 将标记任务失败。
