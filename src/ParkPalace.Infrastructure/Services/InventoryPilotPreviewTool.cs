using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;

namespace ParkPalace.Infrastructure.Services;

public sealed record PilotPreviewOptions(
    string WarehouseCode = "WH-MAIN",
    string SourceWarehouseId = "1",
    string FiscalYearTitle = "1405",
    string? OutputJsonPath = null,
    string? OutputReportPath = null,
    string DataProvenance = "SIMULATED_TEST",
    bool SemanticContractVerified = false,
    string? RunId = null,
    string? BinaryHash = null,
    string? ConfigFilePath = null);

public sealed record PilotPreviewExecutionResult(
    bool IsExecutable,
    string? FailureReason,
    string? ManifestHash,
    string? PlanHash,
    DateTime? CapturedAtUtc,
    SepidarFiscalYearInfo? FiscalYear,
    int ManifestItemCount,
    int ExcludedCandidateCount,
    int SnapshotInsertedCount,
    int SnapshotModifiedCount,
    int SnapshotUnchangedCount,
    int MovementInsertedCount,
    int MovementModifiedCount,
    int MovementUnchangedCount,
    int ExcludedStockRows,
    int ExcludedMovementRows,
    IReadOnlyList<PilotItemExclusion> ExcludedCandidates,
    IReadOnlyList<PilotSnapshotChange> SnapshotChanges,
    IReadOnlyList<PilotMovementChange> MovementChanges,
    string HumanReadableReport,
    string JsonOutput);

