# Clones bgfx/bx/bimg into hksrc/src/Thirdparty and builds the MinGW GCC
# static libs that CMakeLists.txt expects, plus shaderc (shader compiler).
# Run once (and again after a bgfx update):  powershell -File Tools/setup-bgfx.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$tp   = Join-Path $root 'hksrc\src\Thirdparty'

# 1) clone the three sibling repos (gitignored; re-fetched any time)
foreach ($repo in @('bgfx', 'bx', 'bimg')) {
    $dest = Join-Path $tp $repo
    if (Test-Path $dest) {
        Write-Host "[setup] $repo already cloned, skipping"
    } else {
        Write-Host "[setup] cloning $repo"
        git clone --depth 1 "https://github.com/bkaradzic/$repo" $dest
        if ($LASTEXITCODE -ne 0) { throw "git clone $repo failed" }
    }
}

# 2) generate MinGW makefiles with bx's bundled genie (run from inside bgfx/).
#    --with-tools adds shaderc (and its vendored glslang/spirv-cross) to the
#    project set; without it genie only generates the four library projects.
Push-Location (Join-Path $tp 'bgfx')
& (Join-Path $tp 'bx\tools\bin\windows\genie.exe') --gcc=mingw-gcc --with-tools gmake
if ($LASTEXITCODE -ne 0) { throw "genie failed" }

# 3) build everything (bgfx/bimg/bx libs + shaderc and the other tools).
#    This takes a while the first time (glslang, spirv-cross, etc).
#
#    mingw32-make runs each recipe through sh.exe; MSYS2 keeps it in usr\bin,
#    which usually is NOT on PATH even when ucrt64\bin is. Add it locally.
$gcc = (Get-Command x86_64-w64-mingw32-g++.exe -ErrorAction SilentlyContinue)
if (-not $gcc) { throw "x86_64-w64-mingw32-g++.exe not on PATH (install MSYS2 mingw-w64-ucrt-x86_64-gcc)" }
$mingwRoot = Split-Path (Split-Path $gcc.Source)   # ...\msys64\ucrt64\bin\g++.exe -> ...\msys64\ucrt64
$msysRoot  = Split-Path $mingwRoot                 # -> ...\msys64
$env:PATH = (Join-Path $msysRoot 'usr\bin') + ';' + $env:PATH

# genie's mingw-gcc makefiles build every tool path as $(MINGW)/bin/..., and
# default MINGW to /bin (Debian cross-toolchain layout). Override it to point
# at MSYS2's ucrt64 root, where the prefixed compiler + ar actually live.
$mingwRoot = $mingwRoot -replace '\\', '/'

$jobs = $env:NUMBER_OF_PROCESSORS
if (-not $jobs) { $jobs = 4 }
mingw32-make -C ".build\projects\gmake-mingw-gcc" config=release64 -j $jobs MINGW="$mingwRoot"
if ($LASTEXITCODE -ne 0) { throw "mingw32-make failed" }
Pop-Location

$bin = Join-Path $tp 'bgfx\.build\win64_mingw-gcc\bin'
Write-Host "[setup] done. libs + tools in $bin"
Get-ChildItem $bin -Filter '*.a' | Select-Object Name
