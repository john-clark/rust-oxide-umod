install-script install-git
Set-Executionpolicy -Scope CurrentUser -ExecutionPolicy UnRestricted
install-git.ps1
$ENV:PATH = "$((Get-ItemProperty -Path 'Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Session Manager\Environment' -Name Path).Path);$((Get-ItemProperty HKCU:\Environment).PATH)"
cd \
mkdir backups
git clone https://github.com/john-clark/rust-oxide-umod.git
cd .\rust-oxide-umod
cp umod-server-vars.cfg.example umod-server-vars.cfg
cmd.exe /c .\install-steamcmd.bat
cmd.exe /c .\umod-server-start.bat