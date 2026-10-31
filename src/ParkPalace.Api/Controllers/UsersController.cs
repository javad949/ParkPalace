using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ICrmDbContext _dbContext;
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITelephonyAdapter _telephonyAdapter;
    private readonly ITelephonyAccessControlService _accessControlService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ICrmDbContext dbContext,
        IActiveDirectoryService activeDirectoryService,
        ICurrentUserService currentUserService,
        ITelephonyAdapter telephonyAdapter,
        ITelephonyAccessControlService accessControlService,
        ILogger<UsersController> logger)
    {
        _dbContext = dbContext;
        _activeDirectoryService = activeDirectoryService;
        _currentUserService = currentUserService;
        _telephonyAdapter = telephonyAdapter;
        _accessControlService = accessControlService;
        _logger = logger;
    }

    private bool IsAdminOrAuthorized()
    {
        if (string.Equals(_currentUserService.ActiveRole, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        if (_currentUserService.Roles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase))
            return true;

        if (_currentUserService.AvailableRoles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase))
            return true;

        return _currentUserService.HasPermission(Permissions.AdminUsers);
    }

    private ActionResult Forbidden(string message = "شما مجوز مدیریت کاربران و دسترسی‌ها را ندارید.")
    {
        return StatusCode(403, new ProblemDetails
        {
            Status = 403,
            Title = "عدم دسترسی",
            Detail = message
        });
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll(CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        var users = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var extensions = await _dbContext.UserExtensions.ToListAsync(ct);

        var dtoList = users.Select(u =>
        {
            var ext = FindOwnedExtension(extensions, u.Id, u.Username);

            return new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Email = u.Email,
                AdObjectSid = u.AdObjectSid,
                IsEnabled = u.IsEnabled,
                LastLoginAtUtc = u.LastLoginAtUtc,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList(),
                Extension = ext?.Extension,
                DeviceType = ext?.DeviceType
            };
        }).ToList();

        return Ok(dtoList);
    }

    [HttpGet("ad-directory")]
    public async Task<ActionResult<List<AdUserDirectoryDto>>> GetAdDirectoryUsers(CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        try
        {
            var adUsers = await _activeDirectoryService.GetDomainUsersAsync(ct);
            return Ok(adUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read domain users from PDC");
            return StatusCode(500, new ProblemDetails
            {
                Status = 500,
                Title = "خطای ارتباط با سرور PDC",
                Detail = ex.Message
            });
        }
    }    [HttpGet("available-extensions")]
    public async Task<ActionResult<List<AvailableExtensionDto>>> GetAvailableExtensions(CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        var pbxExtensions = await _telephonyAdapter.GetAvailableExtensionsAsync(ct);
        var dbExtensions = await _dbContext.UserExtensions.ToListAsync(ct);
        var dbUsers = await _dbContext.Users.ToDictionaryAsync(u => u.Id, ct);

        foreach (var ext in pbxExtensions)
        {
            var matched = dbExtensions.FirstOrDefault(e => e.Extension == ext.Extension && e.IsActive);
            if (matched != null)
            {
                ext.IsAssigned = true;
                ext.AssignedUserId = matched.UserId;
                ext.AssignedUsername = matched.Username;
                if (matched.UserId.HasValue && dbUsers.TryGetValue(matched.UserId.Value, out var u))
                {
                    ext.AssignedDisplayName = u.DisplayName;
                }
            }
        }

        // Also include any extensions in DB that weren't in PBX list
        foreach (var dbExt in dbExtensions.Where(e => e.IsActive && !pbxExtensions.Any(p => p.Extension == e.Extension)))
        {
            var dto = new AvailableExtensionDto
            {
                Extension = dbExt.Extension,
                DisplayName = $"داخلی {dbExt.Extension}",
                DeviceType = dbExt.DeviceType,
                IsAssigned = true,
                AssignedUserId = dbExt.UserId,
                AssignedUsername = dbExt.Username
            };
            if (dbExt.UserId.HasValue && dbUsers.TryGetValue(dbExt.UserId.Value, out var u))
            {
                dto.AssignedDisplayName = u.DisplayName;
            }
            pbxExtensions.Add(dto);
        }

        return Ok(pbxExtensions.OrderBy(e => int.TryParse(e.Extension, out var num) ? num : 9999).ToList());
    }


    [HttpPost("sync-ad")]
    public async Task<ActionResult> SyncAdUsers([FromBody] SyncAdUsersRequest req, CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        var adUsers = await _activeDirectoryService.GetDomainUsersAsync(ct);
        var crmUsers = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ToListAsync(ct);

        var rolesDict = await _dbContext.Roles.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, ct);
        Role? defaultRole = null;
        if (!string.IsNullOrWhiteSpace(req.DefaultRole) && rolesDict.TryGetValue(req.DefaultRole, out var matchedRole))
        {
            defaultRole = matchedRole;
        }

        var extensions = await _dbContext.UserExtensions.ToListAsync(ct);

        int addedCount = 0;
        int updatedCount = 0;

        var targetAdUsers = (req.SamAccountNames != null && req.SamAccountNames.Any())
            ? adUsers.Where(u => req.SamAccountNames.Contains(u.SamAccountName, StringComparer.OrdinalIgnoreCase)).ToList()
            : adUsers;

        foreach (var adUser in targetAdUsers)
        {
            // Unique SID, otherwise one exact SAHAR\account. Short names and a second domain must not bind this account.
            var existingUser = SelectImportedUser(crmUsers, adUser.Sid, adUser.SamAccountName, out var identityAmbiguous);
            if (identityAmbiguous)
            {
                continue;
            }

            if (existingUser == null)
            {
                var sid = !string.IsNullOrWhiteSpace(adUser.Sid)
                    ? adUser.Sid
                    : $"SAHAR-SID-{adUser.SamAccountName.ToUpperInvariant()}";

                if (crmUsers.Any(u => u.AdObjectSid == sid))
                {
                    sid = $"{sid}-{Guid.NewGuid():N}";
                }

                // Create user
                var newUser = new AppUser
                {
                    Username = $"SAHAR\\{adUser.SamAccountName}",
                    DisplayName = !string.IsNullOrWhiteSpace(adUser.DisplayName) ? adUser.DisplayName : adUser.SamAccountName,
                    Email = adUser.Email,
                    AdObjectSid = sid,
                    IsEnabled = !adUser.IsDisabled
                };

                // Zero roles by default ("در ابتدا هیچ نقشی به هیچ کاربری داده نشود")
                // Only grant role if domain Administrator or explicitly specified
                if (adUser.SamAccountName.Equals("Administrator", StringComparison.OrdinalIgnoreCase) && rolesDict.TryGetValue(Roles.Admin, out var adminRole))
                {
                    newUser.UserRoles.Add(new UserRole
                    {
                        UserId = newUser.Id,
                        User = newUser,
                        RoleId = adminRole.Id
                    });
                }
                else if (defaultRole != null)
                {
                    newUser.UserRoles.Add(new UserRole
                    {
                        UserId = newUser.Id,
                        User = newUser,
                        RoleId = defaultRole.Id
                    });
                }

                _dbContext.Users.Add(newUser);
                crmUsers.Add(newUser);
                addedCount++;

                // Assign extension if present in AD and not conflicting
                var adExt = !string.IsNullOrWhiteSpace(adUser.IpPhone) ? adUser.IpPhone : adUser.TelephoneNumber;
                if (!string.IsNullOrWhiteSpace(adExt))
                {
                    var trimmed = adExt.Trim();
                    if (trimmed.All(char.IsDigit) && trimmed.Length <= 10 && !extensions.Any(e => e.Extension == trimmed))
                    {
                        var userExt = new UserExtension
                        {
                            UserId = newUser.Id,
                            User = newUser,
                            Username = newUser.Username,
                            Extension = trimmed,
                            DeviceType = "SIP",
                            IsActive = true,
                            Description = $"وارد شده از Active Directory ({adUser.DisplayName})"
                        };
                        _dbContext.UserExtensions.Add(userExt);
                        extensions.Add(userExt);
                    }
                }
            }
            else
            {
                // Update existing user details from AD
                if (!string.IsNullOrWhiteSpace(adUser.DisplayName) && adUser.DisplayName != adUser.SamAccountName)
                {
                    existingUser.DisplayName = adUser.DisplayName;
                }
                if (!string.IsNullOrWhiteSpace(adUser.Email))
                {
                    existingUser.Email = adUser.Email;
                }
                if (!string.IsNullOrWhiteSpace(adUser.Sid) && !crmUsers.Any(u => u.Id != existingUser.Id && u.AdObjectSid == adUser.Sid))
                {
                    existingUser.AdObjectSid = adUser.Sid;
                }

                if (req.OverwriteExistingExtensions)
                {
                    var adExt = !string.IsNullOrWhiteSpace(adUser.IpPhone) ? adUser.IpPhone : adUser.TelephoneNumber;
                    if (!string.IsNullOrWhiteSpace(adExt))
                    {
                        var trimmed = adExt.Trim();
                        if (trimmed.All(char.IsDigit) && trimmed.Length <= 10)
                        {
                            var existingExt = FindOwnedExtension(extensions, existingUser.Id, existingUser.Username);

                            var conflict = extensions.FirstOrDefault(e => e.Extension == trimmed && (existingExt == null || e.Id != existingExt.Id));
                            if (conflict == null)
                            {
                                if (existingExt != null)
                                {
                                    existingExt.Extension = trimmed;
                                    existingExt.IsActive = true;
                                }
                                else
                                {
                                    var userExt = new UserExtension
                                    {
                                        UserId = existingUser.Id,
                                        Username = existingUser.Username,
                                        Extension = trimmed,
                                        DeviceType = "SIP",
                                        IsActive = true,
                                        Description = $"وارد شده از Active Directory ({adUser.DisplayName})"
                                    };
                                    _dbContext.UserExtensions.Add(userExt);
                                    extensions.Add(userExt);
                                }
                            }
                        }
                    }
                }
                updatedCount++;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            await _accessControlService.SyncActiveConnectionsAsync(ct);
            _logger.LogInformation("Active Directory Sync completed: {Added} added, {Updated} updated", addedCount, updatedCount);
        }
        catch (DbUpdateException dbEx)
        {
            var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
            _logger.LogError(dbEx, "Database error during Active Directory Sync: {Message}", innerMsg);
            return StatusCode(500, new ProblemDetails
            {
                Status = 500,
                Title = "خطای پایگاه داده در همگام‌سازی",
                Detail = innerMsg
            });
        }

        return Ok(new
        {
            message = "همگام‌سازی کاربران اکتیودایرکتوری با موفقیت انجام شد",
            addedCount,
            updatedCount,
            totalDomainUsers = adUsers.Count
        });
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult> UpdateRoles(Guid id, [FromBody] UpdateUserRolesRequest req, CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user == null)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "یافت نشد", Detail = "کاربر مورد نظر یافت نشد" });
        }

        var availableRoles = await _dbContext.Roles.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, ct);

        // Remove old roles
        _dbContext.UserRoles.RemoveRange(user.UserRoles);

        // Add requested roles
        foreach (var roleName in req.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (availableRoles.TryGetValue(roleName, out var roleEntity))
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleEntity.Id
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        await _accessControlService.SyncActiveConnectionsAsync(ct);
        return Ok(new { message = "نقش‌ها و سطوح دسترسی کاربر با موفقیت به‌روزرسانی شد", roles = req.Roles });
    }

    [HttpPut("{id:guid}/extension")]
    public async Task<ActionResult> UpdateExtension(Guid id, [FromBody] UpdateUserExtensionRequest req, CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "یافت نشد", Detail = "کاربر مورد نظر یافت نشد" });
        }

        var extensionRows = await _dbContext.UserExtensions.ToListAsync(ct);
        var existingExt = FindOwnedExtension(extensionRows, user.Id, user.Username);

        if (string.IsNullOrWhiteSpace(req.Extension))
        {
            if (existingExt != null)
            {
                existingExt.IsActive = false;
                existingExt.Extension = string.Empty;
            }
        }
        else
        {
            var extTrimmed = req.Extension.Trim();

            // Check if extension is used by another user
            var inUse = await _dbContext.UserExtensions
                .AnyAsync(e => e.Extension == extTrimmed && e.UserId != user.Id && e.IsActive, ct);

            if (inUse)
            {
                return Conflict(new ProblemDetails
                {
                    Status = 409,
                    Title = "تداخل شماره داخلی",
                    Detail = $"شماره داخلی {extTrimmed} قبلاً به کاربر دیگری اختصاص یافته است."
                });
            }

            if (existingExt != null)
            {
                existingExt.Extension = extTrimmed;
                existingExt.DeviceType = string.IsNullOrWhiteSpace(req.DeviceType) ? "SIP" : req.DeviceType;
                existingExt.IsActive = true;
                existingExt.Description = req.Description;
            }
            else
            {
                var newExt = new UserExtension
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Extension = extTrimmed,
                    DeviceType = string.IsNullOrWhiteSpace(req.DeviceType) ? "SIP" : req.DeviceType,
                    IsActive = true,
                    Description = req.Description
                };
                _dbContext.UserExtensions.Add(newExt);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        await _accessControlService.SyncActiveConnectionsAsync(ct);
        return Ok(new { message = "شماره داخلی کاربر با موفقیت ذخیره شد", extension = req.Extension });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest req, CancellationToken ct)
    {
        if (!IsAdminOrAuthorized()) return Forbidden();

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "یافت نشد", Detail = "کاربر مورد نظر یافت نشد" });
        }

        user.IsEnabled = req.IsEnabled;
        await _dbContext.SaveChangesAsync(ct);
        await _accessControlService.SyncActiveConnectionsAsync(ct);

        return Ok(new { message = $"وضعیت کاربر با موفقیت به {(req.IsEnabled ? "فعال" : "غیرفعال")} تغییر یافت", isEnabled = req.IsEnabled });
    }

    private static AppUser? SelectImportedUser(List<AppUser> crmUsers, string? sid, string samAccountName, out bool ambiguous)
    {
        ambiguous = false;
        if (!string.IsNullOrEmpty(sid))
        {
            var bySid = crmUsers.Where(u => u.AdObjectSid == sid).ToList();
            if (bySid.Count == 1) return bySid[0];
            if (bySid.Count > 1)
            {
                ambiguous = true;
                return null;
            }
        }

        var canonical = $"SAHAR\\{samAccountName}";
        var byName = crmUsers
            .Where(u => string.Equals(u.Username, canonical, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byName.Count == 1) return byName[0];
        if (byName.Count > 1) ambiguous = true;
        return null;
    }

    private static UserExtension? FindOwnedExtension(IEnumerable<UserExtension> extensions, Guid userId, string? username)
    {
        var rows = extensions.ToList();
        var byUserId = rows.Where(e => e.UserId == userId).ToList();
        if (byUserId.Count > 0) return byUserId[0];

        if (string.IsNullOrEmpty(username)) return null;
        var byExactName = rows
            .Where(e => string.Equals(e.Username, username, StringComparison.OrdinalIgnoreCase)
                && (e.UserId == null || e.UserId == userId))
            .ToList();
        return byExactName.Count == 1 ? byExactName[0] : null;
    }
}
