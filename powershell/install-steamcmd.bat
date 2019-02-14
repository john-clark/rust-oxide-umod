@echo off
set STEAM_CMD_LOCATION="C:\SteamCMD"
set STEAM_USERNAME="anonymous"

mkdir "%STEAM_CMD_LOCATION%"
cd "%STEAM_CMD_LOCATION%"
powershell.exe -Command "(New-Object Net.WebClient).DownloadFile(' https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip', 'steamcmd.zip')"
powershell.exe -NoP -NonI -Command "Expand-Archive '.\steamcmd.zip' '.'"
steamcmd.exe +login "%STEAM_USERNAME%" +quit
