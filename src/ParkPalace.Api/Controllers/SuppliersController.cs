using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Common;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public SuppliersController(ICrmDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PartyDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] PartyType? partyType,
        [FromQuery] Guid? suppliedMaterialId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var query = _dbContext.Parties
            .Include(p => p.Phones)
            .Include(p => p.Contacts)
            .Include(p => p.Addresses)
            .Include(p => p.SupplierMaterials)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        if (partyType.HasValue)
            query = query.Where(p => p.PartyType == partyType.Value);

        if (suppliedMaterialId.HasValue)
            query = query.Where(p => p.SupplierMaterials.Any(sm => sm.MaterialId == suppliedMaterialId.Value));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var normalizedPhone = PhoneNormalizer.Normalize(s);

            query = query.Where(p =>
                p.DisplayName.Contains(s) ||
                (p.LegalName != null && p.LegalName.Contains(s)) ||
                (p.ShopName != null && p.ShopName.Contains(s)) ||
                (p.NationalId != null && p.NationalId.Contains(s)) ||
                (p.NationalIdentifier != null && p.NationalIdentifier.Contains(s)) ||
                (p.EconomicCode != null && p.EconomicCode.Contains(s)) ||
                p.Contacts.Any(c => c.PersonName.Contains(s)) ||
                p.SupplierMaterials.Any(sm => sm.Material != null && sm.Material.Name.Contains(s)) ||
                p.Phones.Any(ph => ph.OriginalValue.Contains(s) || (normalizedPhone.Length > 3 && ph.NormalizedValue.Contains(normalizedPhone)))
            );
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PartyDto
            {
                Id = p.Id,
                PartyType = p.PartyType,
                DisplayName = p.DisplayName,
                IsActive = p.IsActive,
                Notes = p.Notes,
                FirstName = p.FirstName,
                LastName = p.LastName,
                ShopName = p.ShopName,
                NationalId = p.NationalId,
                LegalName = p.LegalName,
                NationalIdentifier = p.NationalIdentifier,
                RegistrationNumber = p.RegistrationNumber,
                EconomicCode = p.EconomicCode,
                PrimaryPhone = p.Phones.Where(ph => ph.IsPrimary).Select(ph => ph.OriginalValue).FirstOrDefault() ??
                               p.Phones.Select(ph => ph.OriginalValue).FirstOrDefault(),
                PrimaryContactName = p.Contacts.Where(c => c.IsPrimary).Select(c => c.PersonName).FirstOrDefault() ??
                                     p.Contacts.Select(c => c.PersonName).FirstOrDefault(),
                PrimaryAddressCity = p.Addresses.Where(a => a.IsPrimary).Select(a => a.City).FirstOrDefault() ??
                                     p.Addresses.Select(a => a.City).FirstOrDefault(),
                SuppliedMaterialsCount = p.SupplierMaterials.Count
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<PartyDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartyDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var p = await _dbContext.Parties
            .Include(x => x.Contacts).ThenInclude(c => c.Phones)
            .Include(x => x.Phones).ThenInclude(ph => ph.Contact)
            .Include(x => x.Addresses)
            .Include(x => x.Documents)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.Material)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.PurchaseUnit)
            .Include(x => x.SupplierMaterials).ThenInclude(sm => sm.PriceHistories).ThenInclude(ph => ph.Unit)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تأمین‌کننده مورد نظر یافت نشد" });

        var canViewPrices = _currentUserService.HasPermission(Permissions.SuppliersPricesView);

        var detail = new PartyDetailDto
        {
            Id = p.Id,
            PartyType = p.PartyType,
            DisplayName = p.DisplayName,
            IsActive = p.IsActive,
            Notes = p.Notes,
            FirstName = p.FirstName,
            LastName = p.LastName,
            ShopName = p.ShopName,
            NationalId = p.NationalId,
            LegalName = p.LegalName,
            NationalIdentifier = p.NationalIdentifier,
            RegistrationNumber = p.RegistrationNumber,
            EconomicCode = p.EconomicCode,
            PrimaryPhone = p.Phones.Where(ph => ph.IsPrimary).Select(ph => ph.OriginalValue).FirstOrDefault(),
            PrimaryContactName = p.Contacts.Where(c => c.IsPrimary).Select(c => c.PersonName).FirstOrDefault(),
            PrimaryAddressCity = p.Addresses.Where(a => a.IsPrimary).Select(a => a.City).FirstOrDefault(),
            SuppliedMaterialsCount = p.SupplierMaterials.Count,
            Contacts = p.Contacts.Select(c => new PartyContactDto
            {
                Id = c.Id,
                PartyId = c.PartyId,
                PersonName = c.PersonName,
                Position = c.Position,
                IsPrimary = c.IsPrimary,
                IsActive = c.IsActive,
                Phones = c.Phones.Select(ph => new PartyPhoneDto
                {
                    Id = ph.Id,
                    PartyId = ph.PartyId,
                    ContactId = ph.ContactId,
                    ContactName = c.PersonName,
                    PhoneType = ph.PhoneType,
                    OriginalValue = ph.OriginalValue,
                    NormalizedValue = ph.NormalizedValue,
                    IsPrimary = ph.IsPrimary
                }).ToList()
            }).ToList(),
            Phones = p.Phones.Select(ph => new PartyPhoneDto
            {
                Id = ph.Id,
                PartyId = ph.PartyId,
                ContactId = ph.ContactId,
                ContactName = ph.Contact?.PersonName,
                PhoneType = ph.PhoneType,
                OriginalValue = ph.OriginalValue,
                NormalizedValue = ph.NormalizedValue,
                IsPrimary = ph.IsPrimary
            }).ToList(),
            Addresses = p.Addresses.Select(a => new PartyAddressDto
            {
                Id = a.Id,
                PartyId = a.PartyId,
                AddressType = a.AddressType,
                Province = a.Province,
                City = a.City,
                AddressText = a.AddressText,
                PostalCode = a.PostalCode,
                IsPrimary = a.IsPrimary
            }).ToList(),
            SuppliedMaterials = p.SupplierMaterials.Select(sm => new SupplierMaterialDto
            {
                Id = sm.Id,
                PartyId = sm.PartyId,
                SupplierName = p.DisplayName,
                MaterialId = sm.MaterialId,
                MaterialName = sm.Material?.Name ?? string.Empty,
                MaterialCode = sm.Material?.InternalCode ?? string.Empty,
                IsPreferred = sm.IsPreferred,
                Status = sm.Status,
                DefaultLeadTimeDays = sm.DefaultLeadTimeDays,
                MinimumOrderQty = sm.MinimumOrderQty,
                PurchaseUnitId = sm.PurchaseUnitId,
                PurchaseUnitName = sm.PurchaseUnit?.Name,
                Notes = sm.Notes,
                PriceHistories = canViewPrices ? sm.PriceHistories
                    .OrderByDescending(ph => ph.EffectiveFrom)
                    .Select(ph => new SupplierPriceHistoryDto
                    {
                        Id = ph.Id,
                        SupplierMaterialId = ph.SupplierMaterialId,
                        Price = ph.Price,
                        Currency = ph.Currency,
                        UnitId = ph.UnitId,
                        UnitName = ph.Unit?.Name ?? string.Empty,
                        EffectiveFrom = ph.EffectiveFrom,
                        EffectiveTo = ph.EffectiveTo,
                        RecordedAt = ph.RecordedAt,
                        Source = ph.Source,
                        Notes = ph.Notes
                    }).ToList() : new List<SupplierPriceHistoryDto>()
            }).ToList(),
            Documents = p.Documents.Select(d => new PartyDocumentDto
            {
                Id = d.Id,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                FileExtension = d.FileExtension,
                FileSizeBytes = d.FileSizeBytes,
                ContentType = d.ContentType,
                UploadedAtUtc = d.UploadedAtUtc
            }).ToList()
        };

        return Ok(detail);
    }

    [HttpPost]
    public async Task<ActionResult<PartyDto>> Create([FromBody] CreatePartyRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        // Duplicate checks
        if (req.PartyType == PartyType.LegalEntity && !string.IsNullOrWhiteSpace(req.NationalIdentifier))
        {
            var exists = await _dbContext.Parties.AnyAsync(p => p.NationalIdentifier == req.NationalIdentifier.Trim(), ct);
            if (exists)
                return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = $"شناسه ملی '{req.NationalIdentifier}' قبلاً ثبت شده است" });
        }
        else if (req.PartyType == PartyType.Individual && !string.IsNullOrWhiteSpace(req.NationalId))
        {
            var exists = await _dbContext.Parties.AnyAsync(p => p.NationalId == req.NationalId.Trim(), ct);
            if (exists)
                return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = $"کد ملی '{req.NationalId}' قبلاً ثبت شده است" });
        }

        var party = new Party
        {
            PartyType = req.PartyType,
            DisplayName = req.DisplayName.Trim(),
            Notes = req.Notes?.Trim(),
            FirstName = req.FirstName?.Trim(),
            LastName = req.LastName?.Trim(),
            ShopName = req.ShopName?.Trim(),
            NationalId = req.NationalId?.Trim(),
            LegalName = req.LegalName?.Trim(),
            NationalIdentifier = req.NationalIdentifier?.Trim(),
            RegistrationNumber = req.RegistrationNumber?.Trim(),
            EconomicCode = req.EconomicCode?.Trim(),
            IsActive = true
        };

        // Add primary contact if provided
        PartyContact? contact = null;
        if (!string.IsNullOrWhiteSpace(req.PrimaryContactName))
        {
            contact = new PartyContact
            {
                PersonName = req.PrimaryContactName.Trim(),
                Position = req.PrimaryContactPosition?.Trim(),
                IsPrimary = true,
                IsActive = true
            };
            party.Contacts.Add(contact);
        }

        // Add primary phone if provided
        if (!string.IsNullOrWhiteSpace(req.PrimaryPhone))
        {
            var normalized = PhoneNormalizer.Normalize(req.PrimaryPhone);
            party.Phones.Add(new PartyPhone
            {
                OriginalValue = req.PrimaryPhone.Trim(),
                NormalizedValue = normalized,
                PhoneType = req.PrimaryPhoneType ?? "Mobile",
                IsPrimary = true,
                Contact = contact
            });
        }

        // Add primary address if provided
        if (!string.IsNullOrWhiteSpace(req.PrimaryAddressCity) || !string.IsNullOrWhiteSpace(req.PrimaryAddressText))
        {
            party.Addresses.Add(new PartyAddress
            {
                City = req.PrimaryAddressCity?.Trim() ?? "تهران",
                Province = "تهران",
                AddressText = req.PrimaryAddressText?.Trim() ?? string.Empty,
                IsPrimary = true
            });
        }

        _dbContext.Parties.Add(party);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = party.Id }, new PartyDto
        {
            Id = party.Id,
            PartyType = party.PartyType,
            DisplayName = party.DisplayName,
            IsActive = party.IsActive,
            Notes = party.Notes,
            ShopName = party.ShopName,
            LegalName = party.LegalName,
            EconomicCode = party.EconomicCode,
            PrimaryPhone = party.Phones.FirstOrDefault()?.OriginalValue,
            PrimaryContactName = contact?.PersonName
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PartyDto>> Update(Guid id, [FromBody] UpdatePartyRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تأمین‌کننده مورد نظر یافت نشد" });

        party.DisplayName = req.DisplayName.Trim();
        party.Notes = req.Notes?.Trim();
        party.FirstName = req.FirstName?.Trim();
        party.LastName = req.LastName?.Trim();
        party.ShopName = req.ShopName?.Trim();
        party.NationalId = req.NationalId?.Trim();
        party.LegalName = req.LegalName?.Trim();
        party.NationalIdentifier = req.NationalIdentifier?.Trim();
        party.RegistrationNumber = req.RegistrationNumber?.Trim();
        party.EconomicCode = req.EconomicCode?.Trim();
        party.IsActive = req.IsActive;

        await _dbContext.SaveChangesAsync(ct);

        return Ok(new PartyDto
        {
            Id = party.Id,
            PartyType = party.PartyType,
            DisplayName = party.DisplayName,
            IsActive = party.IsActive,
            Notes = party.Notes,
            ShopName = party.ShopName,
            LegalName = party.LegalName,
            EconomicCode = party.EconomicCode
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party == null)
            return NotFound();

        // Soft delete / deactivate
        party.IsActive = false;
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "تأمین‌کننده با موفقیت غیرفعال شد", id });
    }

    [HttpPost("{id:guid}/contacts")]
    public async Task<ActionResult<PartyContactDto>> AddContact(Guid id, [FromBody] CreatePartyContactRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تأمین‌کننده مورد نظر یافت نشد" });

        var contact = new PartyContact
        {
            PartyId = id,
            PersonName = req.PersonName.Trim(),
            Position = req.Position?.Trim(),
            IsPrimary = req.IsPrimary,
            IsActive = true
        };

        _dbContext.PartyContacts.Add(contact);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new PartyContactDto
        {
            Id = contact.Id,
            PartyId = contact.PartyId,
            PersonName = contact.PersonName,
            Position = contact.Position,
            IsPrimary = contact.IsPrimary,
            IsActive = contact.IsActive
        });
    }

    [HttpPost("{id:guid}/phones")]
    public async Task<ActionResult<PartyPhoneDto>> AddPhone(Guid id, [FromBody] CreatePartyPhoneRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تأمین‌کننده مورد نظر یافت نشد" });

        var normalized = PhoneNormalizer.Normalize(req.PhoneNumber);

        var phone = new PartyPhone
        {
            PartyId = id,
            ContactId = req.ContactId,
            PhoneType = req.PhoneType,
            OriginalValue = req.PhoneNumber.Trim(),
            NormalizedValue = normalized,
            IsPrimary = req.IsPrimary
        };

        _dbContext.PartyPhones.Add(phone);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new PartyPhoneDto
        {
            Id = phone.Id,
            PartyId = phone.PartyId,
            ContactId = phone.ContactId,
            PhoneType = phone.PhoneType,
            OriginalValue = phone.OriginalValue,
            NormalizedValue = phone.NormalizedValue,
            IsPrimary = phone.IsPrimary
        });
    }

    [HttpPost("{id:guid}/addresses")]
    public async Task<ActionResult<PartyAddressDto>> AddAddress(Guid id, [FromBody] CreatePartyAddressRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تأمین‌کننده مورد نظر یافت نشد" });

        var address = new PartyAddress
        {
            PartyId = id,
            AddressType = req.AddressType,
            Province = req.Province.Trim(),
            City = req.City.Trim(),
            AddressText = req.AddressText.Trim(),
            PostalCode = req.PostalCode?.Trim(),
            IsPrimary = req.IsPrimary
        };

        _dbContext.PartyAddresses.Add(address);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new PartyAddressDto
        {
            Id = address.Id,
            PartyId = address.PartyId,
            AddressType = address.AddressType,
            Province = address.Province,
            City = address.City,
            AddressText = address.AddressText,
            PostalCode = address.PostalCode,
            IsPrimary = address.IsPrimary
        });
    }

    [HttpPost("{id:guid}/materials")]
    public async Task<ActionResult<SupplierMaterialDto>> AddMaterial(Guid id, [FromBody] AddSupplierMaterialRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "تأمین‌کننده یافت نشد" });

        var material = await _dbContext.Materials.FirstOrDefaultAsync(m => m.Id == req.MaterialId, ct);
        if (material == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "ماده اولیه مشخص شده یافت نشد" });

        var existing = await _dbContext.SupplierMaterials
            .FirstOrDefaultAsync(sm => sm.PartyId == id && sm.MaterialId == req.MaterialId, ct);

        if (existing != null)
            return Conflict(new ProblemDetails { Title = "تداخل داده", Detail = "این ماده اولیه قبلاً به این تأمین‌کننده منتسب شده است" });

        var sm = new SupplierMaterial
        {
            PartyId = id,
            MaterialId = req.MaterialId,
            IsPreferred = req.IsPreferred,
            Status = req.Status ?? "Active",
            DefaultLeadTimeDays = req.DefaultLeadTimeDays,
            MinimumOrderQty = req.MinimumOrderQty,
            PurchaseUnitId = req.PurchaseUnitId ?? material.BaseUnitId,
            Notes = req.Notes
        };

        // Add initial price quote if provided and authorized
        if (req.InitialPrice.HasValue && req.InitialPrice.Value >= 0 && _currentUserService.HasPermission(Permissions.SuppliersPricesManage))
        {
            sm.PriceHistories.Add(new SupplierPriceHistory
            {
                Price = req.InitialPrice.Value,
                Currency = req.Currency ?? "TOMAN",
                UnitId = req.PriceUnitId ?? material.BaseUnitId,
                EffectiveFrom = req.EffectiveFrom ?? DateTime.UtcNow,
                Source = req.PriceSource ?? "استعلام اولیه",
                RecordedAt = DateTime.UtcNow
            });
        }

        _dbContext.SupplierMaterials.Add(sm);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new SupplierMaterialDto
        {
            Id = sm.Id,
            PartyId = sm.PartyId,
            SupplierName = party.DisplayName,
            MaterialId = sm.MaterialId,
            MaterialName = material.Name,
            MaterialCode = material.InternalCode,
            IsPreferred = sm.IsPreferred,
            Status = sm.Status,
            DefaultLeadTimeDays = sm.DefaultLeadTimeDays,
            MinimumOrderQty = sm.MinimumOrderQty,
            PurchaseUnitId = sm.PurchaseUnitId,
            Notes = sm.Notes
        });
    }

    [HttpDelete("{id:guid}/materials/{materialId:guid}")]
    public async Task<ActionResult> RemoveMaterial(Guid id, Guid materialId, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var sm = await _dbContext.SupplierMaterials
            .FirstOrDefaultAsync(x => x.PartyId == id && x.MaterialId == materialId, ct);

        if (sm == null)
            return NotFound();

        _dbContext.SupplierMaterials.Remove(sm);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "انتساب کالا به تأمین‌کننده با موفقیت حذف شد" });
    }

    [HttpGet("{id:guid}/materials/{materialId:guid}/prices")]
    public async Task<ActionResult<List<SupplierPriceHistoryDto>>> GetPriceHistory(Guid id, Guid materialId, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersPricesView))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var sm = await _dbContext.SupplierMaterials
            .Include(x => x.PriceHistories).ThenInclude(ph => ph.Unit)
            .FirstOrDefaultAsync(x => x.PartyId == id && x.MaterialId == materialId, ct);

        if (sm == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "رابطه تأمین‌کننده و کالا یافت نشد" });

        var list = sm.PriceHistories
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => new SupplierPriceHistoryDto
            {
                Id = p.Id,
                SupplierMaterialId = p.SupplierMaterialId,
                Price = p.Price,
                Currency = p.Currency,
                UnitId = p.UnitId,
                UnitName = p.Unit != null ? p.Unit.Name : string.Empty,
                EffectiveFrom = p.EffectiveFrom,
                EffectiveTo = p.EffectiveTo,
                RecordedAt = p.RecordedAt,
                Source = p.Source,
                Notes = p.Notes
            })
            .ToList();

        return Ok(list);
    }

    [HttpPost("{id:guid}/materials/{materialId:guid}/prices")]
    public async Task<ActionResult<SupplierPriceHistoryDto>> AddPriceQuote(Guid id, Guid materialId, [FromBody] AddPriceHistoryRequest req, CancellationToken ct)
    {
        if (!_currentUserService.IsAuthenticated)
            return Unauthorized();

        if (!_currentUserService.HasPermission(Permissions.SuppliersPricesManage))
            return StatusCode(403, new ProblemDetails { Status = 403, Title = "عدم دسترسی", Detail = "شما مجوز انجام این عملیات را ندارید." });

        var sm = await _dbContext.SupplierMaterials
            .FirstOrDefaultAsync(x => x.PartyId == id && x.MaterialId == materialId, ct);

        if (sm == null)
            return NotFound(new ProblemDetails { Title = "یافت نشد", Detail = "رابطه کالا و تأمین‌کننده یافت نشد" });

        var unit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == req.UnitId, ct);
        if (unit == null)
            return BadRequest(new ProblemDetails { Title = "داده نامعتبر", Detail = "واحد سنجش قیمت یافت نشد" });

        var priceHistory = new SupplierPriceHistory
        {
            SupplierMaterialId = sm.Id,
            Price = req.Price,
            Currency = req.Currency ?? "TOMAN",
            UnitId = req.UnitId,
            EffectiveFrom = req.EffectiveFrom,
            EffectiveTo = req.EffectiveTo,
            Source = req.Source?.Trim(),
            Notes = req.Notes?.Trim(),
            RecordedAt = DateTime.UtcNow
        };

        _dbContext.SupplierPriceHistories.Add(priceHistory);
        await _dbContext.SaveChangesAsync(ct);

        return Ok(new SupplierPriceHistoryDto
        {
            Id = priceHistory.Id,
            SupplierMaterialId = priceHistory.SupplierMaterialId,
            Price = priceHistory.Price,
            Currency = priceHistory.Currency,
            UnitId = priceHistory.UnitId,
            UnitName = unit.Name,
            EffectiveFrom = priceHistory.EffectiveFrom,
            EffectiveTo = priceHistory.EffectiveTo,
            RecordedAt = priceHistory.RecordedAt,
            Source = priceHistory.Source,
            Notes = priceHistory.Notes
        });
    }
}
