
$catagory = "rust"
$source = .\plugins\$catagory"
$clean = ".\rust-oxide-umod\plugins"
$dest = ".\rust-oxide-umod\plugins\umod\$catagory"
write-host "Working on $catagory" -foregroundcolor red
ForEach ($file in Get-ChildItem $clean) {
	if (Test-Path "$source\$file") {
	write-host "Moving $file.FullName to $dest"
	Move-Item $file.FullName $dest 
	}
}
$catagory = "universal"
$source = ".\plugins\$catagory"
$clean = ".\rust-oxide-umod\plugins"
$dest = ".\rust-oxide-umod\plugins\umod\$catagory"
write-host "Working on $catagory" -foregroundcolor red
 ForEach ($file in Get-ChildItem $clean) {
	if (Test-Path "$source\$file") {
	write-host "Moving $file.FullName to $dest"
	Move-Item $file.FullName $dest 
	}
}