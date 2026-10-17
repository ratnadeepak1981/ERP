using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Services;

namespace API.Features.SaaS;

[ApiController]
[Route("api/[controller]")]
public class SaaSController : BaseApiController
{
    private readonly SaaSService _saasService;

    public SaaSController(SaaSService saasService)
    {
        _saasService = saasService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return SuccessResponse(_saasService.GetStatus());
    }
}