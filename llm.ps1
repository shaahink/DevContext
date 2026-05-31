param(
    [string]$Path = ".",
    [string]$Output = "context.txt",
    [int]$MaxLinesPerFile = 30
)

Write-Host "Generating optimized MAUI project context..." -ForegroundColor Cyan

# Helper: Relative path converter
function Get-RelativePath($FullPath, $BasePath) {
    return $FullPath.Substring($BasePath.Length).TrimStart("\","/")
}

# Section 1: Grouped Project Structure
"--- PROJECT STRUCTURE (Grouped) ---" | Out-File $Output
$groups = @("Models","ViewModels","Services","Views","Platforms","Resources","Properties","Other")
foreach ($group in $groups) {
    "`n$group/" | Out-File $Output -Append
    Get-ChildItem -Path $Path -Recurse -Include *.cs,*.xaml,*.csproj |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" -and $_.FullName -match $group } |
        ForEach-Object { "  " + (Get-RelativePath $_.FullName $Path) } |
        Out-File $Output -Append
}

# Include root-level important files
"`nRoot Files:" | Out-File $Output -Append
Get-ChildItem -Path $Path -File -Include *.cs,*.xaml,*.csproj,*.json,*.md |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    ForEach-Object { "  " + (Get-RelativePath $_.FullName $Path) } |
    Out-File $Output -Append

# Section 2: .csproj (only unique build info)
"`n--- PROJECT FILE (.csproj) ---" | Out-File $Output -Append
Get-ChildItem -Path $Path -Recurse -Include *.csproj |
    ForEach-Object {
        "`n### $($_.Name)" | Out-File $Output -Append
        (Get-Content $_.FullName |
            Select-String -Pattern "<TargetFramework|<TargetFrameworks|<UseMaui|<ApplicationId|<ApplicationDisplayName|<ApplicationVersion|<SupportedOSPlatformVersion" |
            ForEach-Object { $_.Line }) |
            Out-File $Output -Append
    }

# Section 3: NuGet packages (unique only)
"`n--- NUGET PACKAGES ---" | Out-File $Output -Append
$nugetList = dotnet list $Path package
($nugetList | Select-Object -Skip 2 | Sort-Object -Unique) | Out-File $Output -Append

# Section 4: Key Source Files (short preview)
"`n--- SOURCE CODE PREVIEW (First $MaxLinesPerFile lines) ---" | Out-File $Output -Append
Get-ChildItem -Path $Path -Recurse -Include *.cs,*.xaml |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" -and $_.FullName -match "Models|ViewModels|Services|Views" } |
    ForEach-Object {
        "`n### $(Get-RelativePath $_.FullName $Path)" | Out-File $Output -Append
        (Get-Content $_.FullName | Select-Object -First $MaxLinesPerFile) |
            Out-File $Output -Append
    }

Write-Host "Done! Optimized context saved to $Output" -ForegroundColor Green
