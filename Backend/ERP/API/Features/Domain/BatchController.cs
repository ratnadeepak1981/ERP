using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using ERP.Domain.Features.Inventory.Batch;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/inventory/batches")]
[RequirePermission("BATCH.VIEW,PRODUCT.VIEW")]
public class BatchController : BaseApiController
{
    private readonly IBatchService _batchService;
    private readonly ICurrentUserContext _currentUserContext;

    public BatchController(IBatchService batchService, ICurrentUserContext currentUserContext)
    {
        _batchService = batchService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetBatches([FromQuery] Guid? productId = null)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var batches = await _batchService.GetBatchesAsync(tenantId.Value, productId);
        return SuccessResponse(batches);
    }

    [HttpGet("eligible")]
    public async Task<IActionResult> GetEligibleBatches(
        [FromQuery] Guid productId,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateTime? asOfDate = null)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        if (productId == Guid.Empty)
        {
            return BadRequestResponse("ProductId is required.");
        }

        var eligible = await _batchService.GetEligibleBatchesAsync(
            tenantId.Value,
            productId,
            warehouseId,
            locationId,
            asOfDate);

        return SuccessResponse(eligible);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBatch(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var batch = await _batchService.GetBatchByIdAsync(tenantId.Value, id);
        if (batch == null)
        {
            return NotFoundResponse("Batch record not found.");
        }

        return SuccessResponse(batch);
    }

    [HttpPost]
    [RequirePermission("BATCH.CREATE,BATCH.EDIT,PRODUCT.EDIT")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateProductBatchRequest request)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        try
        {
            var created = await _batchService.CreateBatchAsync(tenantId.Value, request);
            return CreatedResponse($"/api/inventory/batches/{created.ProductBatchId}", created, "Batch registered successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
