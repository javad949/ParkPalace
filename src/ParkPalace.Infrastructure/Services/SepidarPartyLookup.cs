using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Infrastructure.Services;

/// <summary>
/// Local development lookup. Sepidar party search stays disconnected until a live adapter is configured.
/// </summary>
public class SepidarPartyLookup : ISepidarPartyLookup
{
    public Task<SepidarCodeLookupDto> FindByCodeAsync(string code, CancellationToken ct = default)
    {
        return Task.FromResult(new SepidarCodeLookupDto
        {
            Connected = false,
            Unique = false,
            Code = code,
            Title = string.Empty
        });
    }

    public Task<SepidarPartySearchResultDto> SearchAsync(string? search, CancellationToken ct = default)
    {
        return Task.FromResult(new SepidarPartySearchResultDto
        {
            Connected = false,
            Message = "جستجوی زنده سپیدار در محیط توسعه محلی در دسترس نیست.",
            Hits = new List<SepidarPartySearchHitDto>()
        });
    }

    public Task<PagedResult<SepidarPartyListRow>> ListPagedAsync(DirectoryListQuery query, CancellationToken ct = default)
    {
        return Task.FromResult(new PagedResult<SepidarPartyListRow>
        {
            Items = new List<SepidarPartyListRow>(),
            TotalCount = 0,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        });
    }
}
