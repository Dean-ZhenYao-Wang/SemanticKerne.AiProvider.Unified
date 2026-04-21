# AI 聊天服务压力测试工具 - TTFT 统计版

这是一个专业的 AI 聊天服务压力测试工具，专门用于统计并发速率和 TTFT（Time to First Token，首字延迟）。

## 功能特性

### 核心功能
- ✅ **TTFT（首字延迟）统计** - 精确测量从请求发送到收到第一个字符的时间
- ✅ **并发用户模拟** - 支持多个并发用户同时测试
- ✅ **SSE 流式响应测试** - 完整支持 Server-Sent Events
- ✅ **实时性能指标收集** - 详细的性能数据统计
- ✅ **CSV 日志导出** - 将所有测试数据导出为 CSV 格式
- ✅ **百分位统计** - P50、P95、P99 TTFT 统计

### 性能指标

| 指标 | 说明 |
|------|------|
| **TTFT（首字延迟）** | 从发送请求到收到第一个字符的时间（毫秒） |
| **TTFT P50** | 50% 的请求 TTFT 低于此值 |
| **TTFT P95** | 95% 的请求 TTFT 低于此值 |
| **TTFT P99** | 99% 的请求 TTFT 低于此值 |
| 平均响应时间 | 所有请求的平均响应时间（毫秒） |
| 最小/最大响应时间 | 最快和最慢的请求响应时间 |
| 吞吐量 | 每秒处理的请求数 |
| 成功率 | 成功请求占总请求的百分比 |
| 总字符数 | 接收到的总字符数 |
| 平均字符速率 | 每秒接收的字符数 |

## 项目结构

```
StressTest/
├── Models/
│   ├── StressTestOptions.cs      # 测试配置选项
│   ├── RequestMetrics.cs          # 单个请求的指标
│   └── PerformanceReport.cs       # 性能报告
├── Services/
│   ├── SseClient.cs               # SSE 客户端（支持流式响应）
│   └── StressTestService.cs       # 压力测试服务
├── appsettings.json               # 配置文件
├── Program.cs                     # 程序入口
└── README.md                      # 本文档
```

## 配置说明

编辑 `appsettings.json` 文件来配置测试参数：

```json
{
  "StressTest": {
    "BaseUrl": "http://localhost:5000",           // 目标服务地址
    "ConcurrentUsers": 10,                         // 并发用户数
    "RequestsPerUser": 5,                          // 每用户请求数
    "TestDurationMinutes": 5,                      // 测试时长（分钟）
    "EnableCsvLogging": true,                       // 是否启用 CSV 日志
    "CsvOutputPath": "stresstest_results.csv",      // CSV 输出路径
    "TestMessages": [                              // 测试消息列表
      "你好",
      "请介绍一下你自己",
      "什么是人工智能？",
      "如何使用 C# 编写 REST API？",
      "请解释一下什么是微服务架构"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "StressTest.Services": "Debug"
    }
  }
}
```

## 使用方法

### 方式 1：使用批处理脚本（推荐）

```bash
run-stresstest.bat
```

### 方式 2：手动运行

```bash
cd StressTest
dotnet build
dotnet run
```

### 方式 3：指定配置文件

```bash
cd StressTest
dotnet run --configuration Release
```

## 测试流程

1. **启动程序** - 显示测试配置信息
2. **按任意键开始** - 开始压力测试
3. **并发执行** - 多个用户同时发送请求
4. **实时日志** - 显示每个请求的详细信息
5. **测试完成** - 生成性能报告和 CSV 文件
6. **查看结果** - 控制台显示汇总报告

## 日志说明

### 实时日志示例

```
[info]: 开始压力测试
[info]: 并发用户数: 10
[info]: 每用户请求数: 5
[info]: 测试时长: 5 分钟
[info]: 总请求数: 50
[info]: [用户 1] 开始测试，SessionId: abc123
[info]: [请求 #1] 用户 1 发送第 1 个请求: 你好
[info]: [请求 #1] 首字延迟 (TTFT): 234.56 ms
[info]: [请求 #1] 完成 - 总时长: 1234.56 ms, 字符数: 150, TTFT: 234.56 ms, 吞吐量: 121.45 字符/秒
```

### CSV 日志字段

导出的 CSV 文件包含以下字段：

| 字段 | 说明 |
|------|------|
| RequestId | 请求唯一标识 |
| UserId | 用户 ID |
| RequestNumber | 该用户的第几个请求 |
| StartTime | 请求开始时间 |
| FirstTokenTime | 首个字符到达时间 |
| EndTime | 请求结束时间 |
| IsSuccess | 是否成功 |
| StatusCode | HTTP 状态码 |
| ErrorMessage | 错误信息 |
| ChunksReceived | 收到的数据块数 |
| TotalCharacters | 总字符数 |
| ThinkingCharacters | 思考内容字符数 |
| ContentCharacters | 正常内容字符数 |
| TotalDurationMs | 总时长（毫秒） |
| TtftMs | 首字延迟（毫秒） |
| CharactersPerSecond | 字符速率（字符/秒） |

