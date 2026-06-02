@echo off
REM Local CI: build Core + CLI, run all tests (including integration against real repos)
echo ========================================
echo Building solution...
echo ========================================
dotnet build -c Release || exit /b 1

echo ========================================
echo Running unit/spec tests...
echo ========================================
dotnet test DevContext.Core.Tests\DevContext.Core.Tests.csproj --filter "FullyQualifiedName~Integration" -c Release --no-build --logger "trx;LogFileName=CoreTests.trx" || exit /b 1
dotnet test DevContext.Specs\DevContext.Specs.csproj -c Release --no-build --logger "trx;LogFileName=Specs.trx" || exit /b 1

echo ========================================
echo Running CLI smoke test...
echo ========================================
dotnet run --project DevContext.Cli -- extract . --task "quick architecture overview" --around "DevContext.Core" --no-progress -o smoke-context.md || exit /b 1
if not exist smoke-context.md (
    echo FAILED: CLI produced no output
    exit /b 1
)
echo CLI smoke test passed. Output: %date% %time%

echo ========================================
echo ALL TESTS PASSED
echo ========================================
