# Performance tiers of Opsis News Crawler (0.19.0)

The program adapts itself to the PC: **Low**, **Balanced** or **High**. Settings > General > *Performance*: Automatic (default) / Low / Balanced / High; the card shows what was detected and why this tier is in use.

## What is detected (`Windows/HardwareDetector.cs`, a few Win32 calls, no WMI, no hardware monitor)
| Reading | How |
|---|---|
| logical processors | `Environment.ProcessorCount` |
| physical memory | `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` |
| real graphics adapter | `EnumDisplayDevices`: the primary display's adapter is *not* a software renderer ("Microsoft Basic Render Driver / Display", Remote Display, RDP, Hyper-V Video, VMware SVGA, Parallels, VirtualBox, Citrix) |
| battery / battery saver | `GetSystemPowerStatus` (AC line, "no battery" for desktops, system status flag 1 = battery saver) |

## Which tier (`Core/Performance.cs`, `PerformanceClassifier`)
1. battery saver on → **Low**; no real graphics adapter → **Low**; 2 or fewer processors → **Low**; less than 6 GB → **Low**
2. on battery → **Balanced** (the full effects are for a plugged-in PC); fewer than 8 processors or less than 12 GB → **Balanced**
3. otherwise **High**

The free GitHub runner (2 vCPU, ~7 GB, no GPU) is therefore a *Low* PC — the natural low-spec test environment. `PerformanceTests` simulate the runner, a laptop, a strong desktop, battery and a failing detection.

## What each tier changes (one table: `PerformanceProfile.For`, applied in `TickerHost.ApplyPerformance`)
| | Low | Balanced | High |
|---|---|---|---|
| scroll step | every 33 ms (~30 fps) | 20 ms (50 fps) | 15 ms (66 fps) |
| taskbar polling | 90 ms | 45 ms | 30 ms |
| splash / minimise animations | simple fade | full | full |
| refresh of news, prices, weather, disasters | intervals × 2 | as set | as set |

The scroll speed the user sees never changes (the scroll is time based); the user's own intervals are never made shorter. The notification-area icon of the program is always still (since 0.19.2); it shows the live weather and the highest alert.

## Automatic changes
- Evaluated at the start, when the settings change and **every minute**.
- **Down is immediate** (battery saver, unplugged, slow PC); **up needs two equal readings in a row** (about 2 minutes), so a PC does not flicker between two tiers. Unplugged laptop → Balanced; plugged in again → back to High by itself.
- Order of precedence: test variable `OPSISNEWSCRAWLER_FORCE_TIER=Low|Balanced|High` → the user's setting (Low / Balanced / High) → inside Opsis the tier Opsis reports (`SetHardwareProfile`) while the setting is Automatic → detection. A failing detection never throws: the tier in use is kept (Balanced at the very start).
- Every change is written to `crawler.log` ("Performance tier: Low (gpu); specs: …").

## Not covered
Real graphics cards, several monitors, per-monitor DPI and measurements of CPU / RAM at idle per tier were not measured on a real PC (NOT VERIFIED).
