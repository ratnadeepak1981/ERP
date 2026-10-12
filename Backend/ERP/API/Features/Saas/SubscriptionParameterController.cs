using API.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using Security.Interfaces;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionParameterController : ControllerBase
{
    private readonly ISubscriptionParameterService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public SubscriptionParameterController(
        ISubscriptionParameterService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    [HttpPost]
    [RequirePermission("SUBSCRIPTION.CREATE")]
    public IActionResult Create(
        [FromBody] CreateSubscriptionParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }
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
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetAll()
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        IReadOnlyList<SubscriptionParameter> result =
            _service.GetAll();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetById(Guid id)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

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

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(
        Guid id,
        [FromBody] UpdateSubscriptionParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionParameter result =
                _service.Update(id, request);

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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
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
}
