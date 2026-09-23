[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z][a-z0-9_]{0,62}$')][string]$Database,
    [Parameter(Mandatory = $true)][string]$BackupDirectory,
    [ValidateRange(1, 365)][int]$DailyRetention = 7,
    [ValidateRange(1, 52)][int]$WeeklyRetention = 4,
    [switch]$Weekly
)
$ErrorActionPreference = 'Stop'
# Authentication is libpq environment/PGPASSFILE, never a URI/password in process arguments.
if (-not $env:PGHOST -or -not $env:PGUSER) { throw 'Set PGHOST and PGUSER, and configure PGPASSFILE or a private PGPASSWORD environment variable.' }
if ($Database -in @('postgres', 'template0', 'template1')) { throw 'Select a catalog database, not a maintenance/template database.' }
$backupRoot = [IO.Path]::GetFullPath($BackupDirectory)
if ($backupRoot.TrimEnd('\','/') -eq [IO.Path]::GetPathRoot($backupRoot).TrimEnd('\','/')) { throw 'Use a dedicated backup directory, not a filesystem root.' }
if (-not (Test-Path -LiteralPath $backupRoot)) { New-Item -ItemType Directory -Path $backupRoot | Out-Null }
if ((Get-Item -LiteralPath $backupRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Backup directory must not be a symbolic link.' }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N')
$archive = Join-Path $backupRoot "techvault-daily-$stamp.dump"
$partial = "$archive.partial"
if (Test-Path -LiteralPath $partial) { throw 'Refusing to overwrite an existing archive.' }
$dumpOutput = & pg_dump --no-password --format=custom --no-owner --no-privileges --file $partial --dbname $Database 2>&1
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed. No retention pruning ran. Inspect the private configuration and any .partial file.' }
$listOutput = & pg_restore --list $partial 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Archive validation failed. No retention pruning ran.' }
Move-Item -LiteralPath $partial -Destination $archive
if ($Weekly) { Copy-Item -LiteralPath $archive -Destination (Join-Path $backupRoot "techvault-weekly-$stamp.dump") }

# Delete only our exact archive filenames after a verified successful backup, never recursively.
foreach ($retention in @(@('daily', $DailyRetention), @('weekly', $WeeklyRetention))) {
    $kind = $retention[0]
    $archives = Get-ChildItem -LiteralPath $backupRoot -File | Where-Object {
        $_.Name -cmatch "^techvault-$kind-[0-9]{8}T[0-9]{6}Z-[a-f0-9]{32}\.dump$"
    } | Sort-Object Name -Descending
    foreach ($oldArchive in ($archives | Select-Object -Skip $retention[1])) {
        $resolvedArchive = [IO.Path]::GetFullPath($oldArchive.FullName)
        if ([IO.Path]::GetDirectoryName($resolvedArchive) -ne $backupRoot.TrimEnd('\','/') -or
            ($oldArchive.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe retention target; refusing deletion.' }
        Remove-Item -LiteralPath $resolvedArchive
    }
}
Write-Output $archive
