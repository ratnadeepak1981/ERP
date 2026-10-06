using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionPlanParameterController
    : ControllerBase
{
    private readonly ISubscriptionPlanParameterService _service;

    public SubscriptionPlanParameterController(
        ISubscriptionPlanParameterService service)
    {
        _service = service;
    }

    [HttpPost]
    public ActionResult<SubscriptionPlanParameter> Create(
        CreateSubscriptionPlanParameterRequest request)
    {
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
    public ActionResult<SubscriptionPlanParameter> GetById(
        Guid id)
    {
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
    public ActionResult<IReadOnlyList<SubscriptionPlanParameter>> GetAll()
    {
        return Ok(_service.GetAll());
    }

    [HttpPut("{id:guid}")]
    public ActionResult<SubscriptionPlanParameter> Update(
        Guid id,
        UpdateSubscriptionPlanParameterRequest request)
    {
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