
$catagory = "rust"
$source = "C:\backups\plugins\$catagory"
$clean = "C:\Users\Administrator\rust-oxide-umod\plugins"
$dest = "C:\Users\Administrator\rust-oxide-umod\plugins\umod\$catagory"
write-host "Working on $catagory" -foregroundcolor red
ForEach ($file in Get-ChildItem $clean) {
	if (Test-Path "$source\$file") {
	write-host "Moving $file.FullName to $dest"
	Move-Item $file.FullName $dest 
	}
}
$catagory = "universal"
$source = "C:\backups\plugins\$catagory"
$clean = "C:\Users\Administrator\rust-oxide-umod\plugins"
$dest = "C:\Users\Administrator\rust-oxide-umod\plugins\umod\$catagory"
write-host "Working on $catagory" -foregroundcolor red
 ForEach ($file in Get-ChildItem $clean) {
	if (Test-Path "$source\$file") {
	write-host "Moving $file.FullName to $dest"
	Move-Item $file.FullName $dest 
	}
}