@echo off

set FXC="C:\Program Files (x86)\Windows Kits\8.1\bin\x86\fxc.exe"
set SRC=ShaderSources\
set DEST=CompiledShaders\
set N=9

FOR /l %%i in (1,1,%N%) DO %FXC% /T ps_3_0 %SRC%ps%%i.fx /Fo %DEST%ps%%i.fxc
