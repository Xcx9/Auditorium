$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\tools\Generate-Code.ps1"
foreach ($project in @('app\SmartCampus.Demo.csproj', 'example\PersonExample.csproj')) {
    # Внешних библиотек нет: восстановление использует только установленный .NET SDK.
    & dotnet restore (Join-Path $PSScriptRoot $project) --configfile "$PSScriptRoot\NuGet.Config"
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $project" }
    & dotnet run --no-restore --project (Join-Path $PSScriptRoot $project)
    if ($LASTEXITCODE -ne 0) { throw "Demo failed: $project" }
}
