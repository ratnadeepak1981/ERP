using API.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using Security.Interfaces;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionLimitController : ControllerBase
{
    private readonly ISubscriptionLimitService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public SubscriptionLimitController(
        ISubscriptionLimitService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    [HttpPost]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Create(
        [FromBody] CreateSubscriptionLimitRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }
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
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetById(Guid id)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

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
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetAll()
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var result = _service.GetAll();

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(
        Guid id,
        [FromBody] UpdateSubscriptionLimitRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

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