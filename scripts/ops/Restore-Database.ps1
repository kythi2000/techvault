[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Archive,
    [Parameter(Mandatory = $true)][ValidatePattern('^techvault_restore_[a-z0-9_]{1,46}$')][string]$DestinationDatabase
)
$ErrorActionPreference = 'Stop'
if (-not $env:PGHOST -or -not $env:PGUSER) { throw 'Set PGHOST and PGUSER, and configure PGPASSFILE or a private PGPASSWORD environment variable.' }
if (-not (Test-Path -LiteralPath $Archive -PathType Leaf)) { throw 'Archive does not exist.' }
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
if ((Get-Item -LiteralPath $archivePath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Archive must not be a symbolic link.' }
$listOutput = & pg_restore --list $archivePath 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Archive cannot be read; no destination was created.' }
# CREATE must fail if the destination exists. Never --clean, --create, DROP, or restore over live data.
$createOutput = & createdb --no-password --template=template0 $DestinationDatabase 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Destination creation failed (possibly already exists). Nothing was restored or overwritten.' }
$restoreOutput = & pg_restore --no-password --exit-on-error --single-transaction --no-owner --no-privileges --dbname $DestinationDatabase $archivePath 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed and the transaction rolled back. The new destination is retained for diagnosis; no existing database was modified.' }
Write-Output "Restored to $DestinationDatabase. Verify data and migration history before any application cutover."
