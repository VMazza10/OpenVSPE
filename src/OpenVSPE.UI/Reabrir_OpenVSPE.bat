@echo off
chcp 65001 >nul
title Reabrir OpenVSPE

rem Fecha tudo que for OpenVSPE (tela de qualquer versão + motor) para nunca ficar com duas instâncias
taskkill /F /IM "OpenVSPE*" >nul 2>&1

rem Dá tempo do Windows liberar as portas COM/TCP antes de abrir de novo
ping -n 3 127.0.0.1 >nul

rem Mesmo critério do instalador: a OpenVSPE*.exe da pasta ui (ex.: OpenVSPE_V1.1.7.exe)
rem O instalador é 32 bits, então no Windows 64 bits ele fica em Program Files (x86)
set "EXE="
for %%F in ("%ProgramFiles(x86)%\OpenVSPE\ui\OpenVSPE*.exe") do set "EXE=%%~fF" & set "DIR=%%~dpF"
if not defined EXE for %%F in ("%ProgramFiles%\OpenVSPE\ui\OpenVSPE*.exe") do set "EXE=%%~fF" & set "DIR=%%~dpF"

if not defined EXE (
    echo Não encontrei o OpenVSPE instalado em Arquivos de Programas\OpenVSPE\ui.
    pause
    exit /b 1
)

rem A tela sobe o motor sozinha e fica na bandeja do relógio
start "" /d "%DIR%" "%EXE%"
