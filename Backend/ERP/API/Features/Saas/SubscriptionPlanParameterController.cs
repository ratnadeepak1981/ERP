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
public class SubscriptionPlanParameterController : BaseApiController
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
    public IActionResult Create([FromBody] CreateSubscriptionPlanParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionPlanParameter result = _service.Create(request);
            return CreatedResponse(nameof(GetById), new { id = result.Id }, result, "Subscription plan parameter created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
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

        SubscriptionPlanParameter? result = _service.GetById(id);
        if (result == null)
        {
            return NotFoundResponse("Subscription plan parameter was not found.");
        }

        return SuccessResponse(result);
    }

    [HttpGet]
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetAll()
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        return SuccessResponse(_service.GetAll());
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(Guid id, [FromBody] UpdateSubscriptionPlanParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionPlanParameter result = _service.Update(id, request);
            return SuccessResponse(result, "Subscription plan parameter updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}