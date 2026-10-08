# Rewrites the goldens with what the decompiler now produces, takes the shaders
# that have become fixed points off KnownNonFixedPoints.txt, and says what changed.
#
# The goldens are blessed first and the fixed points measured after, in a run of
# their own: the fixed point test compiles the goldens, and has to see the new
# ones. A golden that stops being a fixed point is not blessed - it still fails,
# and is reported below. Review the result with git diff before committing it.
#
#   powershell -File HlslDecompiler.Tests\Bless.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tests = Join-Path $root "HlslDecompiler.Tests"
$known = Join-Path $tests "KnownNonFixedPoints.txt"

# Runs the tests the filter picks, showing the failures and the summary, and says
# whether they all passed.
function RunTests($filter) {
    dotnet test $tests --no-build --nologo --filter $filter |
        Where-Object { $_ -match "^\s*(Failed |Passed!|Failed!)" } |
        ForEach-Object { Write-Host $_ }
    return $LASTEXITCODE -eq 0
}

Push-Location $root
try {
    dotnet build (Join-Path $root "HlslDecompiler.sln") --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "The build failed."
    }
    $knownBefore = Get-Content $known

    $env:HLSL_BLESS = "1"
    Write-Host "Blessing the goldens..."
    $goldensPassed = RunTests "FullyQualifiedName~HlslDecompiler.Tests.DecompileTests|FullyQualifiedName~HlslDecompiler.Tests.DecompileDxbcTests|FullyQualifiedName~HlslDecompiler.Tests.DisassemblyOnlyTests"
    Write-Host "Measuring the fixed points against them..."
    $fixedPointsPassed = RunTests "FullyQualifiedName~HlslDecompiler.Tests.FixedPointTests&Name!~SecondRound"
    Remove-Item Env:\HLSL_BLESS

    $paths = @("ShaderSources", "ShaderAssembly", "DisassemblyOnly") |
        ForEach-Object { Join-Path "HlslDecompiler.Tests" $_ }
    Write-Host ""
    Write-Host "Goldens changed or added:"
    git --no-pager diff --stat -- $paths
    git status --short --untracked-files=all -- $paths | Where-Object { $_ -like '`?`?*' }
    Write-Host ""
    Write-Host "Now fixed points, taken off KnownNonFixedPoints.txt:"
    $knownAfter = Get-Content $known
    $knownBefore | Where-Object { $_ -match "^[a-z]" -and $knownAfter -notcontains $_ } |
        ForEach-Object { Write-Host "  $_" }
    if (-not ($goldensPassed -and $fixedPointsPassed)) {
        Write-Host ""
        Write-Host "Some tests failed, listed above. A golden that stopped being a fixed point is one of them."
        exit 1
    }
}
finally {
    Remove-Item Env:\HLSL_BLESS -ErrorAction SilentlyContinue
    Pop-Location
}
