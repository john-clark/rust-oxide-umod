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

$rustdir = "C:\rustserver"
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
    $cmd = "C:\steam\steamcmd.exe +login anonymous +force_install_dir $rustdir +app_update 258550 validate +quit"
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
Write-Host ''

<#
-------------------------------------------------------------------------
                             UPDATE PLUGINS
-------------------------------------------------------------------------
#>
$pluginsdir = $rustdir+"\oxide\plugins\"
$dlstempdir = $backupdir+"\plugdltemp\"
$pluginsconfig = $rustdir+"\umod-plugins.cfg"

#get the list of plugins installed
try {
    Add-Content $umodlogfile "SEARCHING FOR PLUGINS" -encoding ASCII
    $installedplugins = Get-ChildItem $pluginsdir -Include *.cs, *.lua, *.js -Name
}
catch {
    Add-Content $umodlogfile "ERROR FINDING PLUGINS" -encoding ASCII
    #create empty array
    $installedplugins = ""
    # we probably should have one..
    # we will download one in the future
}

# We may have plugins that are not umod or umod plugins we modified
# So we only want to update certain ones let use a cfg file

#lets make sure we have a config
if (-NOT (Test-Path $pluginsconfig)) {
    Add-Content $umodlogfile "PLUGINS CONFIG NOT FOUND MAKING NEW" -encoding ASCII
    Write-Host 'No plugins config...' -ForegroundColor red
    New-Item -Path $pluginsconfig -ItemType File | Out-null
    "$installedplugins=enabled" | Out-File $pluginsconfig
}
#load the config into an array
Add-Content $umodlogfile "LOADING PLUGINS CONFIG" -encoding ASCII
$pluginsarray = [IO.File]::ReadAllLines($pluginsconfig) | ConvertFrom-StringData
Write-Host 'Plugins cataloged...' -ForegroundColor white

#temp folder to download plugins
if (-NOT (Test-Path $dlstempdir)) {
    Add-Content $umodlogfile "NO PLUGINS TEMP FOLDER" -encoding ASCII 
} else {
    #do we want to remove it? for now I guess...
    Add-Content $umodlogfile "PLUGINS TEMP FOLDER FOUND REMOVING" -encoding ASCII 
    Remove-Item $dlstempdir -Force -Recurse
}
Add-Content $umodlogfile "CREATING NEW PLUGINS TEMP FOLDER" -encoding ASCII 
New-Item -Path $dlstempdir -ItemType Directory | Out-Null



