#!/bin/bash
# Builds the app into .run and starts it from there (so the source tree can be rebuilt while it runs).
cd "$(dirname "$0")"
for pid in $(netstat -ano | grep -E ":5100 .*LISTENING" | awk "{print $5}" | sort -u); do taskkill //F //PID $pid >/dev/null 2>&1; done; sleep 2
MSYS_NO_PATHCONV=1 dotnet publish SatPlatform/SatPlatform.csproj -c Debug -o .run -p:UseAppHost=false 2>&1 | grep -E "error|Warn|publish" | head -5
cd .run && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5100 nohup dotnet SatPlatform.dll > /c/Users/d.odilov/AppData/Local/Temp/sat-run.log 2>&1 &
sleep 15; curl -s -o /dev/null -w "healthz %{http_code}\n" http://localhost:5100/healthz
