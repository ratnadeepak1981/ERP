using System;
using System.Collections.Generic;
using API.Common;
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
public class SubscriptionLimitController : BaseApiController
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
    public IActionResult Create([FromBody] CreateSubscriptionLimitRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            var result = _service.Create(request);
            return CreatedResponse(nameof(GetById), new { id = result.Id }, result, "Subscription limit created successfully.");
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

        var result = _service.GetById(id);
        if (result == null)
        {
            return NotFoundResponse("Subscription limit was not found.");
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

        var result = _service.GetAll();
        return SuccessResponse(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUBSCRIPTION.EDIT")]
    public IActionResult Update(Guid id, [FromBody] UpdateSubscriptionLimitRequest request)
    {
        if (!_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        try
        {
            var result = _service.Update(id, request);
            return SuccessResponse(result, "Subscription limit updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}