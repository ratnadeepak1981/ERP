using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Domain;

[ApiController]
[Route("api/[controller]")]
public class DomainController : ControllerBase
{
    private readonly DomainService _domainService;

    public DomainController(DomainService domainService)
    {
        _domainService = domainService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_domainService.GetStatus());
    }
}