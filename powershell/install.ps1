<#
.CREATED BY:
    John Clark    Copyright 2023
.CREATED ON:
    01/24/2023
.Synopsis
   Install Rust Game Server System
.DESCRIPTION
   This is the first script to be run.
   Requires PSv3+
   File Name      : install.ps1
.LINK
    Script posted over:
    https://github.com/john-clark/rust-oxide-umod
.EXAMPLE
   PS C:\> install.ps1

#>

Set-Executionpolicy -Scope LocalMachine -ExecutionPolicy UnRestricted -Force

Write-Host "Checking Installation"
Write-Host "------------------------------------------------"

<#
-------------------------------------------------------------------------
                             Install Git
-------------------------------------------------------------------------
#>

IF(!(Test-Path "C:\Program Files\Git")) {
 Write-Host "Downloading Git" -ForegroundColor Yellow -BackgroundColor red
 Install-PackageProvider -Name NuGet -Force
 Install-Script install-git -Force
 install-git.ps1
} 
Write-Host "Git Installed"

<#
-------------------------------------------------------------------------
                              Install 7-zip
------------------------------------------------------------------------- 
#>

IF(!(Test-Path "C:\Program Files\7-Zip")) {
 Write-Host "Downloading 7zip" -ForegroundColor Yellow -BackgroundColor red
 $dlurl = 'https://7-zip.org/' + (Invoke-WebRequest -UseBasicParsing -Uri 'https://7-zip.org/' | Select-Object -ExpandProperty Links | Where-Object {($_.outerHTML -match 'Download')-and ($_.href -like "a/*") -and ($_.href -like "*-x64.exe")} | Select-Object -First 1 | Select-Object -ExpandProperty href)
 $installerPath = Join-Path $env:TEMP (Split-Path $dlurl -Leaf)
 Invoke-WebRequest $dlurl -OutFile $installerPath
 Start-Process -FilePath $installerPath -Args "/S" -Verb RunAs -Wait
 Remove-Item $installerPath 
} 
Write-Host "7-Zip Installed"

<#
-------------------------------------------------------------------------
                               Install OpenGL
-------------------------------------------------------------------------
#>

IF(!(Test-Path "C:\Windows\System32\opengl32.dll")) {
 Write-Host "No OpenGL found" -ForegroundColor Yellow -BackgroundColor red
 IF(!(Test-Path ".\mesa.7z")) {
   Write-Host "Downloading OpenGL" -ForegroundColor Yellow -BackgroundColor red
   Invoke-WebRequest -UseBasicParsing -uri "https://downloads.fdossena.com/geth.php?r=mesa64-latest" -OutFile .\mesa.7z
 } 
 Write-Host "opengl downloaded"
 & ${env:ProgramFiles}\7-zip\7z.exe x .\mesa.7z -y >$null
 Move-Item -Path ".\opengl32.dll" -Destination "C:\Windows\System32\opengl32.dll"
}
Write-Host "OpenGL Installed"

<#
-------------------------------------------------------------------------
                              Download Repo 
-------------------------------------------------------------------------
#>

IF(!(Test-Path "C:\rust-oxide-umod")) {
 Write-Host "Downloading Repo" -ForegroundColor Yellow -BackgroundColor red
 cd \
 #run installer in new window
 #Start-Process -Wait "Repo Download" /D "C:\" cmd.exe /K C:\Program Files\Git\bin\git.exe clone https://github.com/john-clark/rust-oxide-umod.git
 & 'C:\Program Files\Git\bin\git.exe' clone https://github.com/john-clark/rust-oxide-umod.git
}
cd \rust-oxide-umod
Write-Host "Repository cloned"

<#
-------------------------------------------------------------------------
                         Create Backup folder
-------------------------------------------------------------------------
#>

IF(!(Test-Path "C:\backups")) { New-Item -ItemType Directory -Path "c:\backups" }
 Write-Host "Backup folder created"

<#
-------------------------------------------------------------------------
                               Install Steam
-------------------------------------------------------------------------
#>

IF(!(Test-Path "C:\SteamCMD")) {
 Write-Host "Downloading SteamCMD" -ForegroundColor Yellow -BackgroundColor red
 #run installer in new window
 #Start-Process -Wait "Steam Installer" /D "C:\" cmd.exe /K C:\rust-oxide-umod\powershell\install-steamcmd.bat
 & C:\rust-oxide-umod\powershell\install-steamcmd.bat
}
Write-Host "Steam Installed"

<#
-------------------------------------------------------------------------
                              Install Umod
-------------------------------------------------------------------------
#>

#need a better test here
IF(!(Test-Path "C:\rust-oxide-umod\powershell\umod-devel.ps1")) {
 Write-Host "Downloading uMod" -ForegroundColor Yellow -BackgroundColor red
 Invoke-WebRequest -UseBasicParsing -uri "https://umod.io/umod-install.ps1" -Outfile "C:\rust-oxide-umod\powershell\umod-install.ps1"
 #install prereq
 Install-Package Microsoft.PowerShell.Native -Version 7.3.2
 #run installer in new window
 # some command here
 & C:\rust-oxide-umod\powershell\umod-install.ps1
 #Remove-Item C:\rust-oxide-umod\powershell\umod-install.ps1
}
Write-Host "uMod Install Complete - " -NoNewLine
Write-Host "Run again: " -NoNewLine -ForegroundColor Yellow -BackgroundColor Blue
Write-Host ".\umod-install.ps1" -ForegroundColor green

<#
-------------------------------------------------------------------------
                          Create/Edit Server CFG
-------------------------------------------------------------------------
#>

IF(!(Test-Path "C:\rust-oxide-umod\umod-server-vars.cfg")) {
 Write-Host "Copying Example server cfg" -ForegroundColor Yellow -BackgroundColor red
 Copy-Item -Path "C:\rust-oxide-umod\umod-server-vars.cfg.example" -Destination "C:\rust-oxide-umod\umod-server-vars.cfg"
 Notepad.exe C:\rust-oxide-umod\umod-server-vars.cfg | Out-Null
}
Write-Host "Server Config Found   - " -NoNewLine
Write-Host "Edit again:" -NoNewLine -ForegroundColor Yellow -BackgroundColor Blue
Write-Host " Notepad.exe .\umod-server-vars.cfg" -ForegroundColor Yellow

<#
-------------------------------------------------------------------------
                         Installer Complete message
-------------------------------------------------------------------------
#>

Write-Host "Install Complete      - " -NoNewLine
Write-Host "Run again: " -NoNewLine -ForegroundColor Yellow -BackgroundColor Blue
Write-Host ".\powershell\install.ps1" -ForegroundColor green

<#
-------------------------------------------------------------------------
                               fix paths
-------------------------------------------------------------------------
#>

$ENV:PATH = "$((Get-ItemProperty -Path 'Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Session Manager\Environment' -Name Path).Path);$((Get-ItemProperty HKCU:\Environment).PATH)"

<#
-------------------------------------------------------------------------
                              Next Step Message
-------------------------------------------------------------------------
#>

Write-Host "Ready to Run:" -NoNewLine -ForegroundColor White -BackgroundColor Green
Write-Host " & '.\umod-server-start.cmd'" -ForegroundColor green
