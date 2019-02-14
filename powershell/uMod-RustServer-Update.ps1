<#
.CREATED BY:
    John Clark    Copyright 2019 
.CREATED ON:
    01/11/2019
.SYNOPSIS
    Steam, Rust, uMod, and plugins updater.
.DESCRIPTION
    Looks for updates to Rust server, Oxide/uMod and the plugins.
    File Name      : uMod-Rust-Update.ps1
.LINK
    Script posted over:
    https://github.com/john-clark/rust-oxide-umod
.EXAMPLE
    Edit Variables below for your server
    Run script and it will update uMod


-------------------------------------------------------------------------
                          Edit these variables
-------------------------------------------------------------------------
#>

$rustdir = "C:\rust-oxide-umod"
$backupdir = "C:\backups"

<#
-------------------------------------------------------------------------
                           Don't edit past here
-------------------------------------------------------------------------
#>

#this needs to be switched touse  https://umod.org/games/rust.json
$oxideurl = "https://umod.org/games/rust/download"
$oxidezip = 'Oxide-Rust.zip'

#these are our placeholders
$backupfilename = $backupdir + '\rustserver-' + (get-date -Format yyyyMMdd-HH) + '.7z'
$steamupdate = $backupdir + '\steamupdate-' + (get-date -Format yyyyMMdd)
$oxideupdate = $backupdir + '\oxideupdate-' + (get-date -Format yyyyMMdd)
$plugsupdate = $backupdir + '\plugsupdate-' + (get-date -Format yyyyMMdd)
$umodlogfile = $backupdir + '\umod-rust-' + (get-date -Format yyyyMMdd) + '.log'

Add-Content $umodlogfile "============================================================" -encoding ASCII
Add-Content $umodlogfile "                 STARTING uMod Rust Update" -encoding ASCII
Add-Content $umodlogfile "============================================================" -encoding ASCII
Write-Host ''
Write-Host 'Starting uMod Updater...' -ForegroundColor white

<#
-------------------------------------------------------------------------
                           BACKUP SERVER FOLDER
-------------------------------------------------------------------------
#>

#compress-archive sucks with 2gb limit so we have to require 7zip
if (-not (test-path "$env:ProgramFiles\7-Zip\7z.exe")) {
    Add-Content $umodlogfile "DID NOT FIND 7ZIP" -encoding ASCII
    Write-Host '$env:ProgramFiles\7-Zip\7z.exe needed' -ForegroundColor red
    #
    # We will fix this with the auto download but will do it later
    #
    break
    }
Add-Content $umodlogfile "FOUND 7ZIP" -encoding ASCII
set-alias Seven-Zip "$env:ProgramFiles\7-Zip\7z.exe"

#make sure there is a backups dir if this is the first time run
if (-NOT (Test-Path $backupdir)) {
    Add-Content $umodlogfile "DID NOT FIND BACKUPS FOLDER MAKING ONE" -encoding ASCII
    New-Item -ItemType Directory -Force -Path $backupdir | Out-null
}
Add-Content $umodlogfile "FOUND BACKUP FOLDER" -encoding ASCII

#backup the game if there is not one already made in the last hour
Write-Host '[Backing up Game]' -ForegroundColor yellow
if (-NOT (Test-Path $backupfilename)) {
    Add-Content $umodlogfile "NO HOURLY BACKUP" -encoding ASCII
    Write-Host 'Compressing... please wait.' -ForegroundColor white
    $cmd = "seven-Zip a -t7z -m0=LZMA2:d64k:fb32 -ms=8m -mmt=30 -mx=1 -- `"$backupfilename`" `"$rustdir`""
    Add-Content $umodlogfile "RUNNING: $cmd"
    try {Invoke-Expression $cmd | Out-file $umodlogfile -append -encoding ASCII }
    catch {
        Add-Content $umodlogfile "ERROR MAKING 7ZIP" -encoding ASCII
        break }
    Write-Host 'Completed Backup' -ForegroundColor green
} else {
    Add-Content $umodlogfile "FOUND HOURLY BACKUP" -encoding ASCII
    Write-Host 'Found Hourly Backup' -ForegroundColor green
}

Add-Content $umodlogfile "------------------------------------------------------------" -encoding ASCII
Write-Host ''

<#
-------------------------------------------------------------------------
                      UPDATE STEAM AND RUST SERVER
-------------------------------------------------------------------------
#>

#update steam if not updated today
Write-Host '[Updating Game]' -ForegroundColor yellow
if (-NOT (Test-Path $steamupdate)) {
    Add-Content $umodlogfile "NO STEAM UPDATE TODAY" -encoding ASCII
    Write-Host 'Updating... please wait.' -ForegroundColor white
    $cmd = "C:\steamcmd\steamcmd.exe +login anonymous +force_install_dir $rustdir +app_update 258550 validate +quit"
    Add-Content $umodlogfile "RUNNING: $cmd"
    try { Invoke-Expression $cmd | Out-file $umodlogfile -append -encoding ASCII }
    catch {
        Add-Content $umodlogfile "ERROR WITH STEAM UPDATE" -encoding ASCII
        break }
    New-Item -Path $steamupdate -ItemType File | Out-null
    Write-Host 'Completed Steam Update' -ForegroundColor green
} else {
    Add-Content $umodlogfile "STEAM UPDATED TODAY" -encoding ASCII
    Write-Host 'Steam and Rust updated today' -ForegroundColor green
}

Add-Content $umodlogfile "------------------------------------------------------------" -encoding ASCII
Write-Host ''

<#
-------------------------------------------------------------------------
                             UPDATE uMod
-------------------------------------------------------------------------
#>

#update uMod if not updated today
Write-Host '[Updating uMod]' -ForegroundColor yellow

#
# Fix this to download the zip once a day
#

if (-NOT (Test-Path ($backupdir + '\' + $oxidezip))) {
    Add-Content $umodlogfile "DID NOT FIND uMod ZIP" -encoding ASCII 
    Write-Host 'Downloading...' -ForegroundColor white
    Add-Content $umodlogfile "Downloading: $oxideurl TO: $($backupdir + '\' + $oxidezip)"
    try { (New-Object System.Net.WebClient).DownloadFile($oxideurl, ($backupdir + '\' + $oxidezip)) }
    catch {
        Add-Content $umodlogfile "ERROR DOWNLOADING uMod ZIP" -encoding ASCII
        break }
} else {
    Add-Content $umodlogfile "FOUND uMod ZIP SKIPPING DOWNLOAD" -encoding ASCII
    Write-Host 'Oxide updated today'  -ForegroundColor green
}
#extract oxide no matter what
Write-Host 'Extracting...' -ForegroundColor white
$cmd = "seven-Zip x -aoa -y `"$backupdir\$oxidezip`" -o`"$rustdir\`""
Add-Content $umodlogfile "RUNNING: $cmd"
try { Invoke-Expression $cmd | Out-file $umodlogfile -append -encoding ASCII }
catch {
    Add-Content $umodlogfile "ERROR EXTRACKING uMod ZIP" -encoding ASCII
    break }
New-Item -Path $oxideupdate -ItemType File -Force | Out-null
Write-Host 'Completed uMod installation' -ForegroundColor green

Add-Content $umodlogfile "------------------------------------------------------------" -encoding ASCII
Add-Content $umodlogfile " " -encoding ASCII

Write-Host ''
Write-Host 'Finished uMod Updater...' -ForegroundColor white