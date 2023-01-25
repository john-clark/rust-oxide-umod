
# rust-oxide-umod

## Purpose

* Simplify the install and backup of a [Steam](https://store.steampowered.com/) [Rust](https://rust.facepunch.com/) [dedicated gaming server](https://developer.valvesoftware.com/wiki/Rust_Dedicated_Server) that hosts [plugins](https://umod.org/plugins?categories=rust).
* Speed up/bypass the [uMod](https://umod.org/) site flood protection and add 3rd party/local plugin mirror.

## State

* Current [install](#install) seems to be working, but uMod is an issue...like always.
* This is an attempt at randomly pressing keys and trying to get something to work
> **Warning** None of these files are finished or properly working so don't expect anything to work at all

## Notes

* This is here only so I can work on multiple machines without copying files back and forth
* This if for Windows Powershell/Command line. 
* Some authors modify plugins without updating version, so I mirror plugins are in this repo
* umod has changed quite a bit since I first wrote this, and not for the better

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
