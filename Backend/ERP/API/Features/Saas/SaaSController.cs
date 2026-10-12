using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Services;
using System.Security.Claims;

namespace API.Features.SaaS;

[ApiController]
[Route("api/[controller]")]
public class SaaSController : ControllerBase
{
    private readonly SaaSService _saasService;

    public SaaSController(
        SaaSService saasService)
    {
        _saasService = saasService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_saasService.GetStatus());
    }
}