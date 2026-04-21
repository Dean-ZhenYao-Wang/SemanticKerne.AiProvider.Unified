@echo off
echo ========================================
echo AI 聊天服务压力测试工具
echo TTFT（首字延迟）统计版
echo ========================================
echo.
echo 正在编译项目...
dotnet build StressTest/StressTest.csproj
if %errorlevel% neq 0 (
    echo 编译失败！
    pause
    exit /b 1
)
echo.
echo 编译成功！
echo.
echo 正在启动压力测试...
echo.
cd StressTest
dotnet run
pause
