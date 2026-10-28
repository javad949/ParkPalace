<#
.SYNOPSIS
    Restore-ParkPalaceDatabase.ps1 - Local Disaster Recovery Drill with full DBCC and Relational Integrity checks
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$BackupFilePath,

    [Parameter()]
    [string]$ServerInstance = "localhost",

    [Parameter()]
    [string]$TargetDrillDatabaseName = "",

    [Parameter()]
    [string]$TargetDataDirectory = "",

    [Parameter()]
    [switch]$KeepDrillDatabase = $false
)

$ErrorActionPreference = "Stop"

function Write-Log([string]$Message, [string]$Level = "INFO") {
    $ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $color = switch ($Level) {
        "ERROR"   { "Red" }
        "WARN"    { "Yellow" }
        "SUCCESS" { "Green" }
        default   { "Cyan" }
    }
    Write-Host "[$ts] [$Level] $Message" -ForegroundColor $color
}

if (-not (Test-Path -Path $BackupFilePath)) {
    throw "Backup file not found at path: $BackupFilePath"
}

# Unique independent target drill database name
if ([string]::IsNullOrWhiteSpace($TargetDrillDatabaseName)) {
    $tsSuffix = (Get-Date).ToString("yyyyMMdd_HHmmss")
    $TargetDrillDatabaseName = "ParkPalaceCRM_RestoreDrill_$tsSuffix"
}

Write-Log "Starting Local Restore Drill using file: $BackupFilePath"
Write-Log "Target independent drill database: '$TargetDrillDatabaseName' on server '$ServerInstance'"

Add-Type -AssemblyName 'System.Data'
$connString = "Server=$ServerInstance;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)
$conn.Open()

# Safety Check: Never overwrite an existing database
$cmdCheckDb = $conn.CreateCommand()
$cmdCheckDb.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = N'$TargetDrillDatabaseName';"
$exists = [int]$cmdCheckDb.ExecuteScalar()
if ($exists -gt 0) {
    $conn.Close()
    throw "Database '$TargetDrillDatabaseName' already exists. Aborting drill to prevent overwriting existing databases."
}

# Step 1: RESTORE VERIFYONLY with Checksum
Write-Log "Step 1: Running RESTORE VERIFYONLY with CHECKSUM..."
$cmdVerify = $conn.CreateCommand()
$cmdVerify.CommandTimeout = 600
$cmdVerify.CommandText = "RESTORE VERIFYONLY FROM DISK = N'$BackupFilePath' WITH CHECKSUM;"
$cmdVerify.ExecuteNonQuery() | Out-Null
Write-Log "RESTORE VERIFYONLY completed successfully." "SUCCESS"

# Step 2: Extract Logical File Names
Write-Log "Step 2: Inspecting logical file structure via RESTORE FILELISTONLY..."
$cmdFileList = $conn.CreateCommand()
$cmdFileList.CommandText = "RESTORE FILELISTONLY FROM DISK = N'$BackupFilePath';"
$reader = $cmdFileList.ExecuteReader()

$logicalDataName = $null
$logicalLogName = $null

while ($reader.Read()) {
    $logicalName = $reader["LogicalName"].ToString()
    $type = $reader["Type"].ToString()
    if ($type -eq "D" -and -not $logicalDataName) {
        $logicalDataName = $logicalName
    }
    elseif ($type -eq "L" -and -not $logicalLogName) {
        $logicalLogName = $logicalName
    }
}
$reader.Close()

Write-Log "Logical Data File: $logicalDataName | Logical Log File: $logicalLogName"

# Determine SQL Server Instance Data Path
if ([string]::IsNullOrWhiteSpace($TargetDataDirectory)) {
    $cmdPath = $conn.CreateCommand()
    $cmdPath.CommandText = "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DataPath;"
    $rPath = $cmdPath.ExecuteReader()
    if ($rPath.Read() -and -not [string]::IsNullOrWhiteSpace($rPath["DataPath"])) {
        $TargetDataDirectory = $rPath["DataPath"].ToString()
    } else {
        $TargetDataDirectory = "C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA"
    }
    $rPath.Close()
}

$destMdf = Join-Path -Path $TargetDataDirectory -ChildPath "$($TargetDrillDatabaseName).mdf"
$destLdf = Join-Path -Path $TargetDataDirectory -ChildPath "$($TargetDrillDatabaseName)_log.ldf"

