$backupdir = "C:\backups\"
$logfile = $backupdir + 'umod-rust-' + (get-date -Format yyyyMMdd ) + '.log'
Get-Content -path $logfile -tail 10 -wait