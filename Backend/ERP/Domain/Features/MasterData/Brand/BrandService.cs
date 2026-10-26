using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Brand;

public class BrandService : IBrandService
{
    private readonly IBrandRepository _repository;

    public BrandService(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Brand>> GetBrandsAsync(Guid tenantId)
    {
        return await _repository.GetByTenantAsync(tenantId);
    }

    public async Task<Brand?> GetBrandAsync(Guid tenantId, Guid brandId)
    {
        return await _repository.GetByIdAsync(tenantId, brandId);
    }

    public async Task<Brand> CreateBrandAsync(Guid tenantId, CreateBrandRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.BrandCode))
            throw new ArgumentException("Brand code is required.", nameof(request.BrandCode));

        if (string.IsNullOrWhiteSpace(request.BrandName))
            throw new ArgumentException("Brand name is required.", nameof(request.BrandName));

        string code = request.BrandCode.Trim().ToUpperInvariant();
        string name = request.BrandName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Brand code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Brand name '{name}' already exists for this tenant.");

        var brand = Brand.Create(tenantId, code, name, request.Description?.Trim());
        await _repository.AddAsync(brand);
        await _repository.SaveChangesAsync();
        return brand;
    }

    public async Task<Brand> UpdateBrandAsync(Guid tenantId, Guid brandId, UpdateBrandRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.BrandName))
            throw new ArgumentException("Brand name is required.", nameof(request.BrandName));

        var brand = await _repository.GetByIdAsync(tenantId, brandId)
            ?? throw new KeyNotFoundException("Brand not found.");

        string name = request.BrandName.Trim();
        if (brand.BrandName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Brand name '{name}' already exists for this tenant.");

        brand.Update(name, request.Description?.Trim());
        await _repository.SaveChangesAsync();
        return brand;
    }

    public async Task DeactivateBrandAsync(Guid tenantId, Guid brandId)
    {
        var brand = await _repository.GetByIdAsync(tenantId, brandId)
            ?? throw new KeyNotFoundException("Brand not found.");

        brand.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<Brand> ReactivateBrandAsync(Guid tenantId, Guid brandId)
    {
        var brand = await _repository.GetByIdAsync(tenantId, brandId)
            ?? throw new KeyNotFoundException("Brand not found.");

        if (brand.IsActive)
            return brand;

        brand.Activate();
        await _repository.SaveChangesAsync();
        return brand;
    }
}
