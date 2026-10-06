using Microsoft.AspNetCore.Mvc;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;

namespace SaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionPlanController : ControllerBase
{
    private readonly ISubscriptionPlanService _service;

    public SubscriptionPlanController(
        ISubscriptionPlanService service)
    {
        _service = service;
    }

    [HttpPost]
    public IActionResult Create(
        [FromBody] CreateSubscriptionPlanRequest request)
    {
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
    public IActionResult GetAll()
    {
        IReadOnlyList<SubscriptionPlan> plans =
            _service.GetAll();

        return Ok(plans);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
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
    public IActionResult Update(
        Guid id,
        [FromBody] UpdateSubscriptionPlanRequest request)
    {
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
    public IActionResult Deactivate(Guid id)
    {
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