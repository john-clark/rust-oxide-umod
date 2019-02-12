
# rust-oxide-umod

*Because their site has flood protection and they need a plugin repo on github for speed*
## notes
* I am not a programmer or know anything about  any computer languages
* This is an attempt at randomly pressing keys and trying to get something to work
* None of these files are finished or properly working
* Don't expect anything here to function
* This is here only so I can work on multiple machines without copying files back and forth
* Currently I am testing mechanics of comparing versions and info
* I noticed that some authors modify plugins without updating version info :(
## future
* Scrape the web for version information
* Compare to the files to what is downloaded
* Update if needed
* need a have a local database 
	* because some servers will modify files
	* server admins may not want some updated
	* some files will not be on uMod website

## files
| name| info  |
|--|--|
| umod-plugin-download.ps1 | bloatware
| make-markdown-from{csv/files}.ps1 | testing dumping info
| compare.ps1| compare certain README files
| /plugins/{folder]/index.csv | combined json from all uMod pages 
| /plugins/{folder]/README.md | info from web json/csv 
| /plugins/{folder]/README-fromfiles.md | info scraped from individual files 
| /plugins/{folder]/README-fromcsv.md | markdown info scraped from csv 

## folders
| name  | description  |
|--|--|
| /plugins/universal  | uMod official Universal plugins |
| /plugins/rust	| uMod Rust server plugins
| /plugins/other | Found around the internet and not on uMod website
