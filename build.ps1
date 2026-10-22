# Builds the mod and puts the release zip in dist\ (Mods\LMDCustomRides.dll, README).
param([string]$Dotnet = "dotnet")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
& $Dotnet build (Join-Path $root "src\LMDCustomRides\LMDCustomRides.csproj") -c Release -nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$version = ([xml](Get-Content (Join-Path $root "src\LMDCustomRides\LMDCustomRides.csproj"))).Project.PropertyGroup.Version | Select-Object -First 1
$stage = Join-Path $root "dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $stage "Mods") | Out-Null
Copy-Item (Join-Path $root "src\LMDCustomRides\bin\Release\net6.0\LMDCustomRides.dll") (Join-Path $stage "Mods")
Copy-Item (Join-Path $root "README.md") (Join-Path $stage "LMDCustomRides-README.md")
$zip = Join-Path $root "dist\LMD-Custom-Rides-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
# Entries named here with "/": PowerShell 5.1's Compress-Archive (and ZipFile.CreateFromDirectory on its .NET
# Framework) write "Mods\...", which some unzip tools (Linux, Steam Deck, macOS) extract as one file named with a backslash.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry)
    }
} finally {
    $archive.Dispose()
}
Remove-Item $stage -Recurse -Force
"built $zip"
