using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.ActiveDirectory;

public class ActiveDirectoryService : IActiveDirectoryService
{
    private readonly IConfiguration _configuration;
    private readonly ICrmDbContext _dbContext;
    private readonly ILogger<ActiveDirectoryService> _logger;

    public ActiveDirectoryService(
        IConfiguration configuration,
        ICrmDbContext dbContext,
        ILogger<ActiveDirectoryService> logger)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
    }

    private (string host, string baseDn, string? username, string? password) GetAdConfig()
    {
        var host = _configuration["ActiveDirectory:PdcHost"] ?? "pdc.sahar.loc";
        var baseDn = _configuration["ActiveDirectory:BaseDn"] ?? "DC=sahar,DC=loc";
        var username = _configuration["ActiveDirectory:BindUser"] ?? "SAHAR\\Administrator";
        var password = _configuration["ActiveDirectory:BindPassword"] ?? "OR@1405.srv";

        return (host, baseDn, username, password);
    }

    public async Task<List<AdUserDirectoryDto>> GetDomainUsersAsync(CancellationToken ct = default)
    {
        var (host, baseDn, username, password) = GetAdConfig();
        var adUsers = new List<AdUserDirectoryDto>();

        var hostsToTry = new[] { host, "192.168.1.250" }.Distinct();
        Exception? lastEx = null;

        foreach (var serverHost in hostsToTry)
        {
            try
            {
                var ldapPath = $"LDAP://{serverHost}/{baseDn}";
                using var entry = new DirectoryEntry(ldapPath, username, password, AuthenticationTypes.Secure);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = "(&(objectCategory=person)(objectClass=user))",
                    PageSize = 250,
                    SearchScope = SearchScope.Subtree
                };

                searcher.PropertiesToLoad.AddRange(new[]
                {
                    "samaccountname",
                    "displayname",
                    "mail",
                    "title",
                    "department",
                    "telephonenumber",
                    "ipphone",
                    "mobile",
                    "objectsid",
                    "useraccountcontrol"
                });

                var results = searcher.FindAll();
                _logger.LogInformation("Queried Active Directory PDC at {Host}: found {Count} user entries", serverHost, results.Count);

                foreach (SearchResult sr in results)
                {
                    var p = sr.Properties;
                    var sam = p.Contains("samaccountname") && p["samaccountname"].Count > 0 ? p["samaccountname"][0]?.ToString() : null;
                    if (string.IsNullOrWhiteSpace(sam) || sam.EndsWith("$"))
                    {
                        // Skip machine accounts or blank
                        continue;
                    }

                    var uac = p.Contains("useraccountcontrol") && p["useraccountcontrol"].Count > 0 ? (int)p["useraccountcontrol"][0]! : 0;
                    var isDisabled = (uac & 2) != 0;

                    string sidStr = string.Empty;
                    if (p.Contains("objectsid") && p["objectsid"].Count > 0 && p["objectsid"][0] is byte[] sidBytes)
                    {
                        sidStr = new SecurityIdentifier(sidBytes, 0).Value;
                    }

                    var dto = new AdUserDirectoryDto
                    {
                        SamAccountName = sam,
                        DisplayName = p.Contains("displayname") && p["displayname"].Count > 0 ? p["displayname"][0]?.ToString() ?? sam : sam,
                        Email = p.Contains("mail") && p["mail"].Count > 0 ? p["mail"][0]?.ToString() : null,
                        Title = p.Contains("title") && p["title"].Count > 0 ? p["title"][0]?.ToString() : null,
                        Department = p.Contains("department") && p["department"].Count > 0 ? p["department"][0]?.ToString() : null,
                        TelephoneNumber = p.Contains("telephonenumber") && p["telephonenumber"].Count > 0 ? p["telephonenumber"][0]?.ToString() : null,
                        IpPhone = p.Contains("ipphone") && p["ipphone"].Count > 0 ? p["ipphone"][0]?.ToString() : null,
                        Mobile = p.Contains("mobile") && p["mobile"].Count > 0 ? p["mobile"][0]?.ToString() : null,
                        Sid = sidStr,
                        IsDisabled = isDisabled
                    };

                    adUsers.Add(dto);
                }

                lastEx = null;
                break; // Successfully queried!
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "Failed to query AD from {Host}, attempting fallback...", serverHost);
            }
        }

        if (lastEx != null && adUsers.Count == 0)
        {
            _logger.LogError(lastEx, "Failed to query Active Directory PDC on all endpoints.");
            throw new InvalidOperationException($"خطا در ارتباط با اکتیودایرکتوری PDC: {lastEx.Message}", lastEx);
        }

        // Cross-reference with CRM database users & extensions
        var crmUsers = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .ToListAsync(ct);

        var crmExtensions = await _dbContext.UserExtensions
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var adUser in adUsers)
        {
            AppUser? match = null;
            if (!string.IsNullOrEmpty(adUser.Sid))
            {
                var bySid = crmUsers.Where(u => u.AdObjectSid == adUser.Sid).ToList();
                if (bySid.Count == 1) match = bySid[0];
            }
            if (match == null && (string.IsNullOrEmpty(adUser.Sid) || !crmUsers.Any(u => u.AdObjectSid == adUser.Sid)))
            {
                var canonical = $"SAHAR\\{adUser.SamAccountName}";
                var byName = crmUsers.Where(u => u.Username.Equals(canonical, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byName.Count == 1) match = byName[0];
            }

            if (match != null)
            {
                adUser.IsImportedInCrm = true;
                adUser.ExistingUserId = match.Id;
                adUser.ExistingRoles = match.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();

                var byUserId = crmExtensions.Where(e => e.UserId == match.Id).ToList();
                var ext = byUserId.Count > 0 ? byUserId[0] : null;
                if (ext == null)
                {
                    var byName = crmExtensions.Where(e =>
                        e.Username.Equals(match.Username, StringComparison.OrdinalIgnoreCase) &&
                        (e.UserId == null || e.UserId == match.Id)).ToList();
                    if (byName.Count == 1) ext = byName[0];
                }

                if (ext != null)
                {
                    adUser.ExistingExtension = ext.Extension;
                }
            }
        }

        return adUsers.OrderByDescending(u => u.IsImportedInCrm).ThenBy(u => u.DisplayName).ToList();
    }

    public async Task<AdUserDirectoryDto?> GetUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        var users = await GetDomainUsersAsync(ct);
        var clean = username.Contains('\\') ? username.Split('\\')[1] : username;
        return users.FirstOrDefault(u => u.SamAccountName.Equals(clean, StringComparison.OrdinalIgnoreCase));
    }
}
