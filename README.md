
# rust-oxide-umod

*Because their site has flood protection and they need a plugin repo on github for speed*
## notes
* I am in the middle of this project
* I am not a programmer or know anything about  any computer languages
* This is an attempt at randomly pressing keys and trying to get something to work
* None of these files are finished or properly working
* Don't expect anything here to function
* This is here only so I can work on multiple machines without copying files back and forth
* I noticed that some authors modify plugins without updating version info :(

```cmd
install-script install-git
Set-Executionpolicy -Scope CurrentUser -ExecutionPolicy UnRestricted
install-git.ps1
$ENV:PATH = "$((Get-ItemProperty -Path 'Registry::HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Session Manager\Environment' -Name Path).Path);$((Get-ItemProperty HKCU:\Environment).PATH)"
cd \
mkdir backups
git clone https://github.com/john-clark/rust-oxide-umod.git
cd .\rust-oxide-umod
cp umod-server-vars.cfg.example umod-server-vars.cfg
cmd.exe /c .\powershell\install-steamcmd.bat
notepad .\umod-server-start.bat
cmd.exe /c .\umod-server-start.bat
```
