$category = "rust"
$folder = ".\plugins\$category\"
$first = "README.md"
$second = "README-fromcsv.md"
Compare-Object (Get-Content "$folder$first")(Get-Content "$folder$second")