<#
.CREATED BY:
    John Clark    Copyright 2019 
.CREATED ON:
    01/11/2019
.Synopsis
   Downloads all available mods from umod website
.DESCRIPTION
   This funciton will download all mods from a umod website and save them to your computer.
   Requires PSv3+
   File Name      : uMod-plugin-download.ps1
.LINK
    Script posted over:
    https://github.com/john-clark/rust-oxide-umod
.EXAMPLE
   PS C:\> umod-plugin-download.ps1
   

-------------------------------------------------------------------------
                          Edit these variables
-------------------------------------------------------------------------
#>

$uModURL="https://umod.org/plugins/"
$PluginBackupDir="C:\rust-oxide-umod\plugins\"
$Category='rust'
$DownloadFiles="false"
$DumpCSV="true"
$DumpMD="true"

<#
-------------------------------------------------------------------------
                           Don't edit past here
-------------------------------------------------------------------------
#>

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function uMod-Plugin-WebQuery {
    Param ( [string]$uCategory, [int]$uPage )
    $Query="search.json?query=&filter=&categories[]=$Category&page=$Page"
    $The_Response = $null
   
    try {
        $The_Request = "$uModURL$Query"
        $Web_Client = New-Object System.Net.WebClient
        $The_Response = $Web_Client.DownloadString($The_Request)
        $Web_Client.Dispose()
    }
    catch [Net.WebException] {
        $_ | fl * -Force
		Start-Sleep 10
    }
    Return $The_Response
}

function uMod-Plugin-Download {
	Param ( [string]$download_url, [string]$Category )
	
    $PluginFilename = ([System.Uri]"$download_url").Segments[2]
	$download_file = $PluginBackupDir + $Category + '\' + $PluginFilename
    try {
        $Web_Client = New-Object System.Net.WebClient
		$Web_Client.Headers.Add("user-agent", "Powershell uMod Updater 0.1")
        $Web_Client.DownloadFile($download_url, $download_file) 
        $Web_Client.Dispose()
		
		Flood-Control 5
    }
    catch [System.Net.WebException] {
        $_ | fl * -Force
    }
}

function Flood-Control {
	write-host 'Sleeping to avoid flood protection' -ForegroundColor red
	Start-Sleep $args[0]
}

#--------------------------------------------------------------------------------
#This will get the last page so we know how much to loop through
$Page=999
$ResponseArray = uMod-Plugin-WebQuery $Category $Page | ConvertFrom-Json
$TotalRustPluginPages = $ResponseArray.last_page
#--------------------------------------------------------------------------------
if ( $DumpMD -eq 'true' ) {
	try { 
		Remove-Item "$PluginBackupDir$Category\README.md" -force
		Set-Content "$PluginBackupDir$Category\README.md" "# rust-oxide-umod`n`nThese plugins are $Category category`n`n"-encoding UTF8 -Force
	}
	catch {
		Set-Content "$PluginBackupDir$Category\README.md" "# rust-oxide-umod`n`nThese plugins are $Category category`n`n"-encoding UTF8 -Force
	}
}
if ( $DumpCSV -eq 'true' ) {
	try { Remove-Item "$PluginBackupDir$Category\index.csv" -force }
	catch {	}
}

For ($Page=1; $Page -le $TotalRustPluginPages; $Page++) {
    $timeleft = [timespan]::fromseconds($(($TotalRustPluginPages-$page)*50)).tostring()
    write-host "Downloading Page $Page of $TotalRustPluginPages"  -ForegroundColor Yellow
	#get json page
    $Response = uMod-Plugin-WebQuery $Category $Page
	#extract jason data
    $Responsejson = $Response | ConvertFrom-Json
	#loop through data
    foreach ($Object in $Responsejson.data) {
        try { $PluginName = $Object.name }
		catch { $PluginName = "Not found" }
		try { $PluginTitle = $Object.title }
		catch { $PluginTitle = "Not found" }
        try { $PluginVersion = $Object.latest_release_version }
		catch { $PluginVersion = "Not found" }
		try { $PluginURL = $Object.url }
		catch { $PluginURL = "Not found" }
		try { $PluginDescription = $Object.description }
		catch { $PluginDescription = "Not found" }
		#show in console
        write-host "$PluginName - $PluginVersion"  -ForegroundColor white
		#download file
		if ( $DownloadFiles -eq 'true' ) {
			uMod-Plugin-Download $Object.download_url $Category
		}
		#dump csv
		if ( $DumpCSV -eq 'true' ) {
			$pluginfilecsv = $PluginBackupDir + $Category + '\index.csv'
			$Object | Export-CSV -path $pluginfilecsv -Append -Encoding UTF8 -NoTypeInformation
		}
		#make a markdown file
		if ( $DumpMD -eq 'true' ) {
			$pluginfilemd = $PluginBackupDir + $Category + '\README.md'
			$MDOutput = "[$PluginTitle]($PluginURL) `n$PluginDescription`n"
			Add-Content -path $pluginfilemd $MDOutput -Encoding UTF8
		}
    }
	Flood-Control 5
	
}