/// <summary>
/// Manual read-only preview tool for the limited inventory pilot.
/// Consumes SepidarPilotReader, PilotManifestExtractor, and InventoryPilotPlanner.
/// Strictly executes in PREVIEW mode; NO apply or database write path is available.
/// </summary>
public static class InventoryPilotPreviewTool
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task<PilotPreviewExecutionResult> GeneratePreviewAsync(
        CrmDbContext crmDb,
        ISepidarPilotReader sepidarReader,
        PilotPreviewOptions? options = null,
        CancellationToken ct = default)
    {
        var opt = options ?? new PilotPreviewOptions();

        // 0. Pre-validate output paths: reject existing files and sensitive targets immediately
        ValidateOutputPath(opt.OutputJsonPath, opt.ConfigFilePath);
        ValidateOutputPath(opt.OutputReportPath, opt.ConfigFilePath);

        // 1. Capture and verify source data from Sepidar
        var captureResult = await sepidarReader.CaptureAsync(
            opt.SourceWarehouseId,
            opt.FiscalYearTitle,
            ct);

        if (captureResult.FiscalYear == null || captureResult.Capture == null)
        {
            var unexecutableReport = BuildUnexecutableReport(captureResult.FailureReason ?? "خطای ناشناخته در برداشت مبدأ", opt);
            var unexecutableJson = JsonSerializer.Serialize(new
            {
                Status = "UNEXECUTABLE",
                FailureReason = captureResult.FailureReason,
                WarehouseCode = opt.WarehouseCode,
                SourceWarehouseId = opt.SourceWarehouseId,
                TargetFiscalYearTitle = opt.FiscalYearTitle
            }, JsonOpts);

            SafeWriteOutputs(opt, unexecutableJson, unexecutableReport);

            return new PilotPreviewExecutionResult(
                IsExecutable: false,
                FailureReason: captureResult.FailureReason,
                ManifestHash: null,
                PlanHash: null,
                CapturedAtUtc: null,
                FiscalYear: null,
                ManifestItemCount: 0,
                ExcludedCandidateCount: 0,
                SnapshotInsertedCount: 0,
                SnapshotModifiedCount: 0,
                SnapshotUnchangedCount: 0,
                MovementInsertedCount: 0,
                MovementModifiedCount: 0,
                MovementUnchangedCount: 0,
                ExcludedStockRows: 0,
                ExcludedMovementRows: 0,
                ExcludedCandidates: Array.Empty<PilotItemExclusion>(),
                SnapshotChanges: Array.Empty<PilotSnapshotChange>(),
                MovementChanges: Array.Empty<PilotMovementChange>(),
                HumanReadableReport: unexecutableReport,
                JsonOutput: unexecutableJson);
        }

        var fy = captureResult.FiscalYear;
        var sourceCapture = captureResult.Capture;

        // 2. Extract proposed manifest from CRM
        var manifestResult = await PilotManifestExtractor.ExtractAsync(
            crmDb,
            fy.FiscalYearId,
            fy.StartDate,
            fy.PeriodEndExclusive,
            opt.WarehouseCode,
            opt.SourceWarehouseId,
            ct);

        if (manifestResult.Manifest == null || manifestResult.Manifest.Items.Count == 0)
        {
            var reason = "PILOT_NO_VALID_ITEMS: هیچ قلم کالای معتبر و سازگاری با معیارهای پایلوت کیلوگرم یافت نشد.";
            var unexecReport = BuildUnexecutableReport(reason, opt, manifestResult.ExcludedItems);
            var unexecJson = JsonSerializer.Serialize(new
            {
                Status = "UNEXECUTABLE",
                FailureReason = reason,
                FiscalYear = fy,
                ExcludedItems = manifestResult.ExcludedItems
            }, JsonOpts);

            SafeWriteOutputs(opt, unexecJson, unexecReport);

            return new PilotPreviewExecutionResult(
                IsExecutable: false,
                FailureReason: reason,
                ManifestHash: null,
                PlanHash: null,
                CapturedAtUtc: captureResult.CapturedAtUtc,
                FiscalYear: fy,
                ManifestItemCount: 0,
                ExcludedCandidateCount: manifestResult.ExcludedItems.Count,
                SnapshotInsertedCount: 0,
                SnapshotModifiedCount: 0,
                SnapshotUnchangedCount: 0,
                MovementInsertedCount: 0,
                MovementModifiedCount: 0,
                MovementUnchangedCount: 0,
                ExcludedStockRows: 0,
                ExcludedMovementRows: 0,
                ExcludedCandidates: manifestResult.ExcludedItems,
                SnapshotChanges: Array.Empty<PilotSnapshotChange>(),
                MovementChanges: Array.Empty<PilotMovementChange>(),
                HumanReadableReport: unexecReport,
                JsonOutput: unexecJson);
        }

        var manifest = manifestResult.Manifest;

        // 2.1 Filter out manifest items that have ANY unsupported movements in source capture.
        // NOTE: Unsupported movements belonging to items OUTSIDE the manifest do NOT fail the pilot;
        // they are simply excluded from the pilot scope (ExcludedMovementRows).
        var unsupportedMovementsByItem = sourceCapture.Movements
            .Where(m => m.MovementType == InventoryMovementType.Unknown || (int)m.MovementType <= 0)
            .GroupBy(m => m.ItemRef, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var combinedExclusions = new List<PilotItemExclusion>(manifestResult.ExcludedItems);
        var filteredItems = new List<PilotItem>();

        foreach (var itm in manifest.Items)
        {
            if (unsupportedMovementsByItem.TryGetValue(itm.SourceItemId, out var unsuppMovs))
            {
                var vouchers = string.Join(", ", unsuppMovs.Select(v => v.VoucherNumber).Distinct());
                combinedExclusions.Add(new PilotItemExclusion(
                    itm.SourceItemId,
                    itm.MaterialId,
                    null,
                    null,
                    "ITEM_HAS_UNSUPPORTED_MOVEMENT",
                    $"کالا دارای {unsuppMovs.Count} گردش با نوع پشتیبانی‌نشده (شماره سندها: {vouchers}) است. برای جلوگیری از ثبت کاردکس ناقص، کل قلم از ورود به پایلوت کنار گذاشته شد."));
            }
            else
            {
                filteredItems.Add(itm);
            }
        }

        if (filteredItems.Count == 0)
        {
            var reason = "PILOT_NO_VALID_ITEMS: تمامی اقلام به دلیل نداشتن شرایط اولیه یا داشتن گردش‌های پشتیبانی‌نشده از پایلوت کنار گذاشته شدند.";
            var unexecReport = BuildUnexecutableReport(reason, opt, combinedExclusions);
            var unexecJson = JsonSerializer.Serialize(new
            {
                Status = "UNEXECUTABLE",
                FailureReason = reason,
                FiscalYear = fy,
                ExcludedItems = combinedExclusions
            }, JsonOpts);

            SafeWriteOutputs(opt, unexecJson, unexecReport);

            return new PilotPreviewExecutionResult(
                IsExecutable: false,
                FailureReason: reason,
                ManifestHash: null,
                PlanHash: null,
                CapturedAtUtc: captureResult.CapturedAtUtc,
                FiscalYear: fy,
                ManifestItemCount: 0,
                ExcludedCandidateCount: combinedExclusions.Count,
                SnapshotInsertedCount: 0,
                SnapshotModifiedCount: 0,
                SnapshotUnchangedCount: 0,
                MovementInsertedCount: 0,
                MovementModifiedCount: 0,
                MovementUnchangedCount: 0,
                ExcludedStockRows: 0,
                ExcludedMovementRows: 0,
                ExcludedCandidates: combinedExclusions,
                SnapshotChanges: Array.Empty<PilotSnapshotChange>(),
                MovementChanges: Array.Empty<PilotMovementChange>(),
                HumanReadableReport: unexecReport,
                JsonOutput: unexecJson);
        }

        manifest = manifest with { Items = filteredItems };
        var itemIds = manifest.Items.Select(i => i.MaterialId).ToArray();

        // 3. Read current baseline CRM snapshots and movements with AsNoTracking (strictly read-only)
        var crmSnapshots = await crmDb.InventorySnapshots.AsNoTracking()
            .Where(s => s.WarehouseId == manifest.WarehouseId && itemIds.Contains(s.MaterialId))
            .ToListAsync(ct);

        var crmMovements = await crmDb.InventoryMovements.AsNoTracking()
            .Where(m => m.SourceMovementKey != null)
            .ToListAsync(ct);

        var baselineSnapshots = crmSnapshots.Select(InventoryPilotExecutor.SnapshotState).ToArray();
        var baselineMovements = crmMovements.Select(InventoryPilotExecutor.MovementState).ToArray();

        // 4. Execute deterministic planner kernel
        var plan = InventoryPilotPlanner.Build(
            manifest,
            sourceCapture,
            baselineSnapshots,
            baselineMovements);

        // 5. Compute statistics
        var manifestItemKeys = manifest.Items.Select(i => i.SourceItemId).ToHashSet(StringComparer.Ordinal);
        var inScopeMovements = sourceCapture.Movements.Where(m => manifestItemKeys.Contains(m.ItemRef)).ToArray();

        int snapshotModified = plan.Snapshots.Count;
        int snapshotUnchanged = manifest.Items.Count - plan.Snapshots.Count;
        int movementInserted = plan.Movements.Count(m => m.Before == null);
        int movementModified = plan.Movements.Count(m => m.Before != null);
        int movementUnchanged = inScopeMovements.Length - plan.Movements.Count;

        // 6. Evaluate semantic contract validity and transactional consistency
        // STRICT SAFETY ENFORCEMENT:
        // As long as the real Sepidar contract is unverified and not implemented in code,
        // live output MUST ALWAYS remain IsExecutable = false.
        // For discrete CSV samples, IsConsistent = false guarantees output is non-executable.
        bool isDatabaseReader = sepidarReader is SepidarPilotReader;
        bool isSimulatedProvenance = opt.DataProvenance.Equals("SIMULATED_TEST", StringComparison.OrdinalIgnoreCase);

        // Executable is STRICTLY allowed ONLY for purely simulated in-memory test readers with SIMULATED_TEST provenance and consistent snapshot.
        bool isExecutable = captureResult.IsConsistent && !isDatabaseReader && isSimulatedProvenance;

        string? failureReason = null;
        if (!isExecutable)
        {
            var reasons = new List<string>();
            if (!captureResult.IsConsistent)
            {
                reasons.Add(captureResult.FailureReason ?? "PILOT_TRANSACTIONAL_SNAPSHOT_UNVERIFIED: سازگاری تراکنشی اسنپ‌شات تأییدنشده است.");
            }

            if (isDatabaseReader && isSimulatedProvenance)
            {
                reasons.Add("PILOT_SEMANTIC_CONTRACT_UNVERIFIED: منبع داده ریدر دیتابیس (SepidarPilotReader) است؛ تغییر برچسب منشأ داده‌ها به 'SIMULATED_TEST' یا ارسال سوئیچ اعتبارسنجی نمی‌تواند محدودیت ایمنی قرارداد اثبات‌نشده سپیدار را دور بزند.");
            }
            else
            {
                reasons.Add("PILOT_SEMANTIC_CONTRACT_UNVERIFIED: قرارداد واقعی سپیدار (نگاشت انواع سند، مکانیزم ابطال، وضعیت حسابداری و مرز تاریخ پایان سال مالی) هنوز در محیط عملیاتی کشف و پیاده‌سازی نشده است. تا زمان کشف میدانی و پیاده‌سازی رسمی در کد، خروجی محیط زنده همواره غیرقابل اجرا (IsExecutable=false) است و ارسال سوئیچ --verify-semantic-contract نمی‌تواند این محدودیت را دور بزند.");
            }

            failureReason = string.Join(" | ", reasons);
        }

        // 7. Detect negative stock discrepancies & compile warnings
        var negativeStockWarnings = plan.Snapshots
            .Where(s => s.After.OnHand < 0)
            .Select(s => {
                var item = manifest.Items.FirstOrDefault(i => i.MaterialId == s.Before.MaterialId);
                var srcId = item?.SourceItemId ?? s.Before.MaterialId.ToString();
                return $"NEGATIVE_STOCK_DISCREPANCY: قلم کالا با شناسه مبدأ '{srcId}' دارای موجودی منفی {s.After.OnHand:N4} کیلوگرم است. موجودی منفی بدون اصلاح خودکار یا صفرسازی حفظ گردید؛ مقادیر رزرو ({s.After.Reserved}) و ورودی‌های انتظار ({s.After.Incoming}) و مشخصات بومی CRM دست‌نخورده حفظ شدند.";
            })
            .ToList();

        var combinedWarnings = new List<string>(manifestResult.IncompleteCategoriesWarning);
        combinedWarnings.AddRange(negativeStockWarnings);

        if (!captureResult.IsConsistent)
        {
            combinedWarnings.Add("UNVERIFIED_TRANSACTIONAL_CONSISTENCY: برداشت مبدأ فاقد ایزولاسیون تراکنشی هم‌زمان (Snapshot) است. فایل‌های CSV نمونه حاصل استخراج‌های مستقل در زمان‌های متفاوت بوده و هم‌زمانی داده‌های موجودی با گردش‌ها اثبات نشده است.");
        }

        if (captureResult.MissingMovementData)
        {
            combinedWarnings.Add("MISSING_MOVEMENT_DATA: فایل اقلام گردش انبار (Movements) در ورودی‌ها ارائه نشده است. نبود فایل گردش معادل صفر گردش یا کاردکس کامل نیست. فایل‌های شماره سند نمونه نیز فاقد قلم کالا و مقدار بوده و امکان استنتاج ردیف گردش از آن‌ها وجود ندارد.");
            combinedWarnings.Add("ITEM_360_UNSUPPORTED_RISK_DISCLAIMER: حضور قلم ۳۶۰ (یا سایر اقلام) در پیش‌نمایش جدید، در نبود داده گردش، به هیچ وجه اثبات نداشتن گردش‌های پشتیبانی‌نشده (Unsupported) در واقعیت نیست (در نمونه‌برداری فاز ۲، سند شماره ۱۴۶۹ از نوع ۳ بوده که اقلام آن استخراج نشده است).");
        }

        // 8. Generate structured outputs
        var report = BuildPreviewReport(
            opt,
            fy,
            captureResult.CapturedAtUtc!.Value,
            manifest,
            plan.ManifestHash,
            plan,
            combinedExclusions,
            combinedWarnings,
            negativeStockWarnings,
            snapshotModified,
            snapshotUnchanged,
            movementInserted,
            movementModified,
            movementUnchanged,
            isExecutable,
            failureReason,
            captureResult);

        var json = JsonSerializer.Serialize(new
        {
            Status = isExecutable ? "PREVIEW_SUCCESS" : (captureResult.IsConsistent ? "UNEXECUTABLE_SEMANTIC_CONTRACT_UNVERIFIED" : "UNEXECUTABLE_TRANSACTIONAL_SNAPSHOT_UNVERIFIED"),
            IsExecutable = isExecutable,
            FailureReason = failureReason,
            RunId = opt.RunId,
            BinaryHash = opt.BinaryHash,
            DataProvenance = opt.DataProvenance,
            TransactionalConsistency = captureResult.IsConsistent ? "VERIFIED_SNAPSHOT" : "UNVERIFIED_DISCRETE_CSV_SAMPLES",
            MissingMovementData = captureResult.MissingMovementData,
            SemanticContractVerified = opt.SemanticContractVerified,
            GeneratedAtUtc = DateTime.UtcNow,
            ExecutionMode = "PreviewOnly",
            InputManifest = captureResult.InputManifest,
            WarehouseCode = opt.WarehouseCode,
            SourceWarehouseId = opt.SourceWarehouseId,
            FiscalYear = new
            {
                fy.FiscalYearId,
                fy.Title,
                StartDate = fy.StartDate.ToString("u"),
                EndDate = fy.EndDate.ToString("u")
            },
            Capture = new
            {
                CapturedAtUtc = captureResult.CapturedAtUtc!.Value.ToString("u"),
                TotalStocksInSource = sourceCapture.Stocks.Count,
                TotalMovementsInSource = sourceCapture.Movements.Count,
                MissingMovementData = captureResult.MissingMovementData,
                ExcludedStockRows = plan.ExcludedStockRows,
                ExcludedMovementRows = plan.ExcludedMovementRows
            },
            Manifest = new
            {
                Hash = plan.ManifestHash,
                ItemCount = manifest.Items.Count,
                Items = manifest.Items
            },
            Plan = new
            {
                Hash = plan.PlanHash,
                Snapshots = new
                {
                    ModifiedCount = snapshotModified,
                    UnchangedCount = snapshotUnchanged,
                    Changes = plan.Snapshots
                },
                Movements = new
                {
                    InsertedCount = movementInserted,
                    ModifiedCount = movementModified,
                    UnchangedCount = movementUnchanged,
                    Changes = plan.Movements
                }
            },
            Exclusions = new
            {
                Count = combinedExclusions.Count,
                Items = combinedExclusions
            },
            Warnings = combinedWarnings,
            NegativeStockWarnings = negativeStockWarnings
        }, JsonOpts);

        SafeWriteOutputs(opt, json, report);

        return new PilotPreviewExecutionResult(
            IsExecutable: isExecutable,
            FailureReason: failureReason,
            ManifestHash: manifestResult.ManifestHash,
            PlanHash: plan.PlanHash,
            CapturedAtUtc: captureResult.CapturedAtUtc,
            FiscalYear: fy,
            ManifestItemCount: manifest.Items.Count,
            ExcludedCandidateCount: combinedExclusions.Count,
            SnapshotInsertedCount: 0,
            SnapshotModifiedCount: snapshotModified,
            SnapshotUnchangedCount: snapshotUnchanged,
            MovementInsertedCount: movementInserted,
            MovementModifiedCount: movementModified,
            MovementUnchangedCount: movementUnchanged,
            ExcludedStockRows: plan.ExcludedStockRows,
            ExcludedMovementRows: plan.ExcludedMovementRows,
            ExcludedCandidates: combinedExclusions,
            SnapshotChanges: plan.Snapshots,
            MovementChanges: plan.Movements,
            HumanReadableReport: report,
            JsonOutput: json);
    }

    public static void ValidateOutputPath(string? path, string? sensitiveConfigPath = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var fullPath = Path.GetFullPath(path);

        // 1. Check if file already exists -> Prevent overwriting previous reports!
        if (File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"OUTPUT_FILE_ALREADY_EXISTS: فایل خروجی در مسیر '{fullPath}' از قبل وجود دارد. جهت جلوگیری از نابودی سوابق و تاریخچه، بازنویسی گزارش‌ها مجاز نیست.");
        }

        // 2. Prevent targeting sensitive configuration file
        if (!string.IsNullOrWhiteSpace(sensitiveConfigPath))
        {
            var fullConfigPath = Path.GetFullPath(sensitiveConfigPath);
            if (string.Equals(fullPath, fullConfigPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SENSITIVE_OUTPUT_TARGET_REJECTED: مسیر خروجی '{fullPath}' نمی‌تواند با فایل تنظیمات حساس یکسان باشد.");
            }
        }

        // 3. Prevent targeting sensitive extensions / patterns
        var fileName = Path.GetFileName(fullPath);
        if (IsSensitiveFilename(fileName))
        {
            throw new InvalidOperationException(
                $"SENSITIVE_OUTPUT_TARGET_REJECTED: مسیر خروجی '{fullPath}' نمی‌تواند فایل تنظیمات، کلید امنیتی یا سرتیفیکیت باشد.");
        }
    }

    public static bool IsSensitiveFilename(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var lower = fileName.ToLowerInvariant();
        if (lower.StartsWith("appsettings") && lower.EndsWith(".json")) return true;
        if (lower.EndsWith(".config")) return true;
        if (lower.EndsWith(".cer") || lower.EndsWith(".pfx") || lower.EndsWith(".key") || lower.EndsWith(".crt") || lower.EndsWith(".pem")) return true;
        if (lower == ".env" || lower.StartsWith(".env.")) return true;
        if (lower.StartsWith("secret") || lower.Contains("password") || lower.Contains("credential")) return true;
        return false;
    }

    private static void SafeWriteOutputs(PilotPreviewOptions opt, string json, string report)
    {
        if (!string.IsNullOrWhiteSpace(opt.OutputJsonPath))
        {
            ValidateOutputPath(opt.OutputJsonPath, opt.ConfigFilePath);
            var dir = Path.GetDirectoryName(opt.OutputJsonPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using var fs = new FileStream(opt.OutputJsonPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs, Encoding.UTF8);
            writer.Write(json);
        }

        if (!string.IsNullOrWhiteSpace(opt.OutputReportPath))
        {
            ValidateOutputPath(opt.OutputReportPath, opt.ConfigFilePath);
            var dir = Path.GetDirectoryName(opt.OutputReportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using var fs = new FileStream(opt.OutputReportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs, Encoding.UTF8);
            writer.Write(report);
        }
    }

    private static string BuildUnexecutableReport(string reason, PilotPreviewOptions opt, IReadOnlyList<PilotItemExclusion>? exclusions = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# گزارش پیش‌نمایش پایلوت محدود انبار — وضعیت: غیرقابل اجرا");
        sb.AppendLine();
        sb.AppendLine($"> [!CAUTION]");
        sb.AppendLine($"> **پیش‌نمایش پایلوت غیرقابل اجرا اعلام شد:** {reason}");
        sb.AppendLine();
        sb.AppendLine($"* **انبار هدف:** {opt.WarehouseCode} (انبار مبدأ: {opt.SourceWarehouseId})");
        sb.AppendLine($"* **سال مالی هدف:** {opt.FiscalYearTitle}");
        sb.AppendLine($"* **منشأ داده‌ها:** `{opt.DataProvenance}`");
        sb.AppendLine($"* **زمان بررسی:** {DateTime.UtcNow:u}");
        sb.AppendLine();
        if (exclusions != null && exclusions.Count > 0)
        {
            sb.AppendLine("### اقلام کنار گذاشته‌شده و دلایل رد:");
            sb.AppendLine("| شناسه سپیدار | کد کالا | نام کالا | دلیل رد | توضیحات |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
            foreach (var ex in exclusions)
            {
                sb.AppendLine($"| `{ex.SourceItemId}` | `{ex.MaterialCode ?? "-"}` | {ex.MaterialName ?? "-"} | `{ex.Reason}` | {ex.Details} |");
            }
        }
        return sb.ToString();
    }

    private static string BuildPreviewReport(
        PilotPreviewOptions opt,
        SepidarFiscalYearInfo fy,
        DateTime capturedAtUtc,
        PilotManifest manifest,
        string manifestHash,
        InventoryPilotPlan plan,
        IReadOnlyList<PilotItemExclusion> exclusions,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> negativeStockWarnings,
        int snapshotModified,
        int snapshotUnchanged,
        int movementInserted,
        int movementModified,
        int movementUnchanged,
        bool isExecutable,
        string? failureReason,
        SepidarPilotCaptureResult captureResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# گزارش پیش‌نمایش دستی پایلوت محدود انبار سپیدار (Preview Mode)");
        sb.AppendLine();
        if (opt.DataProvenance == "SIMULATED_TEST")
        {
            sb.AppendLine("> [!WARNING]");
            sb.AppendLine("> **برچسب منشأ داده‌ها: نمونه آزمایشی شبیه‌سازی‌شده (SIMULATED_TEST)**");
            sb.AppendLine("> این پیش‌نمایش بر روی داده‌های آزمایشی محلی تولید شده است و منعکس‌کننده اطلاعات سیستم عملیاتی سپیدار نیست.");
            sb.AppendLine();
        }
        else if (!captureResult.IsConsistent)
        {
            sb.AppendLine("> [!WARNING]");
            sb.AppendLine($"> **منشأ داده‌ها: {opt.DataProvenance} — وضعیت سازگاری تراکنشی: تأییدنشده (UNVERIFIED_SNAPSHOT)**");
            sb.AppendLine("> فایل‌های CSV نمونه حاصل استخراج‌های مجزا در زمان‌های متفاوت هستند و هم‌زمانی تراکنشی آن‌ها در یک تراکنش Snapshot مشترک تضمین نشده است. این گزارش صرفاً جنبه تشخیصی محلی دارد و به هیچ عنوان مبنای Apply یا همگام‌سازی قطعی نیست.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("> [!NOTE]");
            sb.AppendLine($"> **منشأ داده‌ها: {opt.DataProvenance}**");
            sb.AppendLine("> **ارزیابی ایزولاسیون Snapshot:** برداشت تراکنشی با سطح ایزولاسیون Snapshot انجام شده است. این سطح با استفاده از نگارش‌های ردیف در tempdb، از اخذ قفل‌های اشتراکی ردیف/صفحه (S-Locks) در زمان خواندن جلوگیری می‌کند که ریسک بن‌بست‌های معمول خواننده-نویسنده را کاهش می‌دهد. با این حال، قفل‌های تغییر ساختار (Sch-M)، سربار I/O در tempdb و تعارض‌های به‌روزرسانی (Update Conflicts) همزمان کماکان بر عملکرد دیتابیس اثرگذارند و ادعای مطلق مبنی بر عدم مسدودسازی نویسندگان نادرست است.");
            sb.AppendLine();
        }

        if (captureResult.MissingMovementData)
        {
            sb.AppendLine("> [!WARNING]");
            sb.AppendLine("> **هشدار فقدان داده‌های گردش انبار (MissingMovementData):**");
            sb.AppendLine("> فایل اقلام گردش انبار (Movements) در ورودی‌ها موجود نیست. نمونه‌های CSV شماره سند (مانند 02_receipt_document_number_samples.csv و 03_delivery_document_number_samples.csv) صرفاً سربرگ سند هستند و فاقد اطلاعات قلم کالا (ItemRef) و مقدار (Quantity) می‌باشند؛ بنابراین هیچ ردیف گردشی از آن‌ها قابل استنتاج نیست.");
            sb.AppendLine("> - **عدم صفر بودن کاردکس:** نبود فایل گردش به هیچ وجه معادل «صفر گردش» یا «کاردکس کامل» تلقی نمی‌شود؛ کاردکس اقلام در این پیش‌نمایش به صورت قطعی غایب اعلام می‌گردد.");
            sb.AppendLine("> - **سلب مسئولیت قلم ۳۶۰:** وجود قلم ۳۶۰ در این پیش‌نمایش، در نبود داده‌های گردش، به هیچ وجه اثبات‌کننده نداشتن گردش‌های پشتیبانی‌نشده (Unsupported) در واقعیت نیست (در نمونه‌برداری‌های قبلی، سند شماره ۱۴۶۹ از نوع ۳ بوده که اقلام آن نامشخص است).");
            sb.AppendLine();
        }

        if (!isExecutable)
        {
            sb.AppendLine("> [!CAUTION]");
            sb.AppendLine($"> **وضعیت اجرایی پایلوت: غیرقابل اجرا (UNEXECUTABLE)**");
            sb.AppendLine($"> **علت:** {failureReason}");
            sb.AppendLine();
        }

        sb.AppendLine("> [!IMPORTANT]");
        sb.AppendLine("> این گزارش صرفاً در حالت **پیش‌نمایش فقط‌خواندنی (تشخیصی محلی)** تولید شده است. هیچ داده‌ای در دیتابیس CRM یا سپیدار تغییر نکرده و هیچ مسیر Apply در این ابزار تعبیه نشده است.");
        sb.AppendLine();

        if (negativeStockWarnings != null && negativeStockWarnings.Count > 0)
        {
            sb.AppendLine("> [!WARNING]");
            sb.AppendLine("> **هشدار مغایرت موجودی منفی (Negative Stock Discrepancy):**");
            foreach (var negW in negativeStockWarnings)
            {
                sb.AppendLine($"> - ⚠️ {negW}");
            }
            sb.AppendLine("> - **قانون پایلوت:** مقادیر منفی هرگز به صفر رند یا کلمپ (Math.Max(0, ...)) نمی‌شوند. مقادیر رزرو (Reserved)، ورودی‌های مورد انتظار (Incoming) و مشخصات بومی کالا در CRM دست‌نخورده حفظ گردیدند.");
            sb.AppendLine();
        }

        sb.AppendLine("### ۱. مشخصات و محدوده برداشت مبدأ (Source Provenance)");
        sb.AppendLine($"- **منشأ داده‌ها:** `{opt.DataProvenance}`");
        if (!string.IsNullOrWhiteSpace(opt.RunId))
        {
            sb.AppendLine($"- **شناسه یکتای اجرای پایلوت (Run ID):** `{opt.RunId}`");
        }
        if (!string.IsNullOrWhiteSpace(opt.BinaryHash))
        {
            sb.AppendLine($"- **هش باینری اجراشده (Binary SHA256):** `{opt.BinaryHash}`");
        }
        sb.AppendLine($"- **انبار مرکزی CRM:** `{manifest.WarehouseCode}` (شناسه انبار در سپیدار: `{manifest.SourceWarehouseId}`)");
        sb.AppendLine($"- **سال مالی مبدأ:** `{fy.Title}` (شناسه دوره در سپیدار: `{fy.FiscalYearId}`)");
        sb.AppendLine(FormattableString.Invariant($"- **بازه زمانی دوره مالی:** از `{fy.StartDate:yyyy-MM-dd HH:mm:ss} UTC` تا `{fy.EndDate:yyyy-MM-dd HH:mm:ss} UTC`"));
        sb.AppendLine(FormattableString.Invariant($"- **زمان پردازش ورودی / برداشت زنده (برای CSV زمان استخراج نیست):** `{capturedAtUtc:yyyy-MM-dd HH:mm:ss} UTC`"));
        sb.AppendLine($"- **هش مانیفست اقلام (Manifest Hash):** `{manifestHash}`");
        sb.AppendLine($"- **هش پلن اجرایی (Plan Hash):** `{plan.PlanHash}`");
        sb.AppendLine();

        if (captureResult.InputManifest != null && captureResult.InputManifest.Count > 0)
        {
            sb.AppendLine("### ۲. مانیفست دقیق فایل‌های ورودی (Input File Manifest)");
            sb.AppendLine("| نقش فایل | مسیر فایل | آخرین تغییر فایل (UTC؛ زمان استخراج نیست) | هش SHA-256 | تعداد ردیف داده |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :---: |");
            foreach (var f in captureResult.InputManifest)
            {
                sb.AppendLine(FormattableString.Invariant($"| `{f.Role}` | `{f.Path}` | `{f.LastModifiedUtc:yyyy-MM-dd HH:mm:ss}` | `{f.Sha256Hash}` | {f.RowCount} |"));
            }
            sb.AppendLine();
            sb.AppendLine("*توجه: تمامی فایل‌ها با مسیر قطعی و اعتبارسنجی مستقیم خوانده شده‌اند و هیچ ترکیب خودکار یا پنهانی بین اجراهای مختلف انجام نشده است.*");
            sb.AppendLine();
        }

        sb.AppendLine("### ۳. تفکیک دقیق منشأ داده‌ها (Data Provenance Breakdown)");
        sb.AppendLine("| مؤلفه داده | نوع منشأ | منبع استخراج / تولید | توضیحات و هشدارهای فنی |");
        sb.AppendLine("| :--- | :---: | :--- | :--- |");
        sb.AppendLine("| **موجودی‌های مبدأ (OnHand)** | 📊 استخراج‌شده واقعی | نمونه CSV واقعی (01_kg_stock_calculation_sample.csv) | شامل قلم ۳۶۹ با موجودی منفی (1590.00-) و قلم ۸۹ با ۱۰۷.۱۵ کیلوگرم و قلم ۳۶۰ با ۱۵۱۰.۴۱ کیلوگرم. این فایل‌ها برداشت تراکنشی مشترک نیستند. |");
        sb.AppendLine("| **مشخصات و مقادیر پایه CRM** | 🧪 ساختگی آزمایشی (SYNTHETIC_TEST_DATA) | سناریوی آزمایشی محلی CRM جهت ارزیابی ریدر | کلیه مقادیر اولیه OnHand، مقادیر رزرو (Reserved)، ورودی‌های در راه (Incoming) و عناوین کالا کاملاً ساختگی و آزمایشی هستند. |");
        if (captureResult.MissingMovementData)
        {
            sb.AppendLine("| **گردش‌های انبار (Movements)** | ⚠️ مفقود (MissingMovementData) | فایل اقلام گردش موجود نیست | نمونه‌های شماره سند فاقد قلم و مقدار هستند. کاردکس کامل نیست و صفر تلقی نمی‌شود. |");
        }
        else
        {
            sb.AppendLine("| **گردش‌های انبار (Movements)** | 🧪 ساختگی آزمایشی | برچسب صریح SYNTHETIC_TEST_MOVEMENT | گردش‌های آزمایشی کنترل‌شده جهت تست رفتار ریدر. |");
        }
        sb.AppendLine();

        sb.AppendLine("### ۴. تفکیک سازگاری تراکنشی از اعتبار معنایی داده‌ها");
        sb.AppendLine("| مؤلفه ارزیابی | وضعیت | ملاحظات فنی |");
        sb.AppendLine("| :--- | :---: | :--- |");
        if (captureResult.IsConsistent)
        {
            sb.AppendLine("| **سازگاری تراکنشی (Snapshot)** | ✅ تأییدشده | هم‌زمانی مقطعی موجودی و گردش‌ها در یک تراکنش Snapshot بدون قفل اشتراکی داده تضمین شده است. |");
        }
        else
        {
            sb.AppendLine("| **سازگاری تراکنشی (Snapshot)** | ❌ تأییدنشده | فایل‌های ورودی نمونه‌های مجزای CSV هستند و در یک تراکنش Snapshot مشترک برداشت نشده‌اند؛ سازگاری تراکنشی تأییدنشده است. |");
        }

        if (isExecutable)
        {
            sb.AppendLine("| **اعتبار معنایی قرارداد** | ⚠️ شبیه‌سازی | اجرای پایلوت صرفاً در محدوده داده‌های آزمایشی شبیه‌سازی‌شده (SIMULATED_TEST) معتبر است. |");
        }
        else
        {
            sb.AppendLine("| **اعتبار معنایی قرارداد** | ❌ تأییدنشده | قرارداد معنایی اسناد عملیاتی سپیدار هنوز اعتبارسنجی میدانی نشده است (خروجی غیرقابل اجرا). |");
        }
        sb.AppendLine();
        sb.AppendLine("#### فرضیات اثبات‌نشده در ساختار مبدأ:");
        sb.AppendLine("1. **نوع سند (`MovementType`):** فرض انطباق کدهای ۱ و ۲ و ۴ با رسید، مصرف و انتقال صرفاً در شِمای تست اثبات شده است و نیازمند انطباق با جدول انواع اسناد سپیدار عملیاتی است.");
        sb.AppendLine("2. **وضعیت ابطال (`IsCancelled`):** فرض `CAST(0 AS BIT)` یک شبیه‌سازی ساختگی است؛ در سپیدار ستون بولین ابطال وجود ندارد و ابطال از وضعیت سند یا اسناد معکوس خوانده می‌شود.");
        sb.AppendLine("3. **وضعیت قطعی حسابداری (`PostingStatus`):** ارتباط `AccountingVoucherRef IS NOT NULL` با وضعیت قطعی نیازمند اعتبارسنجی میدانی است.");
        sb.AppendLine("4. **مرز تاریخ پایان دوره (`EndDate`):** افزودن یک روز به دلیل ساعت 00:00:00 برای محیط واقعی قابل اتکا نیست و رفتار ثبت تاریخ در سپیدار باید میدانی بررسی شود.");
        sb.AppendLine();

        sb.AppendLine("### ۵. آمار و شمارش اقلام");
        sb.AppendLine("| شاخص | تعداد | توضیحات |");
        sb.AppendLine("| :--- | :---: | :--- |");
        sb.AppendLine($"| اقلام واردشده به مانیفست پایلوت | **{manifest.Items.Count}** | کاندیداهای سازگار و دارای اسنپ‌شات کیلوگرم |");
        sb.AppendLine($"| اقلام کنارگذاشته‌شده از ورود به پایلوت | **{exclusions.Count}** | دلایل رد با شفافیت ثبت شده‌اند |");
        sb.AppendLine($"| اسنپ‌شات‌های موجودی دارای تغییر | **{snapshotModified}** | تغییر OnHand یا Timestamp/SourceKey |");
        sb.AppendLine($"| اسنپ‌شات‌های بدون تغییر | **{snapshotUnchanged}** | مقادیر منطبق با مبدأ بوده‌اند |");
        if (captureResult.MissingMovementData)
        {
            sb.AppendLine($"| گردش‌های جدید انبار جهت درج | **-** | به دلیل نبود داده‌های اقلام گردش (MissingMovementData)، کاردکس کامل نیست و صفر تلقی نمی‌شود |");
            sb.AppendLine($"| گردش‌های دارای اصلاح | **-** | وضعیت گردش‌های انبار مفقود (MissingMovementData) است |");
            sb.AppendLine($"| گردش‌های بدون تغییر | **-** | وضعیت گردش‌های انبار مفقود (MissingMovementData) است |");
        }
        else
        {
            sb.AppendLine($"| گردش‌های جدید انبار جهت درج | **{movementInserted}** | اسناد رسید یا حواله جدید |");
            sb.AppendLine($"| گردش‌های دارای اصلاح | **{movementModified}** | اسناد دارای تغییر وضعیت یا مقدار |");
            sb.AppendLine($"| گردش‌های بدون تغییر | **{movementUnchanged}** | اسناد منطبق در دوره |");
        }
        sb.AppendLine($"| ردیف‌های موجودی مبدأ خارج از مانیفست | **{plan.ExcludedStockRows}** | اقلامی که در مانیفست تأییدشده نبودند |");
        sb.AppendLine($"| ردیف‌های گردش مبدأ خارج از مانیفست | **{plan.ExcludedMovementRows}** | گردش‌های مربوط به اقلام خارج از مانیفست |");
        sb.AppendLine();

        if (warnings.Count > 0)
        {
            sb.AppendLine("### ۶. هشدارهای پایلوت (دسته‌بندی یا مغایرت‌های فنی)");
            foreach (var w in warnings)
            {
                sb.AppendLine($"- ⚠️ {w}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### ۷. جزئیات تغییرات اسنپ‌شات‌های موجودی (Snapshots)");
        if (plan.Snapshots.Count == 0)
        {
            sb.AppendLine("*هیچ تغییری در اسنپ‌شات‌های موجودی اقلام مانیفست شناسایی نشد.*");
        }
        else
        {
            sb.AppendLine("| شناسه قلم | OnHand قبل | OnHand بعد | Reservable قبل -> بعد | Reserved (حفظ بومی CRM) | Incoming (حفظ بومی CRM) |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: |");
            foreach (var ch in plan.Snapshots)
            {
                var item = manifest.Items.FirstOrDefault(i => i.MaterialId == ch.Before.MaterialId);
                var sourceId = item?.SourceItemId ?? ch.Before.MaterialId.ToString();
                sb.AppendLine($"| `{sourceId}` | {ch.Before.OnHand:N2} | **{ch.After.OnHand:N2}** | {ch.Before.Reservable:N2} -> {ch.After.Reservable:N2} | {ch.After.Reserved:N2} | {ch.After.Incoming:N2} |");
            }
        }
        sb.AppendLine();

        sb.AppendLine("### ۸. جزئیات گردش‌های انبار (Movements)");
        if (captureResult.MissingMovementData)
        {
            sb.AppendLine("⚠️ **داده‌های اقلام گردش انبار مفقود است (MissingMovementData):** فایل اقلام گردش ارائه نشده است. نبود فایل گردش معادل صفر گردش یا کاردکس کامل نیست. نمونه‌های CSV شماره سند نیز فاقد قلم کالا و مقدار بوده و امکان استنتاج ردیف گردش از آن‌ها وجود ندارد.");
        }
        else if (plan.Movements.Count == 0)
        {
            sb.AppendLine("*هیچ گردش جدید یا اصلاحی برای اسناد انبار اقلام مانیفست نیاز نیست.*");
        }
        else
        {
            sb.AppendLine("| عملیات | شماره سند | کلید سند مبدأ | نوع سند | مقدار | تاریخ سند (UTC) |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :---: | :---: |");
            foreach (var ch in plan.Movements)
            {
                var op = ch.Before == null ? "درج جدید" : "اصلاح سند";
                sb.AppendLine(FormattableString.Invariant($"| **{op}** | `{ch.After.VoucherNumber}` | `{ch.After.SourceKey}` | {ch.After.Type} | {ch.After.Quantity:N2} | {ch.After.TransactionDate:yyyy-MM-dd} |"));
            }
        }
        sb.AppendLine();

        if (exclusions.Count > 0)
        {
            sb.AppendLine("### ۹. اقلام کنارگذاشته‌شده از پایلوت و دلایل رد (Exclusions)");
            sb.AppendLine("| شناسه سپیدار | کد کالا در CRM | نام کالا | دلیل رد | توضیحات دقیق |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
            foreach (var ex in exclusions)
            {
                sb.AppendLine($"| `{ex.SourceItemId}` | `{ex.MaterialCode ?? "-"}` | {ex.MaterialName ?? "-"} | `{ex.Reason}` | {ex.Details} |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
