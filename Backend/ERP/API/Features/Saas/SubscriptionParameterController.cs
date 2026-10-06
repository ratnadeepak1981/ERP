using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;

namespace API.Features.Saas;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionParameterController : ControllerBase
{
    private readonly ISubscriptionParameterService _service;

    public SubscriptionParameterController(
        ISubscriptionParameterService service)
    {
        _service = service;
    }

    [HttpPost]
    public IActionResult Create(
        [FromBody] CreateSubscriptionParameterRequest request)
    {
        try
        {
            SubscriptionParameter result =
                _service.Create(request);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        IReadOnlyList<SubscriptionParameter> result =
            _service.GetAll();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        SubscriptionParameter? result =
            _service.GetById(id);

        if (result == null)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Subscription parameter not found."
            });
        }

        return Ok(result);
    }
}