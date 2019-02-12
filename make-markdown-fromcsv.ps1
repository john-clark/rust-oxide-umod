#make markdown file in folder by reading info from csv file
$workdir="universal"
$PluginsDir = ".\plugins\$workdir"
$PluginsFile = "$PluginsDir\index.csv"
$saveFileName = "README-fromcsv.md"
#
Set-Content "$PluginsDir\$saveFileName" "# rust-oxide-umod`n`nThese plugins are $workdir category`n`n" -encoding UTF8 -Force

Import-CSV "$PluginsFile" |
Foreach-Object {
	try { $PluginName = $_.Title } catch { $pluginname='No Name' }
	try { $PluginDescription = $_.description } catch { $pluginname='No Description' }
    try { $PluginURL = $_.url } catch { $pluginname='No URL' }
	$infoline = "[$PluginName]($PluginURL) `n$PluginDescription`n"
	#write-host $infoline 
	Add-Content "$PluginsDir\$saveFileName" "$infoline" -encoding UTF8
}
