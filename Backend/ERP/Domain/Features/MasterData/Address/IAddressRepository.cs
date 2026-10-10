using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Address;

public interface IAddressRepository
{
    Task<List<Address>> GetAddressesAsync(Guid tenantId);
    Task<Address?> GetAddressByIdAsync(Guid tenantId, Guid addressId);
    Task AddAddressAsync(Address address);

    Task<List<Contact.Contact>> GetContactsAsync(Guid tenantId);
    Task<Contact.Contact?> GetContactByIdAsync(Guid tenantId, Guid contactId);
    Task AddContactAsync(Contact.Contact contact);

    Task<List<AddressType.AddressType>> GetAddressTypesAsync(Guid tenantId);
    Task<AddressType.AddressType?> GetAddressTypeByIdAsync(Guid? tenantId, Guid addressTypeId);
    Task<AddressType.AddressType?> GetAddressTypeByCodeAsync(Guid? tenantId, string code);
    Task AddAddressTypeAsync(AddressType.AddressType addressType);

    Task<List<Contact.ContactType>> GetContactTypesAsync(Guid tenantId);
    Task<Contact.ContactType?> GetContactTypeByIdAsync(Guid? tenantId, Guid contactTypeId);
    Task<Contact.ContactType?> GetContactTypeByCodeAsync(Guid? tenantId, string code);
    Task AddContactTypeAsync(Contact.ContactType contactType);

    Task<List<Country.Country>> GetCountriesAsync();
    Task<Country.Country?> GetCountryByIdAsync(Guid countryId);
    Task<Country.Country?> GetCountryByCodeOrNameAsync(string query);

    Task SaveChangesAsync();
}
