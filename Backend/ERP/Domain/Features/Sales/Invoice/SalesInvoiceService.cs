using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Sales.Invoice;

public class SalesInvoiceService : ISalesInvoiceService
{
    private readonly ISalesInvoiceRepository _invoiceRepository;
    private readonly ISalesShipmentRepository _shipmentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly DomainDbContext _context;

    public SalesInvoiceService(
        ISalesInvoiceRepository invoiceRepository,
        ISalesShipmentRepository shipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        DomainDbContext context)
    {
        _invoiceRepository = invoiceRepository;
        _shipmentRepository = shipmentRepository;
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _context = context;
    }

    public async Task<List<SalesInvoiceDto>> GetInvoicesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        SalesInvoiceStatus? status = null)
    {
        List<SalesInvoice> invoices;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            invoices = await _invoiceRepository.GetByBranchAsync(tenantId, companyId, branchId.Value, status);
        }
        else
        {
            invoices = await _invoiceRepository.GetByCompanyAsync(tenantId, companyId, status);
        }

        var dtos = new List<SalesInvoiceDto>();
        foreach (var inv in invoices)
        {
            dtos.Add(await EnrichToDtoAsync(tenantId, inv));
        }

        return dtos;
    }

    public async Task<SalesInvoiceDto?> GetInvoiceByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        Guid invoiceId)
    {
        SalesInvoice? inv;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            inv = await _invoiceRepository.GetByIdAsync(tenantId, companyId, branchId.Value, invoiceId);
        }
        else
        {
            inv = await _invoiceRepository.GetByIdAsync(tenantId, invoiceId);
            if (inv != null && inv.CompanyId != companyId) inv = null;
        }

        if (inv == null) return null;
        return await EnrichToDtoAsync(tenantId, inv);
    }

    public async Task<SalesInvoiceDto?> GetInvoiceByShipmentIdAsync(
        Guid tenantId,
        Guid shipmentId)
    {
        var inv = await _invoiceRepository.GetByShipmentIdAsync(tenantId, shipmentId);
        if (inv == null) return null;
        return await EnrichToDtoAsync(tenantId, inv);
    }

    public async Task<SalesInvoiceDto> CreateDraftFromShipmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        string? invoiceNumber = null)
    {
        // 1. Check idempotency: if invoice already exists for this shipment, return it
        var existing = await _invoiceRepository.GetByShipmentIdAsync(tenantId, shipmentId);
        if (existing != null)
        {
            return await EnrichToDtoAsync(tenantId, existing);
        }

        // 2. Fetch shipment and parent sales order
        var shipment = await _shipmentRepository.GetByIdAsync(tenantId, companyId, branchId, shipmentId)
            ?? throw new KeyNotFoundException("Sales shipment not found in authorized scope.");

        if (shipment.Status != SalesShipmentStatus.Delivered && shipment.Status != SalesShipmentStatus.Dispatched)
        {
            throw new InvalidOperationException($"Cannot generate invoice for shipment in status '{shipment.Status}'. Shipment must be delivered or dispatched.");
        }

        var order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, shipment.SalesOrderId)
            ?? throw new KeyNotFoundException("Associated Sales Order not found.");

        // 3. Generate unique invoice number if not provided
        string finalInvoiceNum = invoiceNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(finalInvoiceNum))
        {
            finalInvoiceNum = await GenerateUniqueInvoiceNumberAsync(tenantId, companyId);
        }
        else if (await _invoiceRepository.ExistsInvoiceNumberAsync(tenantId, companyId, finalInvoiceNum))
        {
            throw new InvalidOperationException($"Invoice number '{finalInvoiceNum}' already exists for this company.");
        }

        // 4. Create Draft invoice
        var invoice = SalesInvoice.Create(
            tenantId,
            companyId,
            branchId,
            order.Id,
            order.CustomerId,
            finalInvoiceNum,
            salesShipmentId: shipment.Id,
            invoiceDate: DateTime.UtcNow,
            dueDate: DateTime.UtcNow.AddDays(30),
            currency: order.Currency,
            paymentTerms: order.PaymentTerms,
            notes: $"Generated from Dispatch Note {shipment.ShipmentNumber}");

        // 5. Populate lines from shipment items and order pricing
        var activeShipmentItems = shipment.Items.Where(i => i.IsActive).ToList();
        foreach (var sItem in activeShipmentItems)
        {
            var orderItem = order.Items.FirstOrDefault(oi => oi.Id == sItem.SalesOrderItemId && oi.IsActive);
            decimal unitPrice = orderItem?.UnitPrice ?? 0;
            decimal discountRate = orderItem?.DiscountRate ?? 0;
            decimal taxRate = orderItem?.TaxRate ?? 0;

            var invItem = SalesInvoiceItem.Create(
                invoice.Id,
                sItem.ProductId,
                sItem.QuantityDispatched,
                unitPrice,
                discountRate,
                taxRate,
                salesOrderItemId: sItem.SalesOrderItemId,
                salesShipmentItemId: sItem.Id,
                unitOfMeasureId: sItem.UnitOfMeasureId ?? orderItem?.UnitOfMeasureId,
                notes: sItem.Notes);

            invoice.AddItem(invItem);
        }

        await _invoiceRepository.AddAsync(invoice);
        await _invoiceRepository.SaveChangesAsync();

        return await EnrichToDtoAsync(tenantId, invoice);
    }

    public async Task<SalesInvoiceDto> UpdateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid id,
        UpdateSalesInvoiceRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var invoice = await _invoiceRepository.GetByIdAsync(tenantId, companyId, branchId, id)
            ?? throw new KeyNotFoundException("Sales Invoice not found in authorized scope.");

        if (invoice.Status != SalesInvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update invoice in status '{invoice.Status}'. Only Draft invoices can be updated.");
        }

        if (request.InvoiceDate.HasValue) invoice.InvoiceDate = request.InvoiceDate.Value;
        if (request.DueDate.HasValue) invoice.DueDate = request.DueDate.Value;
        if (request.PaymentTerms != null) invoice.PaymentTerms = request.PaymentTerms.Trim();
        if (request.Notes != null) invoice.Notes = request.Notes.Trim();

        if (request.Items != null && request.Items.Count > 0)
        {
            var existingItems = invoice.Items.ToList();
            invoice.Items.Clear();
            _context.Set<SalesInvoiceItem>().RemoveRange(existingItems);

            foreach (var reqItem in request.Items)
            {
                var invItem = SalesInvoiceItem.Create(
                    invoice.Id,
                    reqItem.ProductId,
                    reqItem.Quantity,
                    reqItem.UnitPrice,
                    reqItem.DiscountRate,
                    reqItem.TaxRate,
                    reqItem.SalesOrderItemId,
                    reqItem.SalesShipmentItemId,
                    reqItem.UnitOfMeasureId,
                    reqItem.Notes);

                invoice.AddItem(invItem);
            }
        }
        else
        {
            invoice.RecalculateTotals();
        }

        await _invoiceRepository.SaveChangesAsync();
        return await EnrichToDtoAsync(tenantId, invoice);
    }

    public async Task<SalesInvoiceDto> ConfirmInvoiceAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid confirmedBy,
        string? notes = null)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(tenantId, companyId, branchId, id)
            ?? throw new KeyNotFoundException("Sales Invoice not found in authorized scope.");

        if (invoice.Status == SalesInvoiceStatus.Confirmed)
        {
            return await EnrichToDtoAsync(tenantId, invoice);
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? notes.Trim()
                : $"{invoice.Notes}; {notes.Trim()}";
        }

        invoice.MarkConfirmed(confirmedBy);
        await _invoiceRepository.SaveChangesAsync();

        return await EnrichToDtoAsync(tenantId, invoice);
    }

    public async Task<SalesInvoiceDto> CancelInvoiceAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(tenantId, companyId, branchId, id)
            ?? throw new KeyNotFoundException("Sales Invoice not found in authorized scope.");

        invoice.MarkCancelled();
        await _invoiceRepository.SaveChangesAsync();

        return await EnrichToDtoAsync(tenantId, invoice);
    }

    private async Task<string> GenerateUniqueInvoiceNumberAsync(Guid tenantId, Guid companyId)
    {
        int year = DateTime.UtcNow.Year;
        var random = new Random();
        for (int i = 0; i < 20; i++)
        {
            string candidate = $"INV-{year}-{random.Next(1000, 9999)}";
            if (!await _invoiceRepository.ExistsInvoiceNumberAsync(tenantId, companyId, candidate))
            {
                return candidate;
            }
        }

        return $"INV-{year}-{DateTime.UtcNow.Ticks % 100000:D5}";
    }

    private async Task<SalesInvoiceDto> EnrichToDtoAsync(Guid tenantId, SalesInvoice invoice)
    {
        string customerName = string.Empty;
        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, invoice.CustomerId);
        if (customer != null)
        {
            customerName = customer.CustomerName;
        }

        string orderNumber = string.Empty;
        var order = await _salesOrderRepository.GetByIdAsync(tenantId, invoice.CompanyId, invoice.BranchId, invoice.SalesOrderId);
        if (order != null)
        {
            orderNumber = order.OrderNumber;
        }

        string shipmentNumber = string.Empty;
        if (invoice.SalesShipmentId.HasValue)
        {
            var shipment = await _shipmentRepository.GetByIdAsync(tenantId, invoice.CompanyId, invoice.BranchId, invoice.SalesShipmentId.Value);
            if (shipment != null)
            {
                shipmentNumber = shipment.ShipmentNumber;
            }
        }

        var itemDtos = new List<SalesInvoiceItemDto>();
        foreach (var it in invoice.Items.Where(i => i.IsActive))
        {
            string prodCode = string.Empty;
            string prodName = string.Empty;
            var prod = await _productRepository.GetByIdAsync(tenantId, it.ProductId);
            if (prod != null)
            {
                prodCode = prod.ProductCode;
                prodName = prod.ProductName;
            }

            itemDtos.Add(new SalesInvoiceItemDto
            {
                Id = it.Id,
                SalesInvoiceId = it.SalesInvoiceId,
                SalesOrderItemId = it.SalesOrderItemId,
                SalesShipmentItemId = it.SalesShipmentItemId,
                ProductId = it.ProductId,
                ProductCode = prodCode,
                ProductName = prodName,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                DiscountRate = it.DiscountRate,
                DiscountAmount = it.DiscountAmount,
                TaxRate = it.TaxRate,
                TaxAmount = it.TaxAmount,
                Subtotal = it.Subtotal,
                LineTotal = it.LineTotal,
                UnitOfMeasureId = it.UnitOfMeasureId,
                Notes = it.Notes
            });
        }

        return new SalesInvoiceDto
        {
            Id = invoice.Id,
            TenantId = invoice.TenantId,
            CompanyId = invoice.CompanyId,
            BranchId = invoice.BranchId,
            SalesOrderId = invoice.SalesOrderId,
            OrderNumber = orderNumber,
            SalesShipmentId = invoice.SalesShipmentId,
            ShipmentNumber = shipmentNumber,
            CustomerId = invoice.CustomerId,
            CustomerName = customerName,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status,
            Currency = invoice.Currency,
            PaymentTerms = invoice.PaymentTerms,
            Subtotal = invoice.Subtotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Notes = invoice.Notes,
            ConfirmedAt = invoice.ConfirmedAt,
            ConfirmedBy = invoice.ConfirmedBy,
            Items = itemDtos
        };
    }
}
