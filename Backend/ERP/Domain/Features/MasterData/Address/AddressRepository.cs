using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Address;

public class AddressRepository : IAddressRepository
{
    private readonly DomainDbContext _context;

    public AddressRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Address>> GetAddressesAsync(Guid tenantId)
    {
        return await _context.Addresses
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Address?> GetAddressByIdAsync(Guid tenantId, Guid addressId)
    {
        return await _context.Addresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.AddressId == addressId && x.IsActive);
    }

    public async Task AddAddressAsync(Address address)
    {
        await _context.Addresses.AddAsync(address);
    }

    public async Task<List<Contact.Contact>> GetContactsAsync(Guid tenantId)
    {
        return await _context.Contacts
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Contact.Contact?> GetContactByIdAsync(Guid tenantId, Guid contactId)
    {
        return await _context.Contacts
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contactId && x.IsActive);
    }

    public async Task AddContactAsync(Contact.Contact contact)
    {
        await _context.Contacts.AddAsync(contact);
    }

    public async Task<List<AddressType.AddressType>> GetAddressTypesAsync(Guid tenantId)
    {
        return await _context.AddressTypes
            .Where(x => (x.TenantId == null || x.TenantId == tenantId) && x.IsActive)
            .ToListAsync();
    }

    public async Task<AddressType.AddressType?> GetAddressTypeByIdAsync(Guid? tenantId, Guid addressTypeId)
    {
        return await _context.AddressTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.AddressTypeId == addressTypeId && x.IsActive);
    }

    public async Task<AddressType.AddressType?> GetAddressTypeByCodeAsync(Guid? tenantId, string code)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return await _context.AddressTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.Code == normalized && x.IsActive);
    }

    public async Task AddAddressTypeAsync(AddressType.AddressType addressType)
    {
        await _context.AddressTypes.AddAsync(addressType);
    }

    public async Task<List<Contact.ContactType>> GetContactTypesAsync(Guid tenantId)
    {
        return await _context.ContactTypes
            .Where(x => (x.TenantId == null || x.TenantId == tenantId) && x.IsActive)
            .ToListAsync();
    }

    public async Task<Contact.ContactType?> GetContactTypeByIdAsync(Guid? tenantId, Guid contactTypeId)
    {
        return await _context.ContactTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.ContactTypeId == contactTypeId && x.IsActive);
    }

    public async Task<Contact.ContactType?> GetContactTypeByCodeAsync(Guid? tenantId, string code)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return await _context.ContactTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.Code == normalized && x.IsActive);
    }

    public async Task AddContactTypeAsync(Contact.ContactType contactType)
    {
        await _context.ContactTypes.AddAsync(contactType);
    }

    public async Task<List<Country.Country>> GetCountriesAsync()
    {
        return await _context.Countries
            .Where(x => x.IsActive)
            .OrderBy(x => x.CountryName)
            .ToListAsync();
    }

    public async Task<Country.Country?> GetCountryByIdAsync(Guid countryId)
    {
        return await _context.Countries
            .FirstOrDefaultAsync(x => x.CountryId == countryId && x.IsActive);
    }

    public async Task<Country.Country?> GetCountryByCodeOrNameAsync(string query)
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
