using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data;

public static class CrmDbSeeder
{
    public static async Task SeedAsync(CrmDbContext context, bool isDevelopmentOrTest = false)
    {
        // 1. Seed Permissions
        var existingPerms = await context.Permissions.ToDictionaryAsync(p => p.Code);
        var permissionsToSeed = new List<Permission>
        {
            new() { Code = Permissions.MaterialsView, Category = "Materials", Description = "مشاهده کاتالوگ و مشخصات مواد اولیه" },
            new() { Code = Permissions.MaterialsManage, Category = "Materials", Description = "مدیریت و ویرایش کاتالوگ مواد اولیه" },
            new() { Code = Permissions.SuppliersView, Category = "Suppliers", Description = "مشاهده تأمین‌کنندگان و مخاطبین" },
            new() { Code = Permissions.SuppliersManage, Category = "Suppliers", Description = "ثبت و ویرایش اطلاعات تأمین‌کنندگان" },
            new() { Code = Permissions.SuppliersPricesView, Category = "Suppliers", Description = "مشاهده قیمت‌ها و استعلام‌های تأمین‌کنندگان" },
            new() { Code = Permissions.SuppliersPricesManage, Category = "Suppliers", Description = "ثبت و به‌روزرسانی قیمت‌های استعلامی تأمین‌کنندگان" },
            new() { Code = Permissions.OrdersView, Category = "Procurement", Description = "مشاهده سفارشات خرید و درخواست‌ها" },
            new() { Code = Permissions.OrdersCreate, Category = "Procurement", Description = "ایجاد پیش‌نویس سفارش خرید" },
            new() { Code = Permissions.OrdersIssue, Category = "Procurement", Description = "صدور مستقیم سفارش خرید بدون نیاز به تأیید مدیر" },
            new() { Code = Permissions.WarehouseView, Category = "Warehouse", Description = "مشاهده موجودی انبار واحد و مغایرت‌ها" },
            new() { Code = Permissions.WarehouseReceive, Category = "Warehouse", Description = "ثبت رسید موقت و بازرسی کالا" },
            new() { Code = Permissions.ReceiptsView, Category = "Warehouse", Description = "مشاهده رسیدهای ورود کالا به انبار" },
            new() { Code = Permissions.ReceiptsManage, Category = "Warehouse", Description = "ثبت و تایید رسیدهای انبار و کنترل کیفی" },
            new() { Code = Permissions.InventoryView, Category = "Inventory", Description = "مشاهده موجودی انبار و گردش کالاها" },
            new() { Code = Permissions.InventoryManage, Category = "Inventory", Description = "مدیریت اسناد انبار و مغایرت‌ها" },
            new() { Code = Permissions.InventorySync, Category = "Inventory", Description = "اجرا و مانیتورینگ همگام‌سازی انبار با سپیدار" },
            new() { Code = Permissions.OrdersCancel, Category = "Procurement", Description = "ابطال سفارش خرید بدون دریافت کالا" },
            new() { Code = Permissions.QuotesView, Category = "Procurement", Description = "مشاهده استعلام‌ها و پیش‌فاکتورهای تأمین‌کنندگان" },
            new() { Code = Permissions.QuotesManage, Category = "Procurement", Description = "ثبت و مدیریت استعلام قیمت و RFQ" },
            new() { Code = Permissions.RecommendationsView, Category = "Procurement", Description = "مشاهده محاسبات و پیشنهادات خرید روزانه" },
            new() { Code = Permissions.PoliciesManage, Category = "Procurement", Description = "تنظیم و ویرایش خط‌مشی‌های سفارش‌دهی مواد اولیه" },
            new() { Code = Permissions.FinanceView, Category = "Finance", Description = "مشاهده وضعیت مالی و فاکتورها" },
            new() { Code = Permissions.FinanceReconcile, Category = "Finance", Description = "تطبیق اسناد سپیدار با سفارشات CRM" },
            new() { Code = Permissions.InvoicesView, Category = "Finance", Description = "مشاهده فاکتورهای تأمین‌کنندگان" },
            new() { Code = Permissions.InvoicesManage, Category = "Finance", Description = "ثبت و مدیریت فاکتورهای تأمین‌کنندگان" },
            new() { Code = Permissions.MatchingView, Category = "Finance", Description = "مشاهده نتایج تطبیق سه‌طرفه و صف مغایرت‌ها" },
            new() { Code = Permissions.MatchingResolve, Category = "Finance", Description = "تصمیم‌گیری و رفع مغایرت‌های تطبیق سه‌طرفه" },
            new() { Code = Permissions.CallsView, Category = "Telephony", Description = "مشاهده تاریخچه تماس‌ها" },
            new() { Code = Permissions.CallsManage, Category = "Telephony", Description = "ثبت یادداشت و پیگیری برای تماس‌ها" },
            new() { Code = Permissions.CallsSupervise, Category = "Telephony", Description = "نظارت مدیریتی و مانیتورینگ تماس‌های کلیه پرسنل" },
            new() { Code = Permissions.CallsRecordingsListen, Category = "Telephony", Description = "شنود و استریم فایل‌های صوتی مکالمات ضبط شده" },
            new() { Code = Permissions.CallsConfig, Category = "Telephony", Description = "پیکربندی تنظیمات تلفنی و اتصال به ایزابل" },
            new() { Code = Permissions.ReportsView, Category = "Reporting", Description = "مشاهده داشبوردها و گزارش‌های تحلیلی" },
            new() { Code = Permissions.ReportsExport, Category = "Reporting", Description = "خروجی اکسل و CSV از گزارشات" },
            new() { Code = Permissions.ReportsProcurement, Category = "Reporting", Description = "دسترسی به گزارشات اختصاصی تدارکات و خرید" },
            new() { Code = Permissions.ReportsManager, Category = "Reporting", Description = "دسترسی به گزارشات جامع مدیریتی و KPIها" },
            new() { Code = Permissions.ReportsWarehouse, Category = "Reporting", Description = "دسترسی به گزارشات موجودی و سلامت انبار" },
            new() { Code = Permissions.ReportsFinance, Category = "Reporting", Description = "دسترسی به گزارشات انطباق مالی و مغایرت‌ها" },
            new() { Code = Permissions.ReportsAdmin, Category = "Reporting", Description = "دسترسی به مانیتورینگ سلامت سیستم و اتصالات" },
            new() { Code = Permissions.AdminUsers, Category = "Administration", Description = "مدیریت کاربران و تخصیص نقش‌ها" },
            new() { Code = Permissions.AdminSettings, Category = "Administration", Description = "تنظیمات عمومی و سیستم" },
            new() { Code = Permissions.AdminAudit, Category = "Administration", Description = "مشاهده لاگ‌های امنیتی و رویدادها" }
        };

        foreach (var p in permissionsToSeed)
        {
            if (!existingPerms.ContainsKey(p.Code))
            {
                context.Permissions.Add(p);
                existingPerms[p.Code] = p;
            }
        }

        await context.SaveChangesAsync();

        // 2. Seed Roles
        var existingRoles = await context.Roles.Include(r => r.RolePermissions).ToDictionaryAsync(r => r.Name);
        var rolesToSeed = new List<Role>
        {
            new() { Name = Roles.Admin, Description = "مدیر ارشد سیستم - دسترسی کامل به کلیه بخش‌ها و لاگ‌ها" },
            new() { Name = Roles.Procurement, Description = "واحد تدارکات و خرید - ثبت سفارش، استعلام قیمت، تعامل با تأمین‌کنندگان" },
            new() { Name = Roles.Manager, Description = "مدیر ناظر - نظارت بر فرایند خرید، داشبوردها و گزارشات" },
            new() { Name = Roles.Warehouse, Description = "انباردار - نظارت بر موجودی، اعلام ورود کالا، کسری و مرجوعی" },
            new() { Name = Roles.Finance, Description = "واحد مالی و حسابداری - انطباق فاکتورها، مغایرت‌گیری با سپیدار و تسویه" }
        };

        foreach (var r in rolesToSeed)
        {
            if (!existingRoles.ContainsKey(r.Name))
            {
                context.Roles.Add(r);
                existingRoles[r.Name] = r;
            }
        }

        await context.SaveChangesAsync();

        // 3. Map Default Role Permissions
        var allPerms = await context.Permissions.ToListAsync();
        var adminRole = existingRoles[Roles.Admin];
        var procurementRole = existingRoles[Roles.Procurement];
        var managerRole = existingRoles[Roles.Manager];
        var warehouseRole = existingRoles[Roles.Warehouse];
        var financeRole = existingRoles[Roles.Finance];

        void Grant(Role role, params string[] permCodes)
        {
            foreach (var code in permCodes)
            {
                var perm = allPerms.First(p => p.Code == code);
                if (!role.RolePermissions.Any(rp => rp.PermissionId == perm.Id))
                {
                    role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                }
            }
        }

        // Admin gets all
        Grant(adminRole, allPerms.Select(p => p.Code).ToArray());

        // Procurement gets catalog, suppliers, prices, orders (direct issuance & cancellation), RFQs, inventory view, recommendations, policies, calls, reporting
        Grant(procurementRole,
            Permissions.MaterialsView, Permissions.MaterialsManage,
            Permissions.SuppliersView, Permissions.SuppliersManage,
            Permissions.SuppliersPricesView, Permissions.SuppliersPricesManage,
            Permissions.OrdersView, Permissions.OrdersCreate, Permissions.OrdersIssue, Permissions.OrdersCancel,
            Permissions.QuotesView, Permissions.QuotesManage,
            Permissions.WarehouseView, Permissions.ReceiptsView, Permissions.InventoryView,
            Permissions.RecommendationsView, Permissions.PoliciesManage,
            Permissions.InvoicesView, Permissions.MatchingView,
            Permissions.CallsView, Permissions.CallsManage, Permissions.CallsRecordingsListen,
            Permissions.ReportsView, Permissions.ReportsProcurement, Permissions.ReportsExport);

        // Manager gets oversight, price viewing, inventory view & recommendations, matching, supervisory calls, management reporting
        Grant(managerRole,
            Permissions.MaterialsView, Permissions.SuppliersView,
            Permissions.SuppliersPricesView,
            Permissions.OrdersView, Permissions.QuotesView, Permissions.WarehouseView, Permissions.ReceiptsView,
            Permissions.InventoryView, Permissions.RecommendationsView,
            Permissions.InvoicesView, Permissions.MatchingView,
            Permissions.FinanceView, Permissions.CallsView, Permissions.CallsSupervise, Permissions.CallsRecordingsListen,
            Permissions.ReportsView, Permissions.ReportsManager, Permissions.ReportsProcurement, Permissions.ReportsWarehouse, Permissions.ReportsFinance, Permissions.ReportsExport);

        // Warehouse gets warehouse & receipts manage, inventory view/manage, materials view, warehouse reporting
        Grant(warehouseRole,
            Permissions.WarehouseView, Permissions.WarehouseReceive,
            Permissions.ReceiptsView, Permissions.ReceiptsManage,
            Permissions.InventoryView, Permissions.InventoryManage,
            Permissions.MaterialsView, Permissions.OrdersView,
            Permissions.ReportsView, Permissions.ReportsWarehouse, Permissions.ReportsExport);

        // Finance gets invoices, matching, reconciliation, finance and prices, inventory view, finance reporting
        Grant(financeRole,
            Permissions.FinanceView, Permissions.FinanceReconcile,
            Permissions.InvoicesView, Permissions.InvoicesManage,
            Permissions.MatchingView, Permissions.MatchingResolve,
            Permissions.SuppliersView, Permissions.SuppliersPricesView,
            Permissions.OrdersView, Permissions.ReceiptsView, Permissions.InventoryView,
            Permissions.ReportsView, Permissions.ReportsFinance, Permissions.ReportsExport);

        await context.SaveChangesAsync();

        // 4. Seed Standard Units of Measure (if empty)
        if (!await context.UnitsOfMeasure.AnyAsync())
        {
            var units = new List<UnitOfMeasure>
            {
                new() { Code = "KG", Name = "کیلوگرم", Precision = 2, Dimension = "Mass" },
                new() { Code = "GR", Name = "گرم", Precision = 0, Dimension = "Mass" },
                new() { Code = "TON", Name = "تن", Precision = 3, Dimension = "Mass" },
                new() { Code = "L", Name = "لیتر", Precision = 2, Dimension = "Volume" },
                new() { Code = "ML", Name = "میلی‌لیتر", Precision = 0, Dimension = "Volume" },
                new() { Code = "COUNT", Name = "عدد", Precision = 0, Dimension = "Count" },
                new() { Code = "BAG50", Name = "کیسه ۵۰ کیلویی", Precision = 0, Dimension = "Packaging" },
                new() { Code = "CARTON", Name = "کارتن/بسته", Precision = 0, Dimension = "Packaging" }
            };
            context.UnitsOfMeasure.AddRange(units);
            await context.SaveChangesAsync();
        }

        // 5. Seed Standard Root Categories (if empty)
        if (!await context.Categories.AnyAsync())
        {
            var flourCategory = new Category
            {
                Name = "آرد و غلات",
                Code = "CAT-FLOUR",
                Description = "انواع آرد گندم، جو، سبوس و دانه‌های روغنی نانوایی",
                SortOrder = 1
            };
            var improversCategory = new Category
            {
                Name = "بهبوددهنده‌ها و مخمر",
                Code = "CAT-IMPROVER",
                Description = "مخمر خشک و تازه، انواع بهبوددهنده‌های تخصصی نان باگت و حجیم",
                SortOrder = 2
            };
            var fatsCategory = new Category
            {
                Name = "روغن و چربی‌های قنادی",
                Code = "CAT-FATS",
                Description = "مارگارین، روغن مایع و قنادی، کره",
                SortOrder = 3
            };
            var packagingCategory = new Category
            {
                Name = "ملزومات بسته‌بندی",
                Code = "CAT-PACKAGING",
                Description = "کیسه نان، سلفون، کارتن و برچسب قیمت",
                SortOrder = 4
            };

            context.Categories.AddRange(flourCategory, improversCategory, fatsCategory, packagingCategory);
            await context.SaveChangesAsync();
        }

        // 6. Seed Default Central Warehouse (if empty)
        if (!await context.Warehouses.AnyAsync())
        {
            var mainWarehouse = new Warehouse
            {
                Code = "WH-MAIN",
                Name = "انبار مرکزی کارخانه نان",
                Description = "انبار اصلی دریافت و ذخیره‌سازی آرد، مخمر، بهبوددهنده‌ها و ملزومات تولید نان و شیرینی",
                SepidarWarehouseRef = "1",
                IsActive = true
            };
            context.Warehouses.Add(mainWarehouse);
            await context.SaveChangesAsync();
        }

        // 7. Seed Default User Extensions (if empty)
        if (!await context.UserExtensions.AnyAsync())
        {
            var extensions = new List<UserExtension>
            {
                new() { Username = "admin", Extension = "100", DeviceType = "SIP", Description = "داخلی مدیر ارشد سیستم" },
                new() { Username = "procurement", Extension = "101", DeviceType = "SIP", Description = "داخلی کارشناس تدارکات و خرید" },
                new() { Username = "manager", Extension = "102", DeviceType = "SIP", Description = "داخلی مدیر ناظر تدارکات" },
                new() { Username = "warehouse", Extension = "103", DeviceType = "SIP", Description = "داخلی انبار مرکزی" },
                new() { Username = "finance", Extension = "104", DeviceType = "SIP", Description = "داخلی واحد حسابداری" }
            };
            context.UserExtensions.AddRange(extensions);
            await context.SaveChangesAsync();
        }

        // 8. Seed Default Users & Role Mappings (Active Directory & System Administrator)
        var saharAdminSid = "S-1-5-21-2907115954-3499431352-1050369382-500";
        var crmAdminSid = "S-1-5-21-3272260558-1801529630-4144290918-500";

        var adminRoleEntity = existingRoles[Roles.Admin];
        var managerRoleEntity = existingRoles[Roles.Manager];
        var procurementRoleEntity = existingRoles[Roles.Procurement];
        var warehouseRoleEntity = existingRoles[Roles.Warehouse];
        var financeRoleEntity = existingRoles[Roles.Finance];

        var defaultUsers = new List<(string Sid, string Username, string DisplayName, string Email, Role[] UserRoles)>
        {
            (saharAdminSid, "SAHAR\\Administrator", "مدیر ارشد سیستم (Admin)", "administrator@sahar.loc", new[] { adminRoleEntity, managerRoleEntity }),
            (crmAdminSid, "CRM\\Administrator", "مدیر سرور CRM (Admin)", "crm-admin@sahar.loc", new[] { adminRoleEntity, managerRoleEntity }),
            ("SAHAR-MANAGER-SID", "SAHAR\\manager", "مدیر ناظر بازرگانی", "manager@sahar.loc", new[] { managerRoleEntity }),
            ("SAHAR-PROCUREMENT-SID", "SAHAR\\procurement", "کارشناس تدارکات", "procurement@sahar.loc", new[] { procurementRoleEntity }),
            ("SAHAR-WAREHOUSE-SID", "SAHAR\\warehouse", "سرپرست انبار", "warehouse@sahar.loc", new[] { warehouseRoleEntity }),
            ("SAHAR-FINANCE-SID", "SAHAR\\finance", "کارشناس حسابداری", "finance@sahar.loc", new[] { financeRoleEntity })
        };

        foreach (var def in defaultUsers)
        {
            var user = await context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.AdObjectSid == def.Sid || u.Username == def.Username);

            if (user == null)
            {
                user = new AppUser
                {
                    AdObjectSid = def.Sid,
                    Username = def.Username,
                    DisplayName = def.DisplayName,
                    Email = def.Email,
                    IsEnabled = true
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            foreach (var r in def.UserRoles)
            {
                if (!user.UserRoles.Any(ur => ur.RoleId == r.Id))
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = r.Id,
                        AssignedAtUtc = DateTime.UtcNow
                    });
                }
            }
        }

        await context.SaveChangesAsync();

        // 9. Seed Pilot Catalog Items (Requirement 4: Test items for Damdaran Milk 1L & 250cc)
        // CRITICAL GUARD: Never seed TEST-DAM / TEST-SEP into Production environment!
        if (isDevelopmentOrTest)
        {
            await SeedPilotCatalogItemsAsync(context);
        }
    }

    private static async Task SeedPilotCatalogItemsAsync(CrmDbContext context)
    {
        var countUnit = await context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "COUNT");
        var literUnit = await context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "L");
        var mlUnit = await context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "ML");
        var kgUnit = await context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "KG");
        var wh = await context.Warehouses.FirstOrDefaultAsync(w => w.Code == "WH-MAIN");

        if (countUnit == null || literUnit == null || mlUnit == null) return;

        // 1. Ensure Dairy Root Category
        var dairyCat = await context.Categories.FirstOrDefaultAsync(c => c.Code == "CAT-DAIRY");
        if (dairyCat == null)
        {
            dairyCat = new Category
            {
                Code = "CAT-DAIRY",
                Name = "لبنیات",
                Description = "شیر، خامه، پنیر و محصولات لبنی مصرفی کارخانه نان و قنادی",
                SortOrder = 5,
                IsActive = true
            };
            context.Categories.Add(dairyCat);
            await context.SaveChangesAsync();
        }

        // 2. Ensure Milk Subcategory under Dairy
        var milkCat = await context.Categories.FirstOrDefaultAsync(c => c.Code == "CAT-MILK");
        if (milkCat == null)
        {
            milkCat = new Category
            {
                Code = "CAT-MILK",
                Name = "شیر",
                ParentId = dairyCat.Id,
                Description = "انواع شیر پاستوریزه، پاکتی و فله در احجام مختلف",
                SortOrder = 1,
                IsActive = true
            };
            context.Categories.Add(milkCat);
            await context.SaveChangesAsync();
        }

        // 3. Item 1: Damdaran Milk 1L (Test / Pilot Item)
        var milk1L = await context.Materials.FirstOrDefaultAsync(m => m.InternalCode == "TEST-DAM-MILK-1L");
        if (milk1L == null)
        {
            milk1L = new Material
            {
                Id = Guid.NewGuid(),
                InternalCode = "TEST-DAM-MILK-1L",
                Name = "شیر پاکتی دامداران ۱ لیتری",
                Brand = "دامداران",
                Packaging = "پاکت",
                PackageSize = 1.0m,
                PackageSizeUnitId = literUnit.Id,
                CategoryId = milkCat.Id,
                BaseUnitId = countUnit.Id,
                PurchaseUnitId = countUnit.Id,
                ConsumptionUnitId = literUnit.Id,
                ProductFamily = "شیر پاکتی دامداران",
                CategorizationStatus = "AutoConfirmed",
                IsCustomizedByUser = false,
                StandardizationNotes = "داده آزمایشی محلی / پایلوت کاتالوگ مستقل CRM",
                Description = "شیر پاستوریزه ۱ لیتری پاکتی دامداران جهت مصارف بخش قنادی و شیرینی‌پزی",
                MinStock = 50,
                SafetyStock = 20,
                LeadTimeDays = 1,
                IsActive = true
            };
            context.Materials.Add(milk1L);
            await context.SaveChangesAsync();

            // Unconfirmed suggested conversion: 1 count = 1.0 L
            context.MaterialUnitConversions.Add(new MaterialUnitConversion
            {
                Id = Guid.NewGuid(),
                MaterialId = milk1L.Id,
                FromUnitId = countUnit.Id,
                ToUnitId = literUnit.Id,
                Factor = 1.0m,
                IsConfirmed = false, // Suggestion - requires confirmation to activate
                Notes = "پیشنهاد خودکار از محتویات بسته (۱ لیتر) - نیازمند تأیید کاربر"
            });

            // Mapping to Sepidar test record
            context.InventoryItemMappings.Add(new InventoryItemMapping
            {
                Id = Guid.NewGuid(),
                MaterialId = milk1L.Id,
                SepidarItemId = "TEST-SEP-701",
                SepidarItemCode = "TEST-1001",
                SepidarItemTitle = "شیر پاکتی دامداران-۱لیتری (آزمایشی)",
                SepidarBaseUnit = "عددی",
                IsActive = true,
                LastSyncUtc = DateTime.UtcNow
            });

            // Snapshot in central warehouse: 120 counts
            if (wh != null)
            {
                context.InventorySnapshots.Add(new InventorySnapshot
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = wh.Id,
                    MaterialId = milk1L.Id,
                    UnitId = countUnit.Id,
                    OnHandQuantity = 120m,
                    ReservableQuantity = 120m,
                    IncomingQuantity = 0m,
                    ReservedQuantity = 0m,
                    SnapshotTimeUtc = DateTime.UtcNow
                });
            }
        }

        // 4. Item 2: Damdaran Milk 250 ML (Test / Pilot Item)
        var milk250ML = await context.Materials.FirstOrDefaultAsync(m => m.InternalCode == "TEST-DAM-MILK-250ML");
        if (milk250ML == null)
        {
            milk250ML = new Material
            {
                Id = Guid.NewGuid(),
                InternalCode = "TEST-DAM-MILK-250ML",
                Name = "شیر پاکتی دامداران ۲۵۰ سی‌سی",
                Brand = "دامداران",
                Packaging = "پاکت",
                PackageSize = 250m,
                PackageSizeUnitId = mlUnit.Id,
                CategoryId = milkCat.Id,
                BaseUnitId = countUnit.Id,
                PurchaseUnitId = countUnit.Id,
                ConsumptionUnitId = literUnit.Id,
                ProductFamily = "شیر پاکتی دامداران",
                CategorizationStatus = "AutoConfirmed",
                IsCustomizedByUser = false,
                StandardizationNotes = "داده آزمایشی محلی / پایلوت کاتالوگ مستقل CRM",
                Description = "شیر پاستوریزه ۲۵۰ سی‌سی (میلی‌لیتر) تک‌نفره پاکتی دامداران",
                MinStock = 100,
                SafetyStock = 40,
                LeadTimeDays = 1,
                IsActive = true
            };
            context.Materials.Add(milk250ML);
            await context.SaveChangesAsync();

            // Unconfirmed suggested conversion: 1 count = 0.25 L
            context.MaterialUnitConversions.Add(new MaterialUnitConversion
            {
                Id = Guid.NewGuid(),
                MaterialId = milk250ML.Id,
                FromUnitId = countUnit.Id,
                ToUnitId = literUnit.Id,
                Factor = 0.25m,
                IsConfirmed = false,
                Notes = "پیشنهاد خودکار از محتویات بسته (۲۵۰ سی‌سی معادل ۰.۲۵ لیتر) - نیازمند تأیید کاربر"
            });

            // Mapping to Sepidar test record
            context.InventoryItemMappings.Add(new InventoryItemMapping
            {
                Id = Guid.NewGuid(),
                MaterialId = milk250ML.Id,
                SepidarItemId = "TEST-SEP-702",
                SepidarItemCode = "TEST-1002",
                SepidarItemTitle = "شیر پاکتی دامداران ۲۵۰ سی‌سی (آزمایشی)",
                SepidarBaseUnit = "عددی",
                IsActive = true,
                LastSyncUtc = DateTime.UtcNow
            });

            // Snapshot in central warehouse: 400 counts
            if (wh != null)
            {
                context.InventorySnapshots.Add(new InventorySnapshot
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = wh.Id,
                    MaterialId = milk250ML.Id,
                    UnitId = countUnit.Id,
                    OnHandQuantity = 400m,
                    ReservableQuantity = 400m,
                    IncomingQuantity = 0m,
                    ReservedQuantity = 0m,
                    SnapshotTimeUtc = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
