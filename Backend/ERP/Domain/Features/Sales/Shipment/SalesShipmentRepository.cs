using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Sales.Shipment;

public class SalesShipmentRepository : ISalesShipmentRepository
{
    private readonly DomainDbContext _context;

    public SalesShipmentRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<SalesShipment>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesShipmentStatus? status = null)
    {
        var query = _context.SalesShipments
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.OrderByDescending(x => x.ShipmentDate).ThenByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task<List<SalesShipment>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        SalesShipmentStatus? status = null)
    {
        var query = _context.SalesShipments
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.IsActive);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.OrderByDescending(x => x.ShipmentDate).ThenByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task<SalesShipment?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId)
    {
        return await _context.SalesShipments
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == shipmentId &&
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive);
    }

    public async Task<SalesShipment?> GetByIdAsync(
        Guid tenantId,
        Guid shipmentId)
    {
        return await _context.SalesShipments
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == shipmentId &&
                x.TenantId == tenantId &&
                x.IsActive);
    }

    public async Task<List<SalesShipment>> GetBySalesOrderIdAsync(
        Guid tenantId,
        Guid salesOrderId)
    {
        return await _context.SalesShipments
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.SalesOrderId == salesOrderId &&
                x.IsActive)
            .ToListAsync();
    }

    public async Task<bool> ExistsShipmentNumberAsync(
        Guid tenantId,
        Guid companyId,
        string shipmentNumber)
    {
        return await _context.SalesShipments
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.ShipmentNumber == shipmentNumber);
    }

    public async Task AddAsync(SalesShipment shipment)
    {
        await _context.SalesShipments.AddAsync(shipment);
    }

    public async Task AddItemAsync(SalesShipmentItem item)
    {
        await _context.SalesShipmentItems.AddAsync(item);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
