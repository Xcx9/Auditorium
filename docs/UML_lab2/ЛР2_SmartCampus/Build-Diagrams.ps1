$ErrorActionPreference = 'Stop'
$java = (Get-Command java -ErrorAction Stop).Source
$jar = Join-Path $PSScriptRoot 'tools\plantuml.jar'
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\images" | Out-Null
foreach ($format in @('png','svg')) {
    & $java -jar $jar -charset UTF-8 -failfast2 "-t$format" -o '../images' "$PSScriptRoot\diagrams\*.puml"
    if ($LASTEXITCODE -ne 0) { throw "PlantUML failed: $format" }
}
Write-Output 'Ready: images (PNG and SVG)'
