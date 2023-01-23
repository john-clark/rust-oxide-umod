@echo off
SETLOCAL
:start
set _date=%date:~4%
set _date=%_date:/=-%
IF NOT EXIST umod-server-vars.cfg goto :end
IF EXIST umod-server-vars.cfg FOR /F "delims=" %%A IN (umod-server-vars.cfg) DO SET "%%A"
echo Updating Server...
powershell -ExecutionPolicy Bypass -File "c:\rust-oxide-umod\powershell\umod-rustserver-update.ps1"
echo Updating uMod Plugins...
powershell -ExecutionPolicy Bypass -File "c:\rust-oxide-umod\powershell\umod-plugin-update.ps1"
echo Starting Server...
echo This may take a while...
start /high /wait RustDedicated.exe -batchmode -nographics -LogFile "c:\backups\serverlog-%_date%.log" ^
+server.official %official% ^
+server.hostname %host% ^
+server.description %descr% +server.headerimage %image% +server.url %url% ^
+server.port %sport% ^
+rcon.port %rport% +rcon.web %rweb% +rcon.password %rpass% ^
+server.identity "Procedural_Map-seed1-size4000" ^
+server.level %level% +server.seed %seed% +server.worldsize %size% ^
+server.maxplayers %maxplayers% ^
+server.radiation %radiation% +server.stability %stability% +decay.upkeep %upkeep%
echo Restarting Server...
timeout /t 10
goto start
:end
