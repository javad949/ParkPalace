<#
.SYNOPSIS
    Unit test to prove that SqlDataReader/DbDataReader ordinal extraction
    distinctly preserves True, False, 0, and NULL without type coercion bugs.
#>

$ErrorActionPreference = 'Stop'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ParkPalace - آزمون محلی تبدیل مقادیر SqlDataReader به CSV" -ForegroundColor Cyan
Write-Host " تست تمایز قطعی مقادیر True, False, 0 و NULL" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ساخت یک جدول در حافظه با ستون‌های انواع مختلف داده
$table = New-Object System.Data.DataTable
[void]$table.Columns.Add("RowId", [int])
[void]$table.Columns.Add("IsActiveBool", [bool])
[void]$table.Columns.Add("QuantityInt", [int])
[void]$table.Columns.Add("NullableString", [string])
[void]$table.Columns.Add("NullableInt", [int])

# ردیف ۱: IsActive=True, Quantity=0, NullableString=NULL, NullableInt=NULL
$r1 = $table.NewRow()
$r1["RowId"] = 1
$r1["IsActiveBool"] = $true
$r1["QuantityInt"] = 0
$r1["NullableString"] = [System.DBNull]::Value
$r1["NullableInt"] = [System.DBNull]::Value
$table.Rows.Add($r1)

# ردیف ۲: IsActive=False, Quantity=15, NullableString="تست فارسی", NullableInt=0
$r2 = $table.NewRow()
$r2["RowId"] = 2
$r2["IsActiveBool"] = $false
$r2["QuantityInt"] = 15
$r2["NullableString"] = "تست فارسی"
$r2["NullableInt"] = 0
$table.Rows.Add($r2)

# ردیف ۳: IsActive=False, Quantity=0, NullableString="", NullableInt=100
$r3 = $table.NewRow()
$r3["RowId"] = 3
$r3["IsActiveBool"] = $false
$r3["QuantityInt"] = 0
$r3["NullableString"] = ""
$r3["NullableInt"] = 100
$table.Rows.Add($r3)

# اجرای الگوریتم اصلاح‌شده خواندن از DataReader با IsDBNull(ordinal)
$reader = $table.CreateDataReader()
$colNames = @()
for ($i = 0; $i -lt $reader.FieldCount; $i++) {
    $colNames += $reader.GetName($i)
}

$extractedRows = New-Object System.Collections.Generic.List[PSObject]

while ($reader.Read()) {
    $rowObj = [ordered]@{}
    for ($i = 0; $i -lt $reader.FieldCount; $i++) {
        $col = $colNames[$i]
        if ($reader.IsDBNull($i)) {
            $rowObj[$col] = "" # مقدار NULL به‌صورت صریح رشته خالی در CSV می‌شود
        } else {
            $val = $reader.GetValue($i)
            if ($val -is [bool]) {
                # تمایز کامل بولی True و False بدون تأثیرپذیری از تبدیل شیء DBNull
                $rowObj[$col] = if ($val) { "True" } else { "False" }
            } else {
                # حفظ عدد 0، اعداد مثبت و رشته‌ها
                $rowObj[$col] = $val.ToString()
            }
        }
    }
    $extractedRows.Add([PSCustomObject]$rowObj)
}
$reader.Close()

# بررسی صحت مقادیر
$row1 = $extractedRows[0]
$row2 = $extractedRows[1]
$row3 = $extractedRows[2]

Write-Host "`n[نتایج تبدیل سطرهای تستی]:" -ForegroundColor Yellow
$extractedRows | Format-Table -AutoSize

# اعتبارسنجی تمایز
$passTrue = ($row1.IsActiveBool -eq "True")
$passFalse = ($row2.IsActiveBool -eq "False" -and $row3.IsActiveBool -eq "False")
$passZero = ($row1.QuantityInt -eq "0" -and $row2.NullableInt -eq "0" -and $row3.QuantityInt -eq "0")
$passNull = ($row1.NullableString -eq "" -and $row1.NullableInt -eq "")

Write-Host "`n[راستی‌آزمایی شرایط چهارگانه]:" -ForegroundColor Cyan
Write-Host "  1. حفظ مقدار True: $passTrue (مقدار استخراج‌شده: '$($row1.IsActiveBool)')" -ForegroundColor $(if($passTrue){"Green"}else{"Red"})
Write-Host "  2. حفظ مقدار False: $passFalse (مقدار استخراج‌شده: '$($row2.IsActiveBool)')" -ForegroundColor $(if($passFalse){"Green"}else{"Red"})
Write-Host "  3. حفظ مقدار عدد صفر: $passZero (مقدار استخراج‌شده: '$($row1.QuantityInt)')" -ForegroundColor $(if($passZero){"Green"}else{"Red"})
Write-Host "  4. حفظ مقدار NULL به عنوان خالی: $passNull (مقدار استخراج‌شده: '$($row1.NullableString)')" -ForegroundColor $(if($passNull){"Green"}else{"Red"})

if ($passTrue -and $passFalse -and $passZero -and $passNull) {
    Write-Host "`n[PASS] تمامی شرایط با موفقیت تأیید شدند؛ تمایز قطعی True, False, 0 و NULL برقرار است." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n[FAIL] آزمون با شکست مواجه شد." -ForegroundColor Red
    exit 1
}