#update plugins if not updated today
Write-Host '[Updating Plugins]' -ForegroundColor yellow
if (-NOT (Test-Path $plugsupdate)) {
    Add-Content $umodlogfile "PLUGINS NOT UPDATED TODAY" -encoding ASCII 

    #loop through each plugin
    foreach ($plugin in $plugins) {
        Add-Content $umodlogfile "----------------------" -encoding ASCII
        Add-Content $umodlogfile "WORKING ON PLUGIN: $plugin" -encoding ASCII
        Write-Host 'Downloading:' $plugin.name -ForegroundColor white

        #parse file for info
        $temp = Get-Content($pluginsdir +$plugin.name) | where-object { $_.Contains("Info") }
        $t,$pluginname,$t,$pluginauthor,$t,$pluginversion,$t=$temp.split('"')
        #lots of plugins are missing the description
        try {
            $temp = Get-Content($pluginsdir +$plugin.name) | where-object { $_.Contains("Description") }
            $t,$plugindescription,$t=$temp.split('"')
        } catch {
            $newplugindescription='missing'
        }
        #log the plugin info
        Add-Content $umodlogfile " CURRENT PLUGIN: $pluginname" -encoding ASCII 
        Add-Content $umodlogfile " CURRENT AUTHOR: $pluginauthor" -encoding ASCII 
        Add-Content $umodlogfile " CURRENT VERSION: $pluginversion" -encoding ASCII 
        Add-Content $umodlogfile " CURRENT DESCRIPTION: $plugindescription" -encoding ASCII 

        #download the plugin
        $PluginURL = 'https://umod.org/plugins/' + $plugin.name
        Add-Content $umodlogfile "DOWNLOADING: $PluginURL" -encoding ASCII 
        Add-Content $umodlogfile "TO: $($dlstempdir + $plugin.name)" -encoding ASCII
        try { (New-Object System.Net.WebClient).DownloadFile($PluginURL, ($dlstempdir + $plugin.name)) | Out-file $umodlogfile -append -encoding ASCII }
        catch { Add-Content $umodlogfile "ERROR DOWNLOADING" -encoding ASCII }

        #check to see if it downloaded right
        if (Test-Path ($dlstempdir + $plugin.name)) {
            #make some hash
            $currentpluginhash = (Get-FileHash ($pluginsdir +$plugin.name)).hash
            $downloadpluginhash = (Get-FileHash ($dlstempdir +$plugin.name)).hash
            #compare the hash
            if ($currentpluginhash -ne $downloadpluginhash) {
                #Files are different
                Add-Content $umodlogfile "PLUGINS HASH DOES NOT MATCH" -encoding ASCII 
                Write-Host 'Files are different...' -ForegroundColor red

                #get file details about the download
                $temp = Get-Content($dlstempdir +$plugin.name) | where-object { $_.Contains("Info") }
                $t,$newpluginname,$t,$newpluginauthor,$t,$newpluginversion,$t=$temp.split('"')
                #lots of plugins dont have a description
                try {
                    $temp = Get-Content($dlstempdir +$plugin.name) | where-object { $_.Contains("Description") }
                    $t,$newplugindescription,$t=$temp.split('"')
                } catch {
                    $newplugindescription='missing'
                }

                #Lets see if they changed the version
                if ($pluginversion -eq $newpluginversion) {
                    #go figure they changed the file without updating the version
                    Add-Content $umodlogfile "PLUGIN VERSIONS MATCH BUT HASH DO NOT" -encoding ASCII
                    Write-Host 'Versions match but hash does not...' -ForegroundColor red
                    $currentplugin = Get-Content($pluginsdir +$plugin.name)
                    $downloadedplugin = Get-Content($dlstempdir +$plugin.name)
                    Compare-Object $currentplugin $downloadedplugin | Out-file $umodlogfile -append -encoding ASCII
                   
                    #
                    # Lets do something here - probably updated so we could go ahead and replace
                    #
                   
                } else {
                    #oh look a different version let make a note
                    Add-Content $umodlogfile " NEW PLUGIN: $newpluginname" -encoding ASCII 
                    Add-Content $umodlogfile " NEW AUTHOR: $newpluginauthor" -encoding ASCII 
                    Add-Content $umodlogfile " NEW VERSION: $newpluginversion" -encoding ASCII 
                    Add-Content $umodlogfile " NEW DESCRIPTION: $newplugindescription" -encoding ASCII 
                    Write-Host 'New Plugin version...' -ForegroundColor green
                   
                    #
                    # Lets do something here - probably updated so we could go ahead and replace
                    #
                }
           
            } else {
                Add-Content $umodlogfile "PLUGINS ARE THE SAME REMOVING DOWNLOAD" -encoding ASCII
                Write-Host 'Files are the same...' -ForegroundColor green
                Remove-Item ($dlstempdir + $plugin.name) -force
            }
        } else {
            Add-Content $umodlogfile "FILE NOT FOUND IN DOWNLOAD DIR" -encoding ASCII
            Add-Content $umodlogfile "DID NOT DOWNLOAD PLUGIN" -encoding ASCII
            Write-Host 'Error Downloading...' -ForegroundColor red
           
            #
            # Write to a file to never try and download again
            #
           
        }
        start-sleep 5
    }
     Add-Content $umodlogfile "ALL PLUGINS CHECKED FOR UPDATES" -encoding ASCII
     Write-Host 'All Plugins Checked...' -ForegroundColor green
     New-Item -Path $plugsupdate -ItemType File -Force | Out-null
} else {
    Add-Content $umodlogfile "PLUGINS UPDATED ALREADY THIS HOUR" -encoding ASCII
    Write-Host 'Plugins updated already' -ForegroundColor green
}

Add-Content $umodlogfile "------------------------------------------------------------" -encoding ASCII
Add-Content $umodlogfile " " -encoding ASCII

Write-Host ''
Write-Host 'Finished uMod Updater...' -ForegroundColor white