## 测试场景

### 场景 1：轻量级测试

```json
{
  "ConcurrentUsers": 5,
  "RequestsPerUser": 3,
  "TestDurationMinutes": 2
}
```

适用于快速验证基本功能和 TTFT 指标。

### 场景 2：中等负载测试

```json
{
  "ConcurrentUsers": 20,
  "RequestsPerUser": 10,
  "TestDurationMinutes": 5
}
```

适用于测试中等负载下的性能表现和 TTFT 稳定性。

### 场景 3：高强度压力测试

```json
{
  "ConcurrentUsers": 50,
  "RequestsPerUser": 20,
  "TestDurationMinutes": 10
}
```

适用于测试系统极限性能、并发处理能力和 TTFT 在高负载下的表现。

## 性能报告示例

```
====================================================================================================
压力测试结果汇总
====================================================================================================
测试时长: 45.23 秒
总请求数: 50
成功请求: 48
失败请求: 2
成功率: 96.00%
吞吐量: 1.11 请求/秒

响应时间统计:
  平均响应时间: 1234.56 ms
  最小响应时间: 890.12 ms
  最大响应时间: 2345.67 ms

TTFT（首字延迟）统计:
  平均 TTFT: 234.56 ms
  最小 TTFT: 123.45 ms
  最大 TTFT: 456.78 ms
  TTFT P50: 234.56 ms
  TTFT P95: 345.67 ms
  TTFT P99: 412.34 ms

内容统计:
  总字符数: 12500
  平均字符速率: 276.35 字符/秒
====================================================================================================
```

## TTFT 指标解读

### 什么是 TTFT？

TTFT（Time to First Token）是衡量 AI 服务响应速度的重要指标，表示从发送请求到收到第一个响应字符的时间。

### TTFT 的重要性

1. **用户体验** - TTFT 越低，用户感觉响应越快
2. **系统性能** - TTFT 反映了系统的初始处理能力
3. **并发影响** - 高并发下 TTFT 可能会增加
4. **优化目标** - 降低 TTFT 是性能优化的关键目标

### TTFT 百分位

- **P50（中位数）**：50% 的请求 TTFT 低于此值
- **P95**：95% 的请求 TTFT 低于此值（通常用于 SLA）
- **P99**：99% 的请求 TTFT 低于此值（极端情况）

### TTFT 优化建议

1. **减少网络延迟** - 使用 CDN 或就近部署
2. **优化模型加载** - 预加载模型到内存
3. **减少并发竞争** - 合理设置并发限制
4. **使用更快的模型** - 选择响应速度更快的模型
5. **优化提示词** - 简化提示词可以加快响应

## 注意事项

1. **确保目标服务已启动**：运行测试前，请确保 AI 聊天服务正在运行
2. **合理设置并发数**：根据服务器性能合理设置并发用户数，避免服务器崩溃
3. **监控服务器资源**：测试时注意监控服务器的 CPU、内存和网络使用情况
4. **取消测试**：按 Ctrl+C 可以随时取消正在运行的测试
5. **网络延迟影响**：测试结果会受到网络延迟的影响，建议在同一网络环境下测试
6. **CSV 文件位置**：CSV 文件会生成在 StressTest/bin/Debug/net8.0/ 目录下

## 故障排除

### 问题：连接失败

**原因**：目标服务未启动或地址错误

**解决**：
1. 检查 `appsettings.json` 中的 `BaseUrl` 是否正确
2. 确保目标服务正在运行
3. 检查防火墙设置

### 问题：大量请求失败

**原因**：服务器负载过高或配置不当

**解决**：
1. 减少并发用户数
2. 增加测试时长
3. 检查服务器日志
4. 检查 AI 服务的 API 限制

### 问题：TTFT 过高

**原因**：网络延迟、模型加载慢、并发竞争等

**解决**：
1. 检查网络连接
2. 优化服务器配置
3. 使用更快的 AI 模型
4. 减少并发用户数

### 问题：CSV 文件未生成

**原因**：权限问题或路径错误

**解决**：
1. 检查 `CsvOutputPath` 配置
2. 确保有写入权限
3. 检查磁盘空间

## 性能基准参考

### 优秀的 TTFT 表现

| 场景 | TTFT P50 | TTFT P95 | 评价 |
|------|-----------|-----------|------|
| 本地测试 | < 100ms | < 200ms | 优秀 |
| 同城网络 | 100-200ms | 200-300ms | 良好 |
| 跨城网络 | 200-500ms | 300-600ms | 可接受 |
| 跨国网络 | 500-1000ms | 600-1500ms | 需优化 |

## 技术栈

- .NET 8.0
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Logging
- CsvHelper（CSV 导出）
- System.Text.Json（JSON 解析）

## 许可证

本项目遵循主项目的许可证。
