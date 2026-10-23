# Environment inspection — 2026-10-21

## Phase 02 additions
Pinned EF Core Design/Identity EF packages and local dotnet-ef tool: 10.0.12. The tool manifest is root dotnet-tools.json. Backend, Windows and Android builds pass with the expanded contracts. Initial migration applied to the default server database under LocalAppData/VanderCake, outside OneDrive. A valid localhost development HTTPS certificate exists (expires 2027-05-14); trust was not changed/verified. No new system workload/JDK/SDK installation was required. See DEVELOPMENT.md for interactive login setup.

Local date/time convention: America/Sao_Paulo; raw logs include UTC.
Inspection and builds are actual observations, not assumed prerequisites.

| Component | Observed |
|---|---|
| OS | Windows x64, 10.0.26200 |
| .NET SDK | 10.0.401, installed under C:\Program Files\dotnet |
| CLI MSBuild | 18.9.11+e34a38d2a |
| Runtimes | Microsoft.NETCore.App, Microsoft.AspNetCore.App and Microsoft.WindowsDesktop.App 10.0.12 |
| Visual Studio | Community 2026, 18.10.12224.181 |
| Other build tools | VS 2022 Build Tools 17.14.37710.0; use .NET CLI/VS 2026 for this project |
| Workloads | android 36.1.69; maui-windows 10.0.20; ios/maccatalyst 26.5.10301 also installed but not targeted |
| MAUI packs | 10.0.20 (selected), 9.0.120 also present |
| Windows SDK | 10.0.26100.0 installed; net10.0-windows10.0.19041.0 project builds successfully |
| Android compile SDK | C:\Program Files (x86)\Android\android-sdk, platforms android-35/android-36, build-tools 36.0.0 |
| Java | Microsoft OpenJDK 21.0.8; C:\Program Files\Android\openjdk\jdk-21.0.8; also found under Program Files (x86) |
| ADB | 1.0.41, platform-tools 36.0.0; no attached devices at inspection |
| Separate emulator SDK | C:\Users\dirie\AppData\Local\Android\Sdk |
| Emulator | 37.2.12.0; AVD VanderBeer_API35; API35 google_apis x86_64 system image |
| Acceleration | emulator -accel-check reports WHPX installed and usable |
| Native runtime checks | Neither VanderCake Windows nor Android app launched in this phase |

## Missing prerequisites
**None identified for the tested Debug builds.** Server, Windows and Android compile successfully.

The Program Files Android SDK has no emulator/system-images folder, but the separate user-profile SDK DOES. An initial sandbox-only inventory could not see the real LocalAppData path; the outside-sandbox inventory corrected this. Do not install another emulator merely because the compile SDK lacks one.

No running emulator/physical Android device was listed by adb. Starting the existing AVD or connecting an authorized physical device is needed for runtime validation. Runtime UI behavior, Windows App SDK activation, Android installation and server connectivity remain unverified. No release-signing certificate/store account is needed for these Debug builds; production packaging is outside scope.

## Commands / remediation
Inspect:
- dotnet --info
- dotnet workload list
- dotnet --list-sdks
- dotnet --list-runtimes
- dotnet new list
- vswhere.exe -all -products * -format json
- java.exe -version (using the full JDK path)
- adb.exe version and adb.exe devices -l
- emulator.exe -version, -list-avds, -accel-check (using user-profile SDK path)

On another machine, install the pinned .NET 10 SDK and the .NET MAUI workload through Visual Studio Installer, including Android SDK/JDK and Windows SDK. Then run dotnet workload restore src/VanderCake.Mobile/VanderCake.Mobile.csproj and rebuild. Check current compatible platform versions against [Microsoft's installation guide](https://learn.microsoft.com/dotnet/maui/get-started/installation); do not upgrade this machine's working SDK/workloads without a reason.

The sandbox initially denied Service Control Manager/CIM access and a server build returned exit 1 with no compiler diagnostics. Approved outside-sandbox diagnostics/builds succeeded. No global workload changes were needed. Java/ADB/MSBuild were not found on PATH initially; full paths and build auto-discovery worked.

Two Android SDK roots exist: keep their roles explicit when launching the AVD versus compiling. Avoid overwriting the existing unrelated VanderBeer AVD. Native runtime verification can reuse it without modifying its configuration.

