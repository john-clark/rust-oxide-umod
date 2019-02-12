#make markdown file in folder by reading info from plugins (no links)
$workdir="universal"
$PluginsDir = ".\plugins\$workdir"
$saveFileName = "README-fromfiles.md"

#
Set-Content "$PluginsDir\$saveFileName" "# rust-oxide-umod`n`nThese plugins are $workdir category`n`n" -encoding UTF8 -Force

Get-ChildItem "$PluginsDir" -Recurse -Filter *.cs |
foreach-Object {
	$content = Get-Content $_.FullName
	try {
	$infoline = $content | where-object { $_.Contains("[Info(") }
		$t,$pluginname,$t,$pluginauthor,$t,$pluginversion,$t=$infoline.split('"')
	} catch {
		$pluginname='No Name'
	}
    #lots of plugins are missing the description
    try {
		$descriptionline = $content | where-object { $_.Contains("[Description(") }
        $t,$plugindescription,$t=$descriptionline.split('"')
    } catch {
       $plugindescription='No Description'
    }
	$infoline = "[$pluginname] (http://nourlyet) `n$plugindescription`n"
	#write-host $infoline 
	Add-Content "$PluginsDir\$saveFileName" "$infoline" -encoding UTF8
}