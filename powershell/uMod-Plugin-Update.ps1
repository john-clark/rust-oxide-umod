<#
.SYNOPSIS
    uMod plugin updater.
.DESCRIPTION
    Looks for updates to plugins found in c:/rust-oxide-umod/oxide/plugins folder.
.NOTES
    File Name      : uMod-Plugin-Update.ps1
    Author         : John Clark (johnrclark3@gmail.com)
    Prerequisite   : PowerShell something.
    Copyright 2019 - John Clark/skyman
.LINK
    Script posted over:
    https://github.com/john-clark/rust-oxide-umod
.EXAMPLE
    Run script and it will update your plugins
#>

$srvpluginsdir = "C:\rust-oxide-umod\oxide\plugins"
$gitpluginsdir = "C:\rust-oxide-umod\oxide\plugins"
$BackupPluginsDir = "C:\backups\pluginsupdated\" + (get-date -Format yyyyMMdd-HH)

<#
Don't edit past here
-------------------------------------------------------------------------
#>
Write-Host 'Checking Paths...' -ForegroundColor Yellow
if (!(Test-Path $srvpluginsdir)) {
    Write-Host '$srvpluginsdir folder not found. Oxide installed?' -ForegroundColor red
    break
}
if (!(Test-Path $gitpluginsdir)) {
    Write-Host '$gitpluginsdir folder not found. No git repo downloaded?' -ForegroundColor red
    break
}
if (!(Test-Path $BackupPluginsDir)) {
    Write-Host "$BackupPluginsDir Created"
    New-Item -ItemType Directory -Force -path $BackupPluginsDir | out-null
} else {
	Write-Host "$BackupPluginsDir already updated this hour" -ForegroundColor red
	break
}
Write-Host 'Paths OK' -ForegroundColor green

Write-Host 'Plugins to be updated...' -ForegroundColor Yellow
$plugins = compare-object (gci "$srvpluginsdir") (gci -recurse "$gitpluginsdir") -ExcludeDifferent -IncludeEqual -Property name -passthru | sort-object
$replaceall = 'false'

foreach ($plugin in $plugins) {
	Write-Host -NoNewline "Checking file: $($plugin.name.trim('.cs'))"
   	
	$mismatch = ''
	$srvplugincontent = ''
	$gitplugincontent = ''
   
    $srvplugin = $(gci -path $srvpluginsdir -recurse -include $plugin.name)
    $gitplugin = $(gci -path $gitpluginsdir -recurse -include $plugin.name)
	
	$srvpluginhash = (Get-FileHash ($srvplugin)).hash
	$gitpluginhash = (Get-FileHash ($gitplugin)).hash
	
	:badHash while ($srvpluginhash -ne $gitpluginhash) {
        Write-Host ' - Different' -ForegroundColor red
		write-host "srvhash: $srvpluginhash"
		write-host "githash: $gitpluginhash"
		$srvplugininfo = @(Get-Content($srvplugin) | Where-Object { $_.Contains("[Info(") }).split(",").replace(' ','').replace('"','').replace('[Info(','').replace(')]','')
		#$srvplugincontent = $(get-content $srvplugin) | ForEach-Object { $_.Trim() }
		#$srvplugincontent = gc $srvplugin
		$srvpluginname = $srvplugininfo[0]
		$srvpluginauthor = $srvplugininfo[1]
		$srvpluginversion =$srvplugininfo[2]
		
		$gitplugininfo = @(Get-Content($gitplugin) | Where-Object { $_.Contains("[Info(") }).split(",").replace(' ','').replace('"','').replace('[Info(','').replace(')]','')
		#$gitplugincontent = $(get-content $gitplugin) | ForEach-Object { $_.Trim() }
		#$gitplugincontent = gc $gitplugin
		$gitpluginname = $gitplugininfo[0]
		$gitpluginauthor = $gitplugininfo[1]
		$gitpluginversion =$gitplugininfo[2]
		
		Write-Host -NoNewline " USE - Author: "
		Write-Host -NoNewline "$srvpluginauthor" -ForegroundColor yellow
		Write-Host -NoNewline "   Version: "
		Write-Host -NoNewline "$srvpluginversion" -ForegroundColor yellow
		write-host ''
		
		$mismatch = 'hash'
		
		if ($gitpluginname -ne $srvpluginname) {
			Write-Host -NoNewline "SOMETHING BROKE $srvpluginname not $gitpluginname" -ForegroundColor red 
			Write-Host -NoNewline "EXITING SO YOU CAN TAKE A LOOK" -ForegroundColor red 
			break
		}

		Write-Host -NoNewline " NEW - Author: "
		if ($gitpluginauthor -eq $srvpluginauthor) {
		Write-Host -NoNewline "$gitpluginauthor" -ForegroundColor green } else {
		Write-Host -NoNewline "$gitpluginauthor" -ForegroundColor red 
		$mismatch = 'author'
		}
		
		Write-host -NoNewline "   Version: "		
		if ($gitpluginversion -eq $srvpluginversion) {
		Write-Host -NoNewline "$gitpluginversion" -ForegroundColor green } else {
		Write-Host -NoNewline "$gitpluginversion" -ForegroundColor red
		$mismatch = 'version'
		}
		write-host ''

		if ($mismatch) {
			Write-Host "$mismatch mis-match..." -ForegroundColor red
			if ($replaceall -eq 'true') {
				Move-Item $srvplugin "$BackupPluginsDir\$($plugin.name)"
				Copy-Item $gitplugin "$srvplugin"
				$srvpluginhash = (Get-FileHash ($srvplugin)).hash
				write-host -NoNewline "FIXING"
			} else {
				$Readhost = Read-Host "Proceed with backup and replace (Y)es (A) Yes to all (N)o (C)ompare or CTRL+C to quit" 
				switch ($Readhost) {
					A { $replaceall = 'true'
						break }
					Y { Move-Item $srvplugin "$BackupPluginsDir\$($plugin.name)"
						Copy-Item $gitplugin "$srvplugin"
						write-host -NoNewline "FIXING"
						$srvpluginhash = (Get-FileHash ($srvplugin)).hash
						break }
					N { write-host -NoNewline "NOT FIXING" 
						break badHash }
					C { 
						#write-host "`n`n-ReferenceObject`n`n $srvplugincontent `n`n-DifferenceObject`n`n $gitplugincontent"
						#Compare-Object $($srvplugincontent) $($gitplugincontent) -Passthru
						fc.exe $srvplugin $gitplugin
						Write-Host -NoNewline "Checking file: $($plugin.name.trim('.cs'))"
						break }
				}
			}
		
		}
    }
	Write-Host ' - Same' -ForegroundColor green
}
