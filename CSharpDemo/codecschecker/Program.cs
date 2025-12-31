using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HardwareEncoderDecoderTester
{
    class HardwareCodecTester
    {
        // 支持的硬件加速类型
        private static readonly Dictionary<string, string> HardwareAccelerations = new Dictionary<string, string>
        {
            { "h264_qsv", "Intel Quick Sync Video (H.264)" },      // Intel Quick Sync Video是Intel GPU中的一组硬件特性 [[1]]
            { "hevc_qsv", "Intel Quick Sync Video (HEVC)" },
            { "h264_nvenc", "NVIDIA NVENC (H.264)" },              // NVENC是NVIDIA开发的API，用于使用NVIDIA GPU进行H.264和HEVC编码 [[7]]
            { "hevc_nvenc", "NVIDIA NVENC (HEVC)" },
            { "h264_amf", "AMD AMF (H.264)" },
            { "hevc_amf", "AMD AMF (HEVC)" },
            { "h264_vaapi", "VAAPI (Linux/Intel)" },
            { "hevc_vaapi", "VAAPI (Linux/Intel)" }
        };

        // 测试用的示例视频文件（请替换为实际存在的视频文件）
        private const string TestVideoFile = "test_video.mp4";
        private const string OutputDirectory = "hardware_test_results";

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 硬件编码器/解码器自动测试工具 ===");
            Console.WriteLine($"开始时间: {DateTime.Now}");
            Console.WriteLine();

            // 检查FFmpeg是否可用
            if (!CheckFFmpegAvailability())
            {
                Console.WriteLine("错误: 未找到FFmpeg或FFmpeg未正确配置。");
                Console.WriteLine("请确保FFmpeg已安装并添加到系统PATH，且支持硬件加速。");
                Console.WriteLine("FFmpeg必须构建时包含硬件加速功能支持。"); // ffmpeg必须构建时包含此功能 [[5]]
                return;
            }

            // 创建输出目录
            Directory.CreateDirectory(OutputDirectory);

            // 检测可用的硬件加速器
            var availableCodecs = await DetectAvailableHardwareCodecs();
            Console.WriteLine($"检测到 {availableCodecs.Count} 个可用的硬件编解码器");
            Console.WriteLine();

            // 测试所有检测到的编解码器
            var results = new List<TestResult>();
            foreach (var codec in availableCodecs)
            {
                Console.WriteLine($"测试 {codec.FriendlyName} ({codec.CodecName})...");
                var result = await TestHardwareCodec(codec.CodecName, codec.FriendlyName);
                results.Add(result);
                Console.WriteLine($"- 状态: {(result.Success ? "✅ 成功" : "❌ 失败")}");
                Console.WriteLine($"- 耗时: {result.Duration.TotalSeconds:F2} 秒");
                if (!result.Success)
                {
                    Console.WriteLine($"- 错误: {result.ErrorMessage}");
                }
                Console.WriteLine();
            }

            // 生成测试报告
            GenerateReport(results);

            Console.WriteLine("=== 测试完成 ===");
            Console.WriteLine($"结束时间: {DateTime.Now}");
        }

        private static bool CheckFFmpegAvailability()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(5000);

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<List<CodecInfo>> DetectAvailableHardwareCodecs()
        {
            var availableCodecs = new List<CodecInfo>();

            foreach (var codec in HardwareAccelerations)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-hide_banner -encoders | findstr {codec.Key}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(output) && output.Contains(codec.Key))
                    {
                        availableCodecs.Add(new CodecInfo
                        {
                            CodecName = codec.Key,
                            FriendlyName = codec.Value
                        });
                    }
                }
                catch
                {
                    continue;
                }
            }

            return availableCodecs;
        }

        private static async Task<TestResult> TestHardwareCodec(string codecName, string friendlyName)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new TestResult
            {
                CodecName = codecName,
                FriendlyName = friendlyName,
                StartTime = DateTime.Now
            };

            try
            {
                // 检查测试文件是否存在
                if (!File.Exists(TestVideoFile))
                {
                    throw new FileNotFoundException($"测试视频文件不存在: {TestVideoFile}");
                }

                // 构建输出文件名
                var outputFileName = Path.Combine(OutputDirectory, 
                    $"test_{codecName}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

                // 构建FFmpeg命令
                string ffmpegArgs;
                bool isEncoder = codecName.Contains("_nvenc") || codecName.Contains("_qsv") || codecName.Contains("_amf");

                if (isEncoder)
                {
                    // 测试编码器
                    ffmpegArgs = $"-y -hwaccel auto -i \"{TestVideoFile}\" -c:v {codecName} -b:v 2M -c:a copy \"{outputFileName}\"";
                }
                else
                {
                    // 测试解码器
                    ffmpegArgs = $"-y -hwaccel auto -c:v {codecName} -i \"{TestVideoFile}\" -f null -";
                }

                Console.WriteLine($"  执行命令: ffmpeg {ffmpegArgs}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                // 捕获错误输出
                var errorBuilder = new StringBuilder();
                process.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        Console.WriteLine($"  FFmpeg: {e.Data}");
                    }
                };

                process.Start();
                process.BeginErrorReadLine();
                
                // 等待进程完成，设置超时
                if (!await WaitForProcessWithTimeout(process, TimeSpan.FromMinutes(2)))
                {
                    throw new TimeoutException($"测试超时 (2分钟)");
                }

                var exitCode = process.ExitCode;
                if (exitCode != 0)
                {
                    throw new Exception($"FFmpeg返回错误代码: {exitCode}\n{errorBuilder}");
                }

                // 验证输出文件（如果是编码测试）
                if (isEncoder && !File.Exists(outputFileName))
                {
                    throw new FileNotFoundException($"输出文件未生成: {outputFileName}");
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.EndTime = DateTime.Now;
            }

            return result;
        }

        private static async Task<bool> WaitForProcessWithTimeout(Process process, TimeSpan timeout)
        {
            var timeoutTask = Task.Delay(timeout);
            var processTask = Task.Run(() => process.WaitForExit());

            var completedTask = await Task.WhenAny(timeoutTask, processTask);

            return completedTask == processTask;
        }

        private static void GenerateReport(List<TestResult> results)
        {
            Console.WriteLine("\n=== 测试报告 ===");
            
            var successful = results.Count(r => r.Success);
            var failed = results.Count - successful;

            Console.WriteLine($"总测试数: {results.Count}");
            Console.WriteLine($"成功: {successful} ✅");
            Console.WriteLine($"失败: {failed} ❌");
            Console.WriteLine();

            Console.WriteLine("详细结果:");
            foreach (var result in results)
            {
                Console.WriteLine($"[{(result.Success ? "✅" : "❌")}] {result.FriendlyName}");
                Console.WriteLine($"    耗时: {result.Duration.TotalSeconds:F2}秒");
                if (!result.Success)
                {
                    Console.WriteLine($"    错误: {result.ErrorMessage}");
                }
                Console.WriteLine();
            }

            // 生成HTML报告
            GenerateHtmlReport(results);
        }

        private static void GenerateHtmlReport(List<TestResult> results)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='UTF-8'>");
            html.AppendLine("    <title>硬件编解码器测试报告</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("        .success { color: green; font-weight: bold; }");
            html.AppendLine("        .failure { color: red; font-weight: bold; }");
            html.AppendLine("        table { border-collapse: collapse; width: 100%; margin-top: 20px; }");
            html.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("        th { background-color: #f2f2f2; }");
            html.AppendLine("        .summary { background-color: #e9f7ef; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine($"<h1>硬件编解码器测试报告</h1>");
            html.AppendLine($"<p>生成时间: {DateTime.Now}</p>");
            
            var successful = results.Count(r => r.Success);
            var failed = results.Count - successful;
            
            html.AppendLine("<div class='summary'>");
            html.AppendLine($"<h2>测试摘要</h2>");
            html.AppendLine($"<p>总测试数: <strong>{results.Count}</strong></p>");
            html.AppendLine($"<p>成功: <span class='success'>{successful}</span></p>");
            html.AppendLine($"<p>失败: <span class='failure'>{failed}</span></p>");
            html.AppendLine("</div>");
            
            html.AppendLine("<h2>详细结果</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>编解码器</th><th>状态</th><th>耗时(秒)</th><th>错误信息</th></tr>");
            
            foreach (var result in results)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{result.FriendlyName}</td>");
                html.AppendLine($"<td class='{(result.Success ? "success" : "failure")}'>{(result.Success ? "✅ 成功" : "❌ 失败")}</td>");
                html.AppendLine($"<td>{result.Duration.TotalSeconds:F2}</td>");
                html.AppendLine($"<td>{(result.Success ? "-" : result.ErrorMessage)}</td>");
                html.AppendLine("</tr>");
            }
            
            html.AppendLine("</table>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            var reportPath = Path.Combine(OutputDirectory, $"test_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.WriteAllText(reportPath, html.ToString());
            Console.WriteLine($"HTML报告已生成: {reportPath}");
        }
    }

    class CodecInfo
    {
        public string CodecName { get; set; }
        public string FriendlyName { get; set; }
    }

    class TestResult
    {
        public string CodecName { get; set; }
        public string FriendlyName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}