# Compiles the bgfx shader sources (hksrc/src/shaders/*.sc) into .bin files
# the engine loads at runtime (Working/shaders/*.bin).
# Run after changing any .sc file:  powershell -File Tools/build-shaders.ps1
$ErrorActionPreference = 'Stop'

$root     = Split-Path $PSScriptRoot -Parent
$tp       = Join-Path $root 'hksrc\src\Thirdparty'
$shaderc  = Join-Path $tp 'bgfx\.build\win64_mingw-gcc\bin\shadercRelease.exe'  # genie appends the config name
$common   = Join-Path $tp 'bgfx\src'             # holds bgfx_shader.sh (moved here from examples/common)
$srcDir   = Join-Path $root 'hksrc\src\shaders'
$outDir   = Join-Path $root 'Working\shaders'

if (-not (Test-Path $shaderc)) { throw "shaderc.exe not built yet - run Tools/setup-bgfx.ps1 first" }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# one entry per shader stage: name + shaderc --type
$stages = @(
    @{ Name = 'vs_basic'; Type = 'vertex'   },
    @{ Name = 'fs_basic'; Type = 'fragment' }
)

foreach ($s in $stages) {
    & $shaderc `
        -f (Join-Path $srcDir "$($s.Name).sc") `
        -o (Join-Path $outDir "$($s.Name).bin") `
        --platform windows --type $s.Type --profile 300_es `
        -i $common `
        --varyingdef (Join-Path $srcDir 'varying.def.sc')
    if ($LASTEXITCODE -ne 0) { throw "shaderc failed for $($s.Name)" }
    Write-Host "[shaders] compiled $($s.Name).bin"
}
