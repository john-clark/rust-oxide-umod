#
# Install Git
#
IF(!(Test-Path "C:\Program Files\Git")) {
 Install-PackageProvider -Name NuGet -Force
 Install-Script install-git -Force
 Set-Executionpolicy -Scope CurrentUser -ExecutionPolicy UnRestricted -Force
 install-git.ps1
} 
Write-Host "Git Installed"
#
# Install 7-zip
#
IF(!(Test-Path "C:\Program Files\7-Zip")) {
 $dlurl = 'https://7-zip.org/' + (Invoke-WebRequest -UseBasicParsing -Uri 'https://7-zip.org/' | Select-Object -ExpandProperty Links | Where-Object {($_.outerHTML -match 'Download')-and ($_.href -like "a/*") -and ($_.href -like "*-x64.exe")} | Select-Object -First 1 | Select-Object -ExpandProperty href)
 $installerPath = Join-Path $env:TEMP (Split-Path $dlurl -Leaf)
 Invoke-WebRequest $dlurl -OutFile $installerPath
 Start-Process -FilePath $installerPath -Args "/S" -Verb RunAs -Wait
 Remove-Item $installerPath 
} 
Write-Host "7-Zip Installed"
#
# Install OpenGL
# 
IF(!(Test-Path "C:\Windows\System32\opengl32.dll")) {
 IF(!(Test-Path ".\mesa.7z")) {
   Invoke-WebRequest -UseBasicParsing -uri "https://downloads.fdossena.com/geth.php?r=mesa64-latest" -OutFile .\mesa.7z
 } 
 Write-Host "opengl downloaded"
 & ${env:ProgramFiles}\7-zip\7z.exe x .\mesa.7z -y >$null
 Move-Item -Path ".\opengl32.dll" -Destination "C:\Windows\System32\opengl32.dll"
}
Write-Host "OpenGL Installed"
#
#fix paths
#
$ENV:PATH = "$((Get-ItemProperty -Path 'Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Session Manager\Environment' -Name Path).Path);$((Get-ItemProperty HKCU:\Environment).PATH)"
cd \
#
# Download Repo 
#
IF(!(Test-Path "C:\rust-oxide-umod")) {
& 'C:\Program Files\Git\bin\git.exe' clone https://github.com/john-clark/rust-oxide-umod.git
}
Write-Host "Repository cloned"
#
# Install Steam
#
IF(!(Test-Path "C:\SteamCMD")) {
& '\rust-oxide-umod\powershell\install-steamcmd.bat'
}
Write-Host "Steam Installed"
#
# Install Umod
#
Invoke-WebRequest -UseBasicParsing -uri "https://umod.io/umod-install.ps1 -Outfile umod-install.ps1
./umod-install.ps1
Write-Host "uMod Installed"
#
# Create/Edit Server CFG
#
IF(!(Test-Path "C:\backups")) { New-Item -ItemType Directory -Path "c:\backups" }
Write-Host "Backup folder created"
IF(!(Test-Path "C:\rust-oxide-umod\umod-server-vars.cfg")) {
 Copy-Item -Path "C:\rust-oxide-umod\umod-server-vars.cfg.example" -Destination "C:\rust-oxide-umod\umod-server-vars.cfg"
 Notepad.exe C:\rust-oxide-umod\umod-server-vars.cfg | Out-Null
}
cd rust-oxide-umod
Write-Host "Server Config Found - " -NoNewLine
Write-Host "Edit again:" -NoNewLine -ForegroundColor Yellow -BackgroundColor Blue
Write-Host " Notepad.exe .\umod-server-vars.cfg" -ForegroundColor Yellow
#
# Installer Complete messgae
#
Write-Host "Run the installer again: " -NoNewLine
Write-Host ".\powershell\install.ps1" -ForegroundColor green
#
# Next Step Message
#
Write-Host "Ready to Run:" -NoNewLine -ForegroundColor White -BackgroundColor Green
Write-Host " & '.\umod-server-start.cmd'" -ForegroundColor green
