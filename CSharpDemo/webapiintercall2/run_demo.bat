@echo off
echo Building solution...
dotnet build "c:\Users\menghl2\WorkSpace\Temp\Demo\CSharpDemo\webapiintercall2\MeshNetwork.sln"

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b %errorlevel%
)

echo Starting 4 nodes...

REM Define the executable path (adjust if needed based on build output)
set EXE_PATH="c:\Users\menghl2\WorkSpace\Temp\Demo\CSharpDemo\webapiintercall2\MeshNetwork.Node\bin\Debug\net461\MeshNetwork.Node.exe"

REM Start Node 1 (Port 9001, Peers: 9002, 9003, 9004)
start "Node 1 - Port 9001" %EXE_PATH% 9001 9002 9003 9004

REM Start Node 2 (Port 9002, Peers: 9001, 9003, 9004)
start "Node 2 - Port 9002" %EXE_PATH% 9002 9001 9003 9004

REM Start Node 3 (Port 9003, Peers: 9001, 9002, 9004)
start "Node 3 - Port 9003" %EXE_PATH% 9003 9001 9002 9004

REM Start Node 4 (Port 9004, Peers: 9001, 9002, 9003)
start "Node 4 - Port 9004" %EXE_PATH% 9004 9001 9002 9003

echo All nodes started.