Write-Log "Physical data file destination: $destMdf"
Write-Log "Physical log file destination: $destLdf"

# Step 3: Execute RESTORE DATABASE WITH MOVE
Write-Log "Step 3: Restoring to independent drill database '$TargetDrillDatabaseName'..."
$restoreSql = @"
RESTORE DATABASE [$TargetDrillDatabaseName]
FROM DISK = N'$BackupFilePath'
WITH 
    MOVE N'$logicalDataName' TO N'$destMdf',
    MOVE N'$logicalLogName' TO N'$destLdf',
    RECOVERY,
    STATS = 10;
"@

$cmdRestore = $conn.CreateCommand()
$cmdRestore.CommandTimeout = 1200
$cmdRestore.CommandText = $restoreSql
$cmdRestore.ExecuteNonQuery() | Out-Null
Write-Log "RESTORE DATABASE to drill database completed successfully." "SUCCESS"

# Step 4: DBCC CHECKDB Physical & Logical Integrity Check
Write-Log "Step 4: Running DBCC CHECKDB for physical and logical allocation integrity..."
$cmdCheckDb = $conn.CreateCommand()
$cmdCheckDb.CommandTimeout = 600
$cmdCheckDb.CommandText = "DBCC CHECKDB ([$TargetDrillDatabaseName]) WITH NO_INFOMSGS, ALL_ERRORMSGS;"
$cmdCheckDb.ExecuteNonQuery() | Out-Null
Write-Log "DBCC CHECKDB completed with 0 errors (clean physical and allocation structures)." "SUCCESS"

# Step 5: Relational Integrity & Constraint Verification
Write-Log "Step 5: Verifying foreign key relationships and constraint integrity with DBCC CHECKCONSTRAINTS..."
$relationSql = @"
USE [$TargetDrillDatabaseName];
CREATE TABLE #ConstraintViolations (
    TableName sysname,
    ConstraintName sysname,
    WhereClause nvarchar(max)
);
INSERT INTO #ConstraintViolations
EXEC('DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS');
SELECT COUNT(*) AS ViolationCount FROM #ConstraintViolations;
DROP TABLE #ConstraintViolations;
"@

$cmdRelation = $conn.CreateCommand()
$cmdRelation.CommandText = $relationSql
$violations = [int]$cmdRelation.ExecuteScalar()

if ($violations -eq 0) {
    Write-Log "Foreign key relationship integrity verified: 0 constraint violations across all tables." "SUCCESS"
} else {
    throw "Relational integrity check failed: $violations constraint violations detected."
}

# Step 6: Sample Data and Structure Inspection
Write-Log "Step 6: Inspecting restored operational sample data and schema..."
$sampleSql = @"
USE [$TargetDrillDatabaseName];
SELECT 
    COUNT(*) AS TotalTables,
    ISNULL(SUM(p.rows), 0) AS TotalRows
FROM sys.tables t
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1);
"@

$cmdSample = $conn.CreateCommand()
$cmdSample.CommandText = $sampleSql
$rSample = $cmdSample.ExecuteReader()
if ($rSample.Read()) {
    $tables = $rSample["TotalTables"]
    $rows = $rSample["TotalRows"]

    Write-Log "Restored Data Verification:" "INFO"
    Write-Log "  Total database tables: $tables" "INFO"
    Write-Log "  Total row count across tables: $rows" "SUCCESS"
}
$rSample.Close()

# Step 7: Clean up temporary drill database
if (-not $KeepDrillDatabase) {
    Write-Log "Step 7: Cleaning up temporary drill database..."
    $cmdDrop = $conn.CreateCommand()
    $cmdDrop.CommandText = @"
USE master;
ALTER DATABASE [$TargetDrillDatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$TargetDrillDatabaseName];
"@
    $cmdDrop.ExecuteNonQuery() | Out-Null
    Write-Log "Temporary restore drill database removed successfully." "INFO"
}
else {
    Write-Log "Drill database '$TargetDrillDatabaseName' retained for inspection." "WARN"
}

$conn.Close()
Write-Log "Local Restore Drill completed with 100% SUCCESS." "SUCCESS"
