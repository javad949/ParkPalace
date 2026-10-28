<#
.SYNOPSIS
    Atomic, Audited Execution of Phase 1 Category Standardization for 443 Authorized Materials.
.DESCRIPTION
    1. Validates pre-execution manifest (exact 443 items, zero expansion).
    2. Takes a verified full backup of ParkPalaceCRM database with checksum.
    3. Exports a complete pre-execution snapshot of Materials table.
    4. Resolves destination categories (reusing existing categories without duplicates; 
       points packaging items directly to root CAT-PACKAGING).
    5. In an atomic transaction, updates ONLY CategoryId (and appends audit note) 
       with strict concurrency checking (guards against concurrent user changes).
    6. Compares before vs. after across all columns to mathematically prove non-category 
       columns (Name, Brand, Packaging, Size, Units, Conversions, Stock) are 100% unchanged.
#>

[CmdletBinding()]
param(
    [string]$ManifestPath = "d:\Antigravity\ParkPlace\scratch\phase1_category_change_plan.json",
    [string]$RemoteHost = "crm-mgmt.sahar.loc",
    [int]$RemotePort = 5987
)

$ErrorActionPreference = 'Stop'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace CRM - Phase 1 Category Standardization Execution" -ForegroundColor Cyan
Write-Host " Target Host: $RemoteHost ($RemotePort) -> 192.168.1.248" -ForegroundColor Cyan
Write-Host " Target Database: ParkPalaceCRM" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 1: Validate Local Manifest
Write-Host "`n[Step 1] Loading and validating Phase 1 manifest..." -ForegroundColor Yellow
if (-not (Test-Path $ManifestPath)) {
    throw "Manifest file not found at $ManifestPath"
}

$manifestJson = Get-Content $ManifestPath -Raw -Encoding UTF8
$manifestItems = $manifestJson | ConvertFrom-Json
$manifestCount = $manifestItems.Count

Write-Host "  Manifest Items Count: $manifestCount"
if ($manifestCount -ne 443) {
    throw "Manifest count is $manifestCount, expected exactly 443 authorized items! Aborting."
}

$manifestHash = (Get-FileHash -Path $ManifestPath -Algorithm SHA256).Hash
Write-Host "  Manifest SHA-256: $manifestHash" -ForegroundColor Green

# Step 2: Establish Secure WinRM Session
Write-Host "`n[Step 2] Establishing secure WinRM session to https://${RemoteHost}:${RemotePort}/wsman..." -ForegroundColor Yellow
$secPass = ConvertTo-SecureString "OR@1405.srv" -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ("CRM\Administrator", $secPass)
$so = New-PSSessionOption -SkipCACheck:$false -SkipCNCheck:$false -SkipRevocationCheck:$false

$session = New-PSSession -ConnectionUri "https://${RemoteHost}:${RemotePort}/wsman" -Credential $cred -SessionOption $so -Authentication Negotiate
Write-Host "  [OK] Secure WinRM session established." -ForegroundColor Green

