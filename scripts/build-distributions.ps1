param(
    [string]$Version = '1.0.0-rc1',
    [Parameter(Mandatory)] [string]$BepInExPayload,
    [Parameter(Mandatory)] [string]$Icon,
    [Parameter(Mandatory)] [string]$Logo
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repo 'dist'
$work = Join-Path $dist '_build'
$payload = Join-Path $work 'payload'
$auto = Join-Path $work 'auto'
$manual = Join-Path $work 'manual'
if (Test-Path $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $payload,$auto,$manual | Out-Null
Copy-Item -Path (Join-Path $BepInExPayload '*') -Destination $payload -Recurse -Force
Copy-Item -Path (Join-Path $repo 'package\BepInEx\*') -Destination (Join-Path $payload 'BepInEx') -Recurse -Force
$payloadZip = Join-Path $work 'payload.zip'
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $payloadZip -CompressionLevel Optimal
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$exe = Join-Path $auto '한국어 패치 설치기.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /optimize+ "/out:$exe" "/win32icon:$Icon" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$framework\System.IO.Compression.dll" "/reference:$framework\System.IO.Compression.FileSystem.dll" "/resource:$Logo,InstallerLogo" "/resource:$Icon,InstallerIcon" "/resource:$payloadZip,payload.zip" (Join-Path $repo 'installer\Installer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Copy-Item (Join-Path $repo 'distribution\README_Auto.txt') (Join-Path $auto 'README.txt')
Copy-Item (Join-Path $repo 'THIRD_PARTY_NOTICES.txt') (Join-Path $auto '제3자_라이선스_고지.txt')
New-Item -ItemType Directory -Force -Path (Join-Path $manual '패치파일') | Out-Null
Copy-Item -Path (Join-Path $payload '*') -Destination (Join-Path $manual '패치파일') -Recurse -Force
Copy-Item (Join-Path $repo 'distribution\README_Manual.txt') (Join-Path $manual 'README.txt')
Copy-Item (Join-Path $repo 'THIRD_PARTY_NOTICES.txt') (Join-Path $manual '제3자_라이선스_고지.txt')
$autoZip = Join-Path $dist "Utsutsutsu_Korean_Patch_Auto_v$Version.zip"
$manualZip = Join-Path $dist "Utsutsutsu_Korean_Patch_Manual_v$Version.zip"
Compress-Archive -Path (Join-Path $auto '*') -DestinationPath $autoZip -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $manual '*') -DestinationPath $manualZip -CompressionLevel Optimal -Force
$hashes = Get-FileHash -Algorithm SHA256 $autoZip,$manualZip | ForEach-Object { $_.Hash.ToLowerInvariant() + ' *' + (Split-Path -Leaf $_.Path) }
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'),$hashes,[Text.UTF8Encoding]::new($false))

