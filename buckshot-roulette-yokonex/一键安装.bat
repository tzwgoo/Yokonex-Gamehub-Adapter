@echo off
chcp 65001 >nul
title 恶魔轮盘 GameHub 联动安装
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
if errorlevel 1 (
  echo.
  echo 安装失败，请查看上方提示。
  pause
  exit /b 1
)
echo.
echo 安装完成，窗口将在 3 秒后关闭。
timeout /t 3 /nobreak >nul
