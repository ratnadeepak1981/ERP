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
public class SubscriptionPlanParameterController
    : ControllerBase
{
    private readonly ISubscriptionPlanParameterService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public SubscriptionPlanParameterController(
        ISubscriptionPlanParameterService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    [HttpPost]
    [RequirePermission("SUBSCRIPTION.CREATE")]
    public ActionResult<SubscriptionPlanParameter> Create(
        CreateSubscriptionPlanParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }
        try
        {
            SubscriptionPlanParameter result =
                _service.Create(request);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
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
    public ActionResult<SubscriptionPlanParameter> GetById(
        Guid id)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        SubscriptionPlanParameter? result =
            _service.GetById(id);

        if (result == null)
        {
            return NotFound(new
            {
                message = "Subscription plan parameter was not found."
            });
        }

        return Ok(result);
    }

    [HttpGet]
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public ActionResult<IReadOnlyList<SubscriptionPlanParameter>> GetAll()
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        return Ok(_service.GetAll());
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public ActionResult<SubscriptionPlanParameter> Update(
        Guid id,
        UpdateSubscriptionPlanParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionPlanParameter result =
                _service.Update(id, request);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }
}