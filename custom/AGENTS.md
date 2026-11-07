# SalaryBeat custom project

- Main application: Desktop/SalaryBeat.Desktop.csproj; do not edit upstream src/ to change the custom app.
- Core behavior: `dotnet run --project Tests/SalaryBeat.Tests.csproj -c Release`.
- Use .NET 10. Use a locally installed SDK; development machines may use an offline NuGet feed, which must not be committed.
- Local cross-build: `dotnet build Desktop/SalaryBeat.Desktop.csproj -c Release -p:UseAppHost=false -p:NuGetAudit=false --source /tmp/lucyna-offline-nuget`.
- build.ps1 produces self-contained x64 by default in dist/1.1.1/win-x64. package.py creates private local ZIPs and verifies their contents.
- Calendar JSONs are EmbeddedResource in Core. Never regress to requiring sidecar JSON for bundled years. User calendars override built-ins only after validation. Keep MIT notices.
- SettingsView is the BACK FACE of the same 640x400 card, not a separate Window. ShellView owns the flip animation; CardNavigation guards transition state. Keep the zero-animation fallback.
- Theme.xaml is loaded as embedded XAML. XML checks/cross-compilation cannot prove its runtime rendering. Windows-only `SalaryBeat.exe --render-preview output-folder` exercises actual WPF templates without touching the real profile or registry; say unverified unless run.
- Preserve old SalaryGlowLeo profile, recognize its own legacy startup entry, and do not overwrite a populated SalaryBeat profile.
- Never package private settings, .git, .env, bin/obj or old dist versions. Do not publicly publish unless the owner asks.
