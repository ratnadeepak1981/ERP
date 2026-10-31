using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.UnitOfMeasure;

namespace Domain.Features.MasterData.PriceCard;

public class PriceCardService : IPriceCardService
{
    private readonly IPriceCardRepository _repository;
    private readonly IUnitOfMeasureService? _uomService;

    public PriceCardService(
        IPriceCardRepository repository,
        IUnitOfMeasureService? uomService = null)
    {
        _repository = repository;
        _uomService = uomService;
    }

    public async Task<List<PriceCardDto>> GetPriceCardsAsync(Guid tenantId, PriceCardType? type = null, string? search = null, bool? isActive = null)
    {
        var cards = await _repository.GetPriceCardsAsync(tenantId, type, search, isActive);
        var dtos = new List<PriceCardDto>();

        foreach (var c in cards)
        {
            var lines = await _repository.GetLinesByCardIdAsync(tenantId, c.PriceCardId);
            dtos.Add(MapToDto(c, lines));
        }

        return dtos;
    }

    public async Task<PriceCardDto?> GetPriceCardAsync(Guid tenantId, Guid priceCardId)
    {
        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: true);
        if (card == null) return null;

        var lines = await _repository.GetLinesByCardIdAsync(tenantId, card.PriceCardId);
        return MapToDto(card, lines);
    }

    public async Task<PriceCardDto> CreatePriceCardAsync(Guid tenantId, CreatePriceCardRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.PriceCardCode))
            throw new ArgumentException("Price card code is required.", nameof(request.PriceCardCode));

        if (string.IsNullOrWhiteSpace(request.PriceCardName))
            throw new ArgumentException("Price card name is required.", nameof(request.PriceCardName));

        if (!Enum.IsDefined(typeof(PriceCardType), request.PriceCardType))
            throw new ArgumentException("Invalid price card type. Must be Purchase (1) or Sales (2).", nameof(request.PriceCardType));

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required.", nameof(request.Currency));

        if (!await _repository.CurrencyExistsAsync(tenantId, request.Currency))
            throw new ArgumentException($"Currency '{request.Currency}' is invalid or does not exist for this tenant.", nameof(request.Currency));

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
            throw new ArgumentException("EffectiveTo date cannot be earlier than EffectiveFrom date.");

        if (await _repository.ExistsByCodeAsync(tenantId, request.PriceCardCode))
            throw new InvalidOperationException($"Price card with code '{request.PriceCardCode.Trim().ToUpperInvariant()}' already exists.");

        var card = PriceCard.Create(
            tenantId,
            request.PriceCardCode,
            request.PriceCardName,
            request.PriceCardType,
            request.Currency,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Description);

        await _repository.AddPriceCardAsync(card);

        if (request.Lines != null && request.Lines.Count > 0)
        {
            var seen = new HashSet<(Guid, Guid?)>();
            foreach (var lineReq in request.Lines)
            {
                if (lineReq.UnitPrice < 0)
                    throw new ArgumentOutOfRangeException(nameof(lineReq.UnitPrice), "Unit price cannot be negative.");

                if (lineReq.MinimumQuantity < 0)
                    throw new ArgumentOutOfRangeException(nameof(lineReq.MinimumQuantity), "Minimum quantity cannot be negative.");

                if (lineReq.EffectiveFrom.HasValue && lineReq.EffectiveTo.HasValue && lineReq.EffectiveTo.Value < lineReq.EffectiveFrom.Value)
                    throw new ArgumentException("Line EffectiveTo date cannot be earlier than EffectiveFrom date.");

                if (!seen.Add((lineReq.ProductId, lineReq.UnitOfMeasureId)))
                    throw new InvalidOperationException($"Duplicate line for product '{lineReq.ProductId}' and unit of measure '{lineReq.UnitOfMeasureId}' in request.");

                if (!await _repository.ProductExistsAsync(tenantId, lineReq.ProductId))
                    throw new InvalidOperationException($"Product '{lineReq.ProductId}' was not found or does not belong to this tenant.");

                if (lineReq.UnitOfMeasureId.HasValue && !await _repository.UnitOfMeasureExistsAsync(tenantId, lineReq.UnitOfMeasureId.Value))
                    throw new InvalidOperationException($"Unit of measure '{lineReq.UnitOfMeasureId.Value}' was not found or does not belong to this tenant.");

                card.AddLine(
                    lineReq.ProductId,
                    lineReq.UnitPrice,
                    lineReq.UnitOfMeasureId,
                    lineReq.MinimumQuantity,
                    lineReq.EffectiveFrom,
                    lineReq.EffectiveTo);
            }
        }

        await _repository.SaveChangesAsync();
        var lines = await _repository.GetLinesByCardIdAsync(tenantId, card.PriceCardId);
        return MapToDto(card, lines);
    }

    public async Task<PriceCardDto> UpdatePriceCardAsync(Guid tenantId, Guid priceCardId, UpdatePriceCardRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false)
            ?? throw new KeyNotFoundException("Price card not found.");

        if (!card.IsActive)
            throw new InvalidOperationException("Cannot update an inactive price card. Reactivate it first.");

        if (string.IsNullOrWhiteSpace(request.PriceCardName))
            throw new ArgumentException("Price card name is required.", nameof(request.PriceCardName));

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required.", nameof(request.Currency));

        if (!await _repository.CurrencyExistsAsync(tenantId, request.Currency))
            throw new ArgumentException($"Currency '{request.Currency}' is invalid or does not exist for this tenant.", nameof(request.Currency));

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
            throw new ArgumentException("EffectiveTo date cannot be earlier than EffectiveFrom date.");

        card.Update(
            request.PriceCardName,
            request.Currency,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Description);

        if (request.Lines != null)
        {
            var seen = new HashSet<(Guid, Guid?)>();
            foreach (var lineReq in request.Lines)
            {
                if (lineReq.UnitPrice < 0)
                    throw new ArgumentOutOfRangeException(nameof(lineReq.UnitPrice), "Unit price cannot be negative.");

                if (lineReq.MinimumQuantity < 0)
                    throw new ArgumentOutOfRangeException(nameof(lineReq.MinimumQuantity), "Minimum quantity cannot be negative.");

                if (lineReq.EffectiveFrom.HasValue && lineReq.EffectiveTo.HasValue && lineReq.EffectiveTo.Value < lineReq.EffectiveFrom.Value)
                    throw new ArgumentException("Line EffectiveTo date cannot be earlier than EffectiveFrom date.");

                if (!seen.Add((lineReq.ProductId, lineReq.UnitOfMeasureId)))
                    throw new InvalidOperationException($"Duplicate line for product '{lineReq.ProductId}' and unit of measure '{lineReq.UnitOfMeasureId}' in request.");

                if (!await _repository.ProductExistsAsync(tenantId, lineReq.ProductId))
                    throw new InvalidOperationException($"Product '{lineReq.ProductId}' was not found or does not belong to this tenant.");

                if (lineReq.UnitOfMeasureId.HasValue && !await _repository.UnitOfMeasureExistsAsync(tenantId, lineReq.UnitOfMeasureId.Value))
                    throw new InvalidOperationException($"Unit of measure '{lineReq.UnitOfMeasureId.Value}' was not found or does not belong to this tenant.");
            }

            await _repository.RemoveLinesAsync(tenantId, card.PriceCardId);
            foreach (var lineReq in request.Lines)
            {
                var line = PriceCardLine.Create(
                    tenantId,
                    card.PriceCardId,
                    lineReq.ProductId,
                    lineReq.UnitPrice,
                    lineReq.UnitOfMeasureId,
                    lineReq.MinimumQuantity,
                    lineReq.EffectiveFrom ?? card.EffectiveFrom,
                    lineReq.EffectiveTo ?? card.EffectiveTo);
                await _repository.AddLineAsync(line);
            }
        }

        await _repository.SaveChangesAsync();
        var lines = await _repository.GetLinesByCardIdAsync(tenantId, card.PriceCardId);
        return MapToDto(card, lines);
    }

    public async Task DeactivatePriceCardAsync(Guid tenantId, Guid priceCardId)
    {
        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false)
            ?? throw new KeyNotFoundException("Price card not found.");

        if (!card.IsActive) return;

        card.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task ReactivatePriceCardAsync(Guid tenantId, Guid priceCardId)
    {
        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false)
            ?? throw new KeyNotFoundException("Price card not found.");

        if (card.IsActive) return;

        card.Activate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<PriceCardLineDto>> GetLinesAsync(Guid tenantId, Guid priceCardId)
    {
        var lines = await _repository.GetLinesByCardIdAsync(tenantId, priceCardId);
        return lines.Select(MapLineToDto).ToList();
    }

    public async Task<PriceCardLineDto> AddLineAsync(Guid tenantId, Guid priceCardId, CreatePriceCardLineRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false)
            ?? throw new KeyNotFoundException("Price card not found.");

        if (!card.IsActive)
            throw new InvalidOperationException("Cannot add lines to an inactive price card. Reactivate it first.");

        if (!await _repository.ProductExistsAsync(tenantId, request.ProductId))
            throw new InvalidOperationException($"Product '{request.ProductId}' was not found or does not belong to this tenant.");

        if (request.UnitOfMeasureId.HasValue && !await _repository.UnitOfMeasureExistsAsync(tenantId, request.UnitOfMeasureId.Value))
            throw new InvalidOperationException($"Unit of measure '{request.UnitOfMeasureId.Value}' was not found or does not belong to this tenant.");

        if (await _repository.LineExistsAsync(tenantId, priceCardId, request.ProductId, request.UnitOfMeasureId))
            throw new InvalidOperationException("A price line for this product and UOM already exists on this price card.");

        var line = PriceCardLine.Create(
            tenantId,
            priceCardId,
            request.ProductId,
            request.UnitPrice,
            request.UnitOfMeasureId,
            request.MinimumQuantity,
            request.EffectiveFrom ?? card.EffectiveFrom,
            request.EffectiveTo ?? card.EffectiveTo);

        await _repository.AddLineAsync(line);
        await _repository.SaveChangesAsync();

        var createdLine = await _repository.GetLineByIdAsync(tenantId, priceCardId, line.PriceCardLineId);
        return MapLineToDto(createdLine ?? line);
    }

    public async Task<PriceCardLineDto> UpdateLineAsync(Guid tenantId, Guid priceCardId, Guid lineId, UpdatePriceCardLineRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false)
            ?? throw new KeyNotFoundException("Price card not found.");

        if (!card.IsActive)
            throw new InvalidOperationException("Cannot update lines on an inactive price card. Reactivate it first.");

        var line = await _repository.GetLineByIdAsync(tenantId, priceCardId, lineId)
            ?? throw new KeyNotFoundException("Price card line not found.");

        if (request.UnitOfMeasureId.HasValue && !await _repository.UnitOfMeasureExistsAsync(tenantId, request.UnitOfMeasureId.Value))
            throw new InvalidOperationException($"Unit of measure '{request.UnitOfMeasureId.Value}' was not found or does not belong to this tenant.");

        if (await _repository.LineExistsAsync(tenantId, priceCardId, line.ProductId, request.UnitOfMeasureId, lineId))
            throw new InvalidOperationException("Another price line for this product and UOM already exists on this price card.");

        line.Update(
            request.UnitPrice,
            request.UnitOfMeasureId,
            request.MinimumQuantity,
            request.EffectiveFrom,
            request.EffectiveTo);

        await _repository.SaveChangesAsync();

        var updatedLine = await _repository.GetLineByIdAsync(tenantId, priceCardId, lineId);
        return MapLineToDto(updatedLine ?? line);
    }

    public async Task DeleteLineAsync(Guid tenantId, Guid priceCardId, Guid lineId)
    {
        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false)
            ?? throw new KeyNotFoundException("Price card not found.");

        if (!card.IsActive)
            throw new InvalidOperationException("Cannot delete lines on an inactive price card. Reactivate it first.");

        var line = await _repository.GetLineByIdAsync(tenantId, priceCardId, lineId)
            ?? throw new KeyNotFoundException("Price card line not found.");

        await _repository.RemoveLineAsync(line);
        await _repository.SaveChangesAsync();
    }

    public async Task<PriceResolutionResult> GetApplicablePriceAsync(
        Guid tenantId,
        Guid priceCardId,
        Guid productId,
        Guid? requestedUomId,
        DateTime? asOfDate = null)
    {
        DateTime checkDate = asOfDate ?? DateTime.UtcNow;

        var card = await _repository.GetPriceCardByIdAsync(tenantId, priceCardId, includeLines: false);
        if (card == null || !card.IsActive)
        {
            return new PriceResolutionResult
            {
                Found = false,
                ProductId = productId,
                Message = card == null ? "Price card not found." : "Price card is inactive."
            };
        }

        if (card.EffectiveFrom > checkDate || (card.EffectiveTo.HasValue && card.EffectiveTo.Value < checkDate))
        {
            return new PriceResolutionResult
            {
                Found = false,
                ProductId = productId,
                PriceCardId = card.PriceCardId,
                PriceCardCode = card.PriceCardCode,
                PriceCardName = card.PriceCardName,
                PriceCardType = card.PriceCardType,
                Message = $"Price card '{card.PriceCardCode}' is not effective for date {checkDate:yyyy-MM-dd}."
            };
        }

        var line = await _repository.GetActiveLineAsync(tenantId, priceCardId, productId, requestedUomId, checkDate);
        if (line == null)
        {
            return new PriceResolutionResult
            {
                Found = false,
                ProductId = productId,
                PriceCardId = card.PriceCardId,
                PriceCardCode = card.PriceCardCode,
                PriceCardName = card.PriceCardName,
                PriceCardType = card.PriceCardType,
                Message = $"No active price line found for product on price card '{card.PriceCardCode}'."
            };
        }

        decimal resolvedPrice = line.UnitPrice;
        Guid? resolvedUom = line.UnitOfMeasureId;

        // If requested UOM differs from line UOM and uomService is available, compute price conversion
        if (requestedUomId.HasValue && line.UnitOfMeasureId.HasValue && requestedUomId.Value != line.UnitOfMeasureId.Value && _uomService != null)
        {
            try
            {
                // We want price per 1 unit of requestedUomId.
                // Convert 1 of requestedUomId into line.UnitOfMeasureId.
                var context = card.PriceCardType == PriceCardType.Purchase ? ConversionContext.Purchase : ConversionContext.Sales;
                var conv = await _uomService.ConvertProductQuantityAsync(
                    tenantId,
                    productId,
                    requestedUomId.Value,
                    line.UnitOfMeasureId.Value,
                    1m,
                    context);

                if (conv != null && conv.ConvertedQuantity > 0)
                {
                    // 1 requested unit = conv.ConvertedQuantity line units
                    resolvedPrice = Math.Round(line.UnitPrice * conv.ConvertedQuantity, 4);
                    resolvedUom = requestedUomId.Value;
                }
            }
            catch
            {
                // If conversion fails, keep original line price and line UOM
            }
        }

        return new PriceResolutionResult
        {
            Found = true,
            PriceCardId = card.PriceCardId,
            PriceCardCode = card.PriceCardCode,
            PriceCardName = card.PriceCardName,
            PriceCardType = card.PriceCardType,
            ProductId = productId,
            ResolvedUomId = resolvedUom,
            UnitPrice = resolvedPrice,
            Currency = card.Currency,
            ExchangeRate = 1m,
            Source = "PriceCard"
        };
    }

    private static PriceCardDto MapToDto(PriceCard card, IEnumerable<PriceCardLine>? lines = null)
    {
        var lineList = lines?.Select(MapLineToDto).ToList() ?? new List<PriceCardLineDto>();
        return new PriceCardDto
        {
            PriceCardId = card.PriceCardId,
            TenantId = card.TenantId,
            PriceCardCode = card.PriceCardCode,
            PriceCardName = card.PriceCardName,
            PriceCardType = card.PriceCardType,
            Currency = card.Currency,
            EffectiveFrom = card.EffectiveFrom,
            EffectiveTo = card.EffectiveTo,
            Description = card.Description,
            IsActive = card.IsActive,
            LineCount = lineList.Count,
            Lines = lineList
        };
    }

    private static PriceCardLineDto MapLineToDto(PriceCardLine line)
    {
        return new PriceCardLineDto
        {
            PriceCardLineId = line.PriceCardLineId,
            TenantId = line.TenantId,
            PriceCardId = line.PriceCardId,
            ProductId = line.ProductId,
            ProductCode = line.Product?.ProductCode,
            ProductName = line.Product?.ProductName,
            UnitOfMeasureId = line.UnitOfMeasureId,
            UnitOfMeasureCode = line.UnitOfMeasure?.Code,
            UnitOfMeasureName = line.UnitOfMeasure?.Name,
            UnitPrice = line.UnitPrice,
            MinimumQuantity = line.MinimumQuantity,
            EffectiveFrom = line.EffectiveFrom,
            EffectiveTo = line.EffectiveTo,
            IsActive = line.IsActive
        };
    }
}
