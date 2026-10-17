using System;
using System.Collections.Generic;
using API.Common;
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
public class SubscriptionPlanController : BaseApiController
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
    public IActionResult Create([FromBody] CreateSubscriptionPlanRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionPlan result = _service.Create(request);
            return CreatedResponse(nameof(GetById), new { id = result.Id }, result, "Subscription plan created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetAll()
    {
        IReadOnlyList<SubscriptionPlan> plans = _service.GetAll();
        return SuccessResponse(plans);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public IActionResult GetById(Guid id)
    {
        SubscriptionPlan? plan = _service.GetById(id);
        if (plan == null)
        {
            return NotFoundResponse("Subscription plan not found.");
        }

        return SuccessResponse(plan);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(Guid id, [FromBody] UpdateSubscriptionPlanRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionPlan result = _service.Update(id, request);
            return SuccessResponse(result, "Subscription plan updated successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
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
            return SuccessResponse("Subscription plan deactivated.");
        }
        catch (InvalidOperationException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}