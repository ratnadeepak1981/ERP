using API.Common;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Domain;

[ApiController]
[Route("api/[controller]")]
public class DomainController : BaseApiController
{
    private readonly DomainService _domainService;

    public DomainController(DomainService domainService)
    {
        _domainService = domainService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return SuccessResponse(_domainService.GetStatus());
    }
}