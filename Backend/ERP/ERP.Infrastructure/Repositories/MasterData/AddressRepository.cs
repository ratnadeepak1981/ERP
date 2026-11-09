using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AddressEntity = Domain.Features.MasterData.Address.Address;
using ContactEntity = Domain.Features.MasterData.Contact.Contact;
using AddressTypeEntity = Domain.Features.MasterData.AddressType.AddressType;
using ContactTypeEntity = Domain.Features.MasterData.Contact.ContactType;
using CountryEntity = Domain.Features.MasterData.Country.Country;

namespace ERP.Infrastructure.Repositories.MasterData;

public class AddressRepository : IAddressRepository
{
    private readonly DomainDbContext _context;

    public AddressRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<AddressEntity>> GetAddressesAsync(Guid tenantId)
    {
        return await _context.Addresses
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<AddressEntity?> GetAddressByIdAsync(Guid tenantId, Guid addressId)
    {
        return await _context.Addresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.AddressId == addressId && x.IsActive);
    }

    public async Task AddAddressAsync(AddressEntity address)
    {
        if (address == null) throw new ArgumentNullException(nameof(address));
        await _context.Addresses.AddAsync(address);
    }

    public async Task<List<ContactEntity>> GetContactsAsync(Guid tenantId)
    {
        return await _context.Contacts
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<ContactEntity?> GetContactByIdAsync(Guid tenantId, Guid contactId)
    {
        return await _context.Contacts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contactId && x.IsActive);
    }

    public async Task AddContactAsync(ContactEntity contact)
    {
        if (contact == null) throw new ArgumentNullException(nameof(contact));
        await _context.Contacts.AddAsync(contact);
    }

    public async Task<List<AddressTypeEntity>> GetAddressTypesAsync(Guid tenantId)
    {
        return await _context.AddressTypes
            .Where(x => (x.TenantId == null || x.TenantId == tenantId) && x.IsActive)
            .ToListAsync();
    }

    public async Task<AddressTypeEntity?> GetAddressTypeByIdAsync(Guid? tenantId, Guid addressTypeId)
    {
        return await _context.AddressTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.AddressTypeId == addressTypeId && x.IsActive);
    }

    public async Task<AddressTypeEntity?> GetAddressTypeByCodeAsync(Guid? tenantId, string code)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return await _context.AddressTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.Code == normalized && x.IsActive);
    }

    public async Task AddAddressTypeAsync(AddressTypeEntity addressType)
    {
        if (addressType == null) throw new ArgumentNullException(nameof(addressType));
        await _context.AddressTypes.AddAsync(addressType);
    }

    public async Task<List<ContactTypeEntity>> GetContactTypesAsync(Guid tenantId)
    {
        return await _context.ContactTypes
            .Where(x => (x.TenantId == null || x.TenantId == tenantId) && x.IsActive)
            .ToListAsync();
    }

    public async Task<ContactTypeEntity?> GetContactTypeByIdAsync(Guid? tenantId, Guid contactTypeId)
    {
        return await _context.ContactTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.ContactTypeId == contactTypeId && x.IsActive);
    }

    public async Task<ContactTypeEntity?> GetContactTypeByCodeAsync(Guid? tenantId, string code)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return await _context.ContactTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.Code == normalized && x.IsActive);
    }

    public async Task AddContactTypeAsync(ContactTypeEntity contactType)
    {
        if (contactType == null) throw new ArgumentNullException(nameof(contactType));
        await _context.ContactTypes.AddAsync(contactType);
    }

    public async Task<List<CountryEntity>> GetCountriesAsync()
    {
        return await _context.Countries
            .Where(x => x.IsActive)
            .OrderBy(x => x.CountryName)
            .ToListAsync();
    }

    public async Task<CountryEntity?> GetCountryByIdAsync(Guid countryId)
    {
        return await _context.Countries
            .FirstOrDefaultAsync(x => x.CountryId == countryId && x.IsActive);
    }

    public async Task<CountryEntity?> GetCountryByCodeOrNameAsync(string query)
    {
        string normalized = query.Trim().ToUpperInvariant();
        return await _context.Countries
            .FirstOrDefaultAsync(x => x.IsActive &&
                (x.Alpha2Code == normalized || x.Alpha3Code == normalized || x.CountryName.ToUpper() == normalized));
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
