using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface ISepidarPartyLookup
{
    Task<SepidarCodeLookupDto> FindByCodeAsync(string code, CancellationToken ct = default);

    Task<SepidarPartySearchResultDto> SearchAsync(string? search, CancellationToken ct = default);

    Task<PagedResult<SepidarPartyListRow>> ListPagedAsync(DirectoryListQuery query, CancellationToken ct = default);
}

public class SepidarCodeLookupDto
{
    public bool Connected { get; set; }
    public bool Unique { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? PartyRef { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class SepidarPartySearchHitDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? PartyRef { get; set; }
}

public class SepidarPartySearchResultDto
{
    public bool Connected { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SepidarPartySearchHitDto> Hits { get; set; } = new();
}

public class SepidarPartyListRow
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? PartyRef { get; set; }
}

public class DirectoryListQuery
{
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
