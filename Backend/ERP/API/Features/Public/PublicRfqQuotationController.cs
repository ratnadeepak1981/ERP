using System;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using Domain.Features.Procurement.RequestForQuotation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Public;

[ApiController]
[Route("api/public/rfqs")]
[AllowAnonymous]
public class PublicRfqQuotationController : BaseApiController
{
    private readonly ISupplierQuotationService _quotationService;

    public PublicRfqQuotationController(ISupplierQuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    [HttpGet("invitation")]
    public async Task<IActionResult> GetInvitationDetails([FromQuery] string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequestResponse("Invitation token is required.");
        }

        try
        {
            var invitation = await _quotationService.GetPublicRfqDetailsByTokenAsync(token.Trim(), ct);
            if (invitation == null)
            {
                return NotFoundResponse("The quotation invitation link is invalid, expired, or has already been fulfilled.");
            }

            return SuccessResponse(invitation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitQuotation(
        [FromQuery] string token,
        [FromBody] SubmitPublicQuotationRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequestResponse("Invitation token is required.");
        }

        if (request == null)
        {
            return BadRequestResponse("Quotation submission data is required.");
        }

        try
        {
            var created = await _quotationService.SubmitPublicQuotationAsync(token.Trim(), request, ct);
            return SuccessResponse(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }
}