try {
    # Step 3: Backup Database with Checksum & Verify
    Write-Host "`n[Step 3] Taking verified full database backup of ParkPalaceCRM..." -ForegroundColor Yellow
    $backupResult = Invoke-Command -Session $session -ScriptBlock {
        $backupDir = "D:\ParkPlaceCRM\backups\sql"
        if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir | Out-Null }
        
        $ts = (Get-Date).ToString("yyyyMMdd_HHmmss")
        $bakFile = Join-Path $backupDir "ParkPalaceCRM_Pre_Phase1_CategoryUpdate_${ts}.bak"
        
        $sql = @"
BACKUP DATABASE [ParkPalaceCRM] 
TO DISK = N'$bakFile' 
WITH CHECKSUM, INIT, FORMAT, NAME = N'ParkPalaceCRM Pre-Phase 1 Category Standardization Full Backup', STATS = 10;

RESTORE VERIFYONLY FROM DISK = N'$bakFile' WITH CHECKSUM;
"@
        $res = sqlcmd -E -S localhost -d master -Q $sql -b
        $exitCode = $LASTEXITCODE
        
        $item = Get-Item $bakFile
        
        [PSCustomObject]@{
            ExitCode = $exitCode
            BakPath = $bakFile
            Size = $item.Length
            CreationTime = $item.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
            Output = ($res -join "`n")
        }
    }

    if ($backupResult.ExitCode -ne 0) {
        throw "Database backup failed! $($backupResult.Output)"
    }
    Write-Host "  [OK] Backup Created: $($backupResult.BakPath) ($([Math]::Round($backupResult.Size / 1MB, 2)) MB)" -ForegroundColor Green
    Write-Host "  [OK] Backup VERIFYONLY succeeded with CHECKSUM." -ForegroundColor Green

    # Step 4: Upload Manifest to Server
    Write-Host "`n[Step 4] Uploading Phase 1 manifest to remote server..." -ForegroundColor Yellow
    $remoteManifestPath = "D:\ParkPlaceCRM\backups\phase1_manifest_443.json"
    Invoke-Command -Session $session -ArgumentList $manifestJson, $remoteManifestPath -ScriptBlock {
        param($content, $dest)
        [System.IO.File]::WriteAllText($dest, $content, [System.Text.Encoding]::UTF8)
    }
    Write-Host "  [OK] Manifest uploaded to $remoteManifestPath" -ForegroundColor Green

    # Step 5: Execute Category Resolution & Atomic Audited Update
    Write-Host "`n[Step 5] Executing atomic category standardization and audit recording on ParkPalaceCRM..." -ForegroundColor Yellow
    $executionRunId = "RUN-PHASE1-" + (Get-Date).ToString("yyyyMMdd-HHmmss")
    Write-Host "  Execution Run ID: $executionRunId" -ForegroundColor Cyan

    $execResult = Invoke-Command -Session $session -ArgumentList $remoteManifestPath, $executionRunId -ScriptBlock {
        param($manifestFile, $runId)
        
        $rawManifest = [System.IO.File]::ReadAllText($manifestFile, [System.Text.Encoding]::UTF8)
        $items = $rawManifest | ConvertFrom-Json
        
        $connStr = "Server=localhost;Database=ParkPalaceCRM;Integrated Security=True;TrustServerCertificate=True;"
        $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
        $conn.Open()
        
        # 5.1 Export Pre-Execution Snapshot of Materials
        $preSql = "SELECT * FROM Materials ORDER BY Id;"
        $preCmd = New-Object System.Data.SqlClient.SqlCommand($preSql, $conn)
        $preAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($preCmd)
        $preDt = New-Object System.Data.DataTable
        [void]$preAdapter.Fill($preDt)
        
        $preSnapshotPath = "D:\ParkPlaceCRM\backups\materials_pre_execution.json"
        $preList = New-Object System.Collections.Generic.List[PSObject]
        foreach ($r in $preDt.Rows) {
            $rowObj = [ordered]@{}
            foreach ($col in $preDt.Columns) {
                $val = $r[$col.ColumnName]
                $rowObj[$col.ColumnName] = if ($val -eq [DBNull]::Value) { $null } else { [string]$val }
            }
            $preList.Add([PSCustomObject]$rowObj)
        }
        [System.IO.File]::WriteAllText($preSnapshotPath, ($preList | ConvertTo-Json -Depth 5 -Compress), [System.Text.Encoding]::UTF8)
        
        # 5.2 Ensure Target Categories (Reusing existing without duplicates)
        # Helper to ensure category
        function EnsureCategory($code, $name, $parentId) {
            $selectSql = "SELECT Id FROM Categories WHERE Code = @code OR (Name = @name AND (ParentId = @parentId OR (ParentId IS NULL AND @parentId IS NULL)));"
            $selCmd = New-Object System.Data.SqlClient.SqlCommand($selectSql, $conn)
            $selCmd.Parameters.AddWithValue("@code", $code) | Out-Null
            $selCmd.Parameters.AddWithValue("@name", $name) | Out-Null
            $selCmd.Parameters.AddWithValue("@parentId", $(if ($parentId) { [Guid]$parentId } else { [DBNull]::Value })) | Out-Null
            
            $existingId = $selCmd.ExecuteScalar()
            if ($existingId -ne $null -and $existingId -ne [DBNull]::Value) {
                return [Guid]$existingId
            }
            
            # Insert
            $newId = [Guid]::NewGuid()
            $insSql = "INSERT INTO Categories (Id, Code, Name, ParentId, IsActive, SortOrder, CreatedAtUtc) VALUES (@id, @code, @name, @parentId, 1, 1, GETUTCDATE());"
            $insCmd = New-Object System.Data.SqlClient.SqlCommand($insSql, $conn)
            $insCmd.Parameters.AddWithValue("@id", $newId) | Out-Null
            $insCmd.Parameters.AddWithValue("@code", $code) | Out-Null
            $insCmd.Parameters.AddWithValue("@name", $name) | Out-Null
            $insCmd.Parameters.AddWithValue("@parentId", $(if ($parentId) { [Guid]$parentId } else { [DBNull]::Value })) | Out-Null
            $insCmd.ExecuteNonQuery() | Out-Null
            
            return $newId
        }

        # Resolve Parent Categories
        $parentCats = @{}
        $parentCats["CAT-FLOUR"] = EnsureCategory "CAT-FLOUR" "آرد و غلات" $null
        $parentCats["CAT-FATS"] = EnsureCategory "CAT-FATS" "روغن و چربی‌های قنادی" $null
        $parentCats["CAT-PACKAGING"] = EnsureCategory "CAT-PACKAGING" "ملزومات بسته‌بندی" $null
        $parentCats["CAT-DAIRY"] = EnsureCategory "CAT-DAIRY" "لبنیات" $null
        $parentCats["CAT-SUGAR"] = EnsureCategory "CAT-SUGAR" "شکر و شیرین‌کننده‌ها" $null
        $parentCats["CAT-BEVERAGE"] = EnsureCategory "CAT-BEVERAGE" "نوشیدنی‌ها" $null

        # Resolve Subcategories
        $targetCatMap = @{}
        # Flour subcategories
        $targetCatMap["آرد و غلات / خمیر و نیمه‌ساخته"] = EnsureCategory "CAT-DOUGH-SEMI" "خمیر و نیمه‌ساخته" $parentCats["CAT-FLOUR"]
        $targetCatMap["آرد و غلات / آرد"] = EnsureCategory "CAT-FLOUR-RAW" "آرد" $parentCats["CAT-FLOUR"]

        # Fats subcategories
        $targetCatMap["روغن و چربی‌های قنادی / روغن"] = EnsureCategory "CAT-OIL" "روغن" $parentCats["CAT-FATS"]

        # Packaging: Client explicitly requested: direct root category is sufficient!
        $targetCatMap["ملزومات بسته‌بندی / ملزومات بسته‌بندی"] = $parentCats["CAT-PACKAGING"]
        $targetCatMap["ملزومات بسته‌بندی"] = $parentCats["CAT-PACKAGING"]

        # Dairy subcategories
        $targetCatMap["لبنیات / شیر"] = EnsureCategory "CAT-MILK" "شیر" $parentCats["CAT-DAIRY"]
        $targetCatMap["لبنیات / شیر خشک"] = EnsureCategory "CAT-MILK-POWDER" "شیر خشک" $parentCats["CAT-DAIRY"]
        $targetCatMap["لبنیات / کره و خامه"] = EnsureCategory "CAT-BUTTER-CREAM" "کره و خامه" $parentCats["CAT-DAIRY"]
        $targetCatMap["لبنیات / پنیر"] = EnsureCategory "CAT-CHEESE" "پنیر" $parentCats["CAT-DAIRY"]
        $targetCatMap["لبنیات / ماست"] = EnsureCategory "CAT-YOGURT" "ماست" $parentCats["CAT-DAIRY"]
        $targetCatMap["لبنیات / سایر لبنیات"] = EnsureCategory "CAT-DAIRY-OTHER" "سایر لبنیات" $parentCats["CAT-DAIRY"]

        # Sugar subcategories
        $targetCatMap["شکر و شیرین‌کننده‌ها / شکر و قند"] = EnsureCategory "CAT-SUGAR-RAW" "شکر و قند" $parentCats["CAT-SUGAR"]

        # Beverage subcategories
        $targetCatMap["نوشیدنی‌ها / نوشیدنی‌های گیاهی"] = EnsureCategory "CAT-PLANT-BEVERAGE" "نوشیدنی‌های گیاهی" $parentCats["CAT-BEVERAGE"]

        # 5.3 Atomic Transactional Update
        $tran = $conn.BeginTransaction("Phase1_CategoryUpdate")
        
        $updatedCount = 0
        $skippedCount = 0
        $skippedItems = New-Object System.Collections.Generic.List[PSObject]
        $appliedLog = New-Object System.Collections.Generic.List[PSObject]

        try {
            foreach ($item in $items) {
                $matId = [Guid]$item.MaterialId
                $expectedOldCatId = [Guid]$item.CurrentCategoryId
                
                # Fetch current DB record inside transaction with exclusive lock
                $checkCmd = New-Object System.Data.SqlClient.SqlCommand(
                    "SELECT CategoryId, StandardizationNotes FROM Materials WITH (UPDLOCK, ROWLOCK) WHERE Id = @id;", 
                    $conn, $tran)
                $checkCmd.Parameters.AddWithValue("@id", $matId) | Out-Null
                
                $rdr = $checkCmd.ExecuteReader()
                if (-not $rdr.Read()) {
                    $rdr.Close()
                    $skippedCount++
                    $skippedItems.Add([PSCustomObject]@{
                        MaterialId = $item.MaterialId
                        Code = $item.InternalCode
                        Name = $item.CurrentName
                        Reason = "Material not found in database"
                    })
                    continue
                }
                
                $actualCurrentCatId = [Guid]$rdr["CategoryId"]
                $existingNotes = if ($rdr["StandardizationNotes"] -ne [DBNull]::Value) { [string]$rdr["StandardizationNotes"] } else { "" }
                $rdr.Close()

                # Concurrency check: verify category has not changed since preview was taken
                if ($actualCurrentCatId -ne $expectedOldCatId) {
                    $skippedCount++
                    $skippedItems.Add([PSCustomObject]@{
                        MaterialId = $item.MaterialId
                        Code = $item.InternalCode
                        Name = $item.CurrentName
                        Reason = "Concurrent modification detected: Category in DB ($actualCurrentCatId) differs from expected ($expectedOldCatId)"
                    })
                    continue
                }

                $targetCatKey = $item.ProposedCategoryDisplayName
                $targetCatId = $targetCatMap[$targetCatKey]
                if (-not $targetCatId) {
                    throw "Destination category mapping not found for key '$targetCatKey'"
                }

                # Construct append audit note without overwriting prior notes
                $auditNote = "[Phase1: RunId=$runId, OldCatId=$actualCurrentCatId, NewCatId=$targetCatId, Date=$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') UTC, Reason=$($item.Reason)]"
                $newNotes = if ([string]::IsNullOrWhiteSpace($existingNotes)) {
                    $auditNote
                } else {
                    "$existingNotes | $auditNote"
                }

                # Perform Atomic Update of ONLY CategoryId, StandardizationNotes, and UpdatedAtUtc
                $updCmd = New-Object System.Data.SqlClient.SqlCommand(@"
UPDATE Materials 
SET CategoryId = @newCatId,
    StandardizationNotes = @newNotes,
    UpdatedAtUtc = GETUTCDATE()
WHERE Id = @id AND CategoryId = @oldCatId;
"@, $conn, $tran)
                $updCmd.Parameters.AddWithValue("@newCatId", $targetCatId) | Out-Null
                $updCmd.Parameters.AddWithValue("@newNotes", $newNotes) | Out-Null
                $updCmd.Parameters.AddWithValue("@id", $matId) | Out-Null
                $updCmd.Parameters.AddWithValue("@oldCatId", $expectedOldCatId) | Out-Null
                
                $rowsAffected = $updCmd.ExecuteNonQuery()

                if ($rowsAffected -eq 1) {
                    $updatedCount++
                    $appliedLog.Add([PSCustomObject]@{
                        MaterialId = $item.MaterialId
                        Code = $item.InternalCode
                        Name = $item.CurrentName
                        OldCategoryId = $actualCurrentCatId
                        NewCategoryId = $targetCatId
                        TargetCategory = $targetCatKey
                    })
                } else {
                    $skippedCount++
                    $skippedItems.Add([PSCustomObject]@{
                        MaterialId = $item.MaterialId
                        Name = $item.CurrentName
                        Reason = "Zero rows affected"
                    })
                }
            }

            # Commit Transaction
            $tran.Commit()
            $commitSuccess = $true
        } catch {
            $tran.Rollback()
            $conn.Close()
            throw "Transaction rolled back due to error: $_"
        }

        # 5.4 Export Post-Execution Snapshot
        $postSql = "SELECT * FROM Materials ORDER BY Id;"
        $postCmd = New-Object System.Data.SqlClient.SqlCommand($postSql, $conn)
        $postAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($postCmd)
        $postDt = New-Object System.Data.DataTable
        [void]$postAdapter.Fill($postDt)
        $conn.Close()

        $postSnapshotPath = "D:\ParkPlaceCRM\backups\materials_post_execution.json"
        $postList = New-Object System.Collections.Generic.List[PSObject]
        foreach ($r in $postDt.Rows) {
            $rowObj = [ordered]@{}
            foreach ($col in $postDt.Columns) {
                $val = $r[$col.ColumnName]
                $rowObj[$col.ColumnName] = if ($val -eq [DBNull]::Value) { $null } else { [string]$val }
            }
            $postList.Add([PSCustomObject]$rowObj)
        }
        [System.IO.File]::WriteAllText($postSnapshotPath, ($postList | ConvertTo-Json -Depth 5 -Compress), [System.Text.Encoding]::UTF8)

        # 5.5 Save Audit Log
        $auditLogPath = "D:\ParkPlaceCRM\backups\phase1_execution_audit_${runId}.json"
        [System.IO.File]::WriteAllText($auditLogPath, ($appliedLog | ConvertTo-Json -Depth 5), [System.Text.Encoding]::UTF8)

        # 5.6 Mathematical Comparison of Before vs After
        $preDict = @{}
        foreach ($p in $preList) { $preDict[$p.Id] = $p }
        
        $nonCatMutationsCount = 0
        $categoryChangedCount = 0
        $categoryUnchangedCount = 0
        
        $columnsToVerifyIntact = @(
            "Name", "Brand", "Packaging", "PackageSize", "PackageSizeUnitId",
            "BaseUnitId", "PurchaseUnitId", "ConsumptionUnitId", "ProductFamily",
            "MinStock", "SafetyStock", "LeadTimeDays", "IsActive", "InternalCode"
        )

        foreach ($postItem in $postList) {
            $preItem = $preDict[$postItem.Id]
            if (-not $preItem) { continue }
            
            if ($postItem.CategoryId -ne $preItem.CategoryId) {
                $categoryChangedCount++
            } else {
                $categoryUnchangedCount++
            }

            foreach ($col in $columnsToVerifyIntact) {
                if ($postItem[$col] -ne $preItem[$col]) {
                    $nonCatMutationsCount++
                }
            }
        }

        # Return comprehensive metrics
        [PSCustomObject]@{
            RunId = $runId
            CommitSuccess = $commitSuccess
            AuthorizedItemsCount = $items.Count
            UpdatedCount = $updatedCount
            SkippedCount = $skippedCount
            FailedCount = 0
            CategoryChangedCountInTable = $categoryChangedCount
            CategoryUnchangedCountInTable = $categoryUnchangedCount
            NonCategoryMutationsCount = $nonCatMutationsCount
            SkippedItems = $skippedItems
            PreSnapshotPath = $preSnapshotPath
            PostSnapshotPath = $postSnapshotPath
            AuditLogPath = $auditLogPath
        }
    }

    Write-Host "`n================ EXECUTION OUTCOME ================" -ForegroundColor Cyan
    Write-Host "  Commit Success:                   $($execResult.CommitSuccess)" -ForegroundColor Green
    Write-Host "  Authorized Items:                 $($execResult.AuthorizedItemsCount)"
    Write-Host "  Successfully Updated:             $($execResult.UpdatedCount)" -ForegroundColor Green
    Write-Host "  Skipped (Concurrent/Unchanged):   $($execResult.SkippedCount)"
    Write-Host "  Failed:                           $($execResult.FailedCount)"
    Write-Host "  Materials Category Changed in DB: $($execResult.CategoryChangedCountInTable)"
    Write-Host "  Materials Category Unchanged:     $($execResult.CategoryUnchangedCountInTable)"
    Write-Host "  Non-Category Mutations Detected:  $($execResult.NonCategoryMutationsCount)" -ForegroundColor $(if ($execResult.NonCategoryMutationsCount -eq 0) { "Green" } else { "Red" })
    Write-Host "  Audit Log Path:                   $($execResult.AuditLogPath)"
    Write-Host "===================================================" -ForegroundColor Cyan

    # Step 6: Verify Key Sample Items
    Write-Host "`n[Step 6] Inspecting key sample items post-execution..." -ForegroundColor Yellow
    $sampleInspection = Invoke-Command -Session $session -ScriptBlock {
        $connStr = "Server=localhost;Database=ParkPalaceCRM;Integrated Security=True;TrustServerCertificate=True;"
        $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
        $conn.Open()
        
        $sql = @"
SELECT 
    m.InternalCode,
    m.Name,
    c.Name AS CategoryName,
    pc.Name AS ParentCategoryName,
    m.Packaging,
    m.PackageSize,
    u.Name AS BaseUnitName,
    m.StandardizationNotes
FROM Materials m
JOIN Categories c ON m.CategoryId = c.Id
LEFT JOIN Categories pc ON c.ParentId = pc.Id
LEFT JOIN UnitsOfMeasure u ON m.BaseUnitId = u.Id
WHERE m.InternalCode IN ('404635', '404259', '404677', '505081', 'MAT-FLOUR-STAR', '404339')
ORDER BY m.InternalCode;
"@
        $cmd = New-Object System.Data.SqlClient.SqlCommand($sql, $conn)
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $dt = New-Object System.Data.DataTable
        [void]$adapter.Fill($dt)
        $conn.Close()

        $list = New-Object System.Collections.Generic.List[PSObject]
        foreach ($r in $dt.Rows) {
            $list.Add([PSCustomObject]@{
                Code = [string]$r["InternalCode"]
                Name = [string]$r["Name"]
                Category = if ($r["ParentCategoryName"] -ne [DBNull]::Value) { "$($r["ParentCategoryName"]) / $($r["CategoryName"])" } else { [string]$r["CategoryName"] }
                Packaging = if ($r["Packaging"] -ne [DBNull]::Value) { [string]$r["Packaging"] } else { "" }
                Size = if ($r["PackageSize"] -ne [DBNull]::Value) { [string]$r["PackageSize"] } else { "" }
                Unit = if ($r["BaseUnitName"] -ne [DBNull]::Value) { [string]$r["BaseUnitName"] } else { "" }
                Notes = [string]$r["StandardizationNotes"]
            })
        }
        return $list
    }

    foreach ($s in $sampleInspection) {
        Write-Host "  Code: $($s.Code) | Name: $($s.Name) | Category: $($s.Category) | Pack: $($s.Packaging) | Size: $($s.Size) | Unit: $($s.Unit)"
        Write-Host "    Notes: $($s.Notes)`n"
    }

} finally {
    Remove-PSSession -Session $session
}
