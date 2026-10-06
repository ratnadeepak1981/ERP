using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionLimitController : ControllerBase
{
    private readonly ISubscriptionLimitService _service;

    public SubscriptionLimitController(
        ISubscriptionLimitService service)
    {
        _service = service;
    }

    [HttpPost]
    public IActionResult Create(
        [FromBody] CreateSubscriptionLimitRequest request)
    {
        try
        {
            var result = _service.Create(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var result = _service.GetById(id);

        if (result == null)
        {
            return NotFound(new
            {
                message = "Subscription limit was not found."
            });
        }

        return Ok(result);
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var result = _service.GetAll();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(
        Guid id,
        [FromBody] UpdateSubscriptionLimitRequest request)
    {
        try
        {
            var result = _service.Update(id, request);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}