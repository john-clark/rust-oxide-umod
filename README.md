
# rust-oxide-umod

## Purpose

* Simplify the install and backup of a [Steam](https://store.steampowered.com/) [Rust](https://rust.facepunch.com/) [dedicated gaming server](https://developer.valvesoftware.com/wiki/Rust_Dedicated_Server) that hosts [plugins](https://umod.org/plugins?categories=rust).
* Speed up/bypass the [uMod](https://umod.org/) site flood protection and add 3rd party/local plugin mirror.

## State

* Current [install](#install) seems to be working, but uMod is an issue...like always.
* This is an attempt at randomly pressing keys and trying to get something to work
> **Warning** None of these files are finished or properly working so don't expect anything to work at all

## Notes

* This if for Windows Powershell/Command line. 
* This repo is so I can work on multiple machines without copying files back and forth.
* Some plugins authors modify and release code without updating versioning so that is why I originally started to mirror plugins.
* uMod has changed quite a bit since I first wrote this, and not for the better.
* I don't play that much, not sure if I will keep going with this because it is such a PITA.
* This may sit for years without updates and only be updated when I want to look at new game updates.

## Install

**Tested environment**

* Windows Server 2022 (no desktop env, 2023 dvd)
* SConfig - name, manual updates, telemtry off, timezone set

```powershell
$source = "https://tinyurl.com/rustoxumod"
# Real link: https://raw.githubusercontent.com/john-clark/rust-oxide-umod/master/install.ps1
Invoke-WebRequest -UseBasicParsing -Uri $source -OutFile "install.ps1"
# Run the installer
.\install.ps1
```

> **Warning** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
