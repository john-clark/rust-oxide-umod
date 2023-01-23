
# rust-oxide-umod

*Because their site has flood protection and they need a plugin repo on github for speed*

## notes

* This is an attempt at randomly pressing keys and trying to get something to work
* None of these files are finished or properly working so don't expect anything here to function
* This is here only so I can work on multiple machines without copying files back and forth
* I noticed that some authors modify plugins without updating version info :(

## install

**Tested environment**

* Windows Server 2022 (no desktop env, 2023 dvd)
* SConfig - name, manual updates, telemtry off, timezone set

```powershell
$source = "https://tinyurl.com/rustoxumod"
# Real link is here if tinyul stops working
# https://raw.githubusercontent.com/john-clark/rust-oxide-umod/master/install.ps1
Invoke-WebRequest -UseBasicParsing -Uri $source -OutFile "install.ps1"
# Run the installer
.\install.ps1
```