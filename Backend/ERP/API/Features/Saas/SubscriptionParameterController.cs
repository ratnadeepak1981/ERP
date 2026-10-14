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
public class SubscriptionParameterController : BaseApiController
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
    public IActionResult Create([FromBody] CreateSubscriptionParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionParameter result = _service.Create(request);
            return CreatedResponse(nameof(GetById), new { id = result.Id }, result, "Subscription parameter created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
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

        IReadOnlyList<SubscriptionParameter> result = _service.GetAll();
        return SuccessResponse(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.VIEW")]
    public IActionResult GetById(Guid id)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        SubscriptionParameter? result = _service.GetById(id);
        if (result == null)
        {
            return NotFoundResponse("Subscription parameter not found.");
        }

        return SuccessResponse(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(Guid id, [FromBody] UpdateSubscriptionParameterRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            SubscriptionParameter result = _service.Update(id, request);
            return SuccessResponse(result, "Subscription parameter updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
