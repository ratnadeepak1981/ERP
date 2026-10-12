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
public class SubscriptionPlanController : ControllerBase
{
    private readonly ISubscriptionPlanService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public SubscriptionPlanController(
        ISubscriptionPlanService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    [HttpPost]
    [RequirePermission("SUBSCRIPTION.CREATE")]
    public IActionResult Create(
        [FromBody] CreateSubscriptionPlanRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }
        try
        {
            SubscriptionPlan result =
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

        IReadOnlyList<SubscriptionPlan> plans =
            _service.GetAll();

        return Ok(plans);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetById(Guid id)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        SubscriptionPlan? plan =
            _service.GetById(id);

        if (plan == null)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Subscription plan not found."
            });
        }

        return Ok(plan);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(
        Guid id,
        [FromBody] UpdateSubscriptionPlanRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionPlan result =
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
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Deactivate(Guid id)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            _service.Deactivate(id);

            return Ok(new
            {
                status = "PASS",
                message = "Subscription plan deactivated."
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }
}