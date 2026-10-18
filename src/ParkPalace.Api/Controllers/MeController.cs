using Microsoft.AspNetCore.Mvc;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public MeController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public ActionResult<MeResponseDto> Get()
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "احراز هویت نشده",
                Detail = "دسترسی به این منبع نیازمند احراز هویت است."
            });
        }

        var response = new MeResponseDto
        {
            Id = _currentUserService.UserId,
            Username = _currentUserService.Username ?? string.Empty,
            DisplayName = _currentUserService.DisplayName ?? _currentUserService.Username ?? string.Empty,
            AdSid = _currentUserService.AdSid,
            IsAuthenticated = true,
            Roles = _currentUserService.Roles.ToList(),
            Permissions = _currentUserService.Permissions.ToList()
        };

        return Ok(response);
    }
}
