using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Adapters;

public class SepidarFixtureInventoryAdapter : ISepidarInventoryAdapter
{
    private readonly ILogger<SepidarFixtureInventoryAdapter> _logger;

    public SepidarFixtureInventoryAdapter(ILogger<SepidarFixtureInventoryAdapter> logger)
    {
        _logger = logger;
    }

    public bool IsLiveConnected => false;
    public string AdapterName => "SepidarFixtureInventoryAdapter (Deterministic Bakery Fixtures)";

    public Task<List<SepidarWarehouseRecord>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var warehouses = new List<SepidarWarehouseRecord>
        {
            new("1", "WH-MAIN", "انبار مرکزی کارخانه نان", true)
        };
        return Task.FromResult(warehouses);
    }

    public Task<List<SepidarItemRecord>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<SepidarItemRecord>
        {
            new("101001", "MAT-FLOUR-STAR", "آرد ستاره نان فانتزی ۲۱٪", "KG", "کیلوگرم", true),
            new("101002", "MAT-YEAST-DRY", "مخمر خشک فوری فرانسوی", "KG", "کیلوگرم", true),
            new("101003", "MAT-IMPROVER-MAGIK", "بهبوددهنده تخصصی نان باگت ماژیک", "KG", "کیلوگرم", true),
            new("101004", "MAT-MARGARINE", "مارگارین قنادی ۴۴٪", "KG", "کیلوگرم", true),
            new("101005", "MAT-SALT", "نمک تصفیه یددار خوراکی", "KG", "کیلوگرم", true),
            new("101006", "MAT-BAG-PRINTED", "کیسه نان فانتزی چاپی", "COUNT", "عدد", true)
        };
        return Task.FromResult(items);
    }

    public Task<List<SepidarStockRecord>> GetStockBalancesAsync(string warehouseRef, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var balances = new List<SepidarStockRecord>
        {
            // Flour: OnHand = 12,500 kg, Reservable = 10,000 kg, Reserved = 2,500 kg, Incoming = 0
            new("101001", warehouseRef, 12500m, 10000m, 2500m, 0m, "KG", now),
            // Yeast: OnHand = 80 kg, Reservable = 60 kg (Below safety stock 100 kg!), Reserved = 20 kg
            new("101002", warehouseRef, 80m, 60m, 20m, 0m, "KG", now),
            // Improver: OnHand = 140 kg, Reservable = 120 kg, Reserved = 20 kg
            new("101003", warehouseRef, 140m, 120m, 20m, 50m, "KG", now),
            // Margarine: OnHand = 850 kg, Reservable = 750 kg, Reserved = 100 kg
            new("101004", warehouseRef, 850m, 750m, 100m, 0m, "KG", now),
            // Salt: OnHand = 600 kg, Reservable = 550 kg, Reserved = 50 kg
            new("101005", warehouseRef, 600m, 550m, 50m, 0m, "KG", now),
            // Bags: OnHand = 45,000 count, Reservable = 40,000 count, Reserved = 5,000 count
            new("101006", warehouseRef, 45000m, 40000m, 5000m, 0m, "COUNT", now)
        };
        return Task.FromResult(balances);
    }

    public Task<List<SepidarMovementRecord>> GetInventoryMovementsAsync(
        string warehouseRef, 
        DateTime? sinceDateUtc = null, 
        CancellationToken cancellationToken = default)
    {
        var movements = new List<SepidarMovementRecord>();
        var baseDate = DateTime.UtcNow.Date;

        // Generate 30 days of realistic movements
        for (int dayOffset = 30; dayOffset >= 0; dayOffset--)
        {
            var date = baseDate.AddDays(-dayOffset);
            var isFriday = date.DayOfWeek == DayOfWeek.Friday;

            // Flour (101001): Normal daily production consumption is ~500 kg on work days, 0 on Fridays
            if (!isFriday)
            {
                decimal qty = 480m + ((dayOffset % 5) * 10m); // 480 - 520 kg/day
                movements.Add(new SepidarMovementRecord(
                    $"V-FLOUR-{dayOffset}",
                    $"VCH-100{dayOffset:D2}",
                    "101001",
                    warehouseRef,
                    InventoryMovementType.Consumption,
                    qty,
                    "KG",
                    date.AddHours(10),
                    4,     // Posted
                    false, // Not cancelled
                    $"BATCH-BREAD-{date:yyyyMMdd}"
                ));
            }

            // Yeast (101002): Daily consumption ~15 kg on work days
            if (!isFriday && dayOffset % 2 == 0)
            {
                movements.Add(new SepidarMovementRecord(
                    $"V-YEAST-{dayOffset}",
                    $"VCH-200{dayOffset:D2}",
                    "101002",
                    warehouseRef,
                    InventoryMovementType.Consumption,
                    15m,
                    "KG",
                    date.AddHours(11),
                    4,
                    false,
                    $"BATCH-YEAST-{date:yyyyMMdd}"
                ));
            }

            // Bread Improver (101003): Daily consumption ~8 kg
            if (!isFriday && dayOffset % 3 == 0)
            {
                movements.Add(new SepidarMovementRecord(
                    $"V-IMPR-{dayOffset}",
                    $"VCH-300{dayOffset:D2}",
                    "101003",
                    warehouseRef,
                    InventoryMovementType.Consumption,
                    8m,
                    "KG",
                    date.AddHours(11),
                    4,
                    false,
                    null
                ));
            }
        }

        // Add goods receipt (Receipt type = 2) for Flour 10 days ago (10,000 kg)
        movements.Add(new SepidarMovementRecord(
            "V-RCPT-FLOUR-10",
            "RCPT-90010",
            "101001",
            warehouseRef,
            InventoryMovementType.Receipt,
            10000m,
            "KG",
            baseDate.AddDays(-10).AddHours(8),
            4,
            false,
            "INV-SUPP-8871"
        ));

        // Add CANCELLED voucher to test that cancelled transactions are excluded
        movements.Add(new SepidarMovementRecord(
            "V-FLOUR-CANCELLED",
            "VCH-CAN-01",
            "101001",
            warehouseRef,
            InventoryMovementType.Consumption,
            1000m,
            "KG",
            baseDate.AddDays(-5).AddHours(14),
            4,
            true, // IsCancelled = true! MUST BE EXCLUDED FROM CONSUMPTION
            "ERROR_WRONG_ENTRY"
        ));

        // Add DRAFT (Unposted) voucher to test that draft transactions are excluded
        movements.Add(new SepidarMovementRecord(
            "V-FLOUR-DRAFT",
            "VCH-DFT-01",
            "101001",
            warehouseRef,
            InventoryMovementType.Consumption,
            2000m,
            "KG",
            baseDate.AddDays(-2).AddHours(16),
            1, // PostingStatus = 1 (Draft)! MUST BE EXCLUDED FROM CONSUMPTION
            false,
            "PENDING_APPROVAL"
        ));

        // Add TRANSFER voucher to test that warehouse transfers are excluded from consumption
        movements.Add(new SepidarMovementRecord(
            "V-FLOUR-TRANSFER",
            "TRF-001",
            "101001",
            warehouseRef,
            InventoryMovementType.Transfer,
            500m,
            "KG",
            baseDate.AddDays(-3).AddHours(9),
            4,
            false,
            "TRANSFER_TO_SUB_PLANT"
        ));

        if (sinceDateUtc.HasValue)
        {
            movements = movements.Where(m => m.TransactionDateUtc >= sinceDateUtc.Value).ToList();
        }

        _logger.LogInformation("SepidarFixtureInventoryAdapter returning {Count} simulated movements.", movements.Count);
        return Task.FromResult(movements);
    }
}
