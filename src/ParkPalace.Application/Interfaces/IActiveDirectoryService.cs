using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IActiveDirectoryService
{
    Task<List<AdUserDirectoryDto>> GetDomainUsersAsync(CancellationToken ct = default);
    Task<AdUserDirectoryDto?> GetUserByUsernameAsync(string username, CancellationToken ct = default);
}
