using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Customer;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly IAddressRepository _addressRepository;
    private readonly DomainDbContext _context;

    public CustomerService(
        ICustomerRepository repository,
        IAddressRepository addressRepository,
        DomainDbContext context)
    {
        _repository = repository;
        _addressRepository = addressRepository;
        _context = context;
    }

    public async Task<List<Customer>> GetCustomersAsync(Guid tenantId)
    {
        return await _repository.GetCustomersAsync(tenantId);
    }

    public async Task<Customer?> GetCustomerAsync(Guid tenantId, Guid customerId)
    {
        return await _repository.GetCustomerByIdAsync(tenantId, customerId);
    }

    public async Task<Customer> CreateCustomerAsync(Guid tenantId, CreateCustomerRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.CustomerCode))
            throw new ArgumentException("Customer code is required.", nameof(request.CustomerCode));

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("Customer name is required.", nameof(request.CustomerName));

        string code = request.CustomerCode.Trim();
        string name = request.CustomerName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Customer code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Customer name '{name}' already exists for this tenant.");

        var customer = Customer.Create(
            tenantId,
            code,
            name,
            request.ContactPerson?.Trim(),
            request.Email?.Trim(),
            request.Phone?.Trim(),
            request.AddressLine1?.Trim(),
            request.AddressLine2?.Trim(),
            request.City?.Trim(),
            request.PostalCode?.Trim(),
            request.Country?.Trim(),
            request.TaxNumber?.Trim(),
            request.CreditLimit,
            request.PaymentTerms?.Trim());

        await _repository.AddCustomerAsync(customer);
        await _repository.SaveChangesAsync();

        // If legacy address fields were provided, create normalized address and link as default
        if (!string.IsNullOrWhiteSpace(request.AddressLine1) && !string.IsNullOrWhiteSpace(request.City))
        {
            await CreateNormalizedAddressForCustomerAsync(tenantId, customer, request);
        }

        // If legacy contact fields were provided, create normalized contact and link as primary
        if (!string.IsNullOrWhiteSpace(request.ContactPerson) || !string.IsNullOrWhiteSpace(request.Email) || !string.IsNullOrWhiteSpace(request.Phone))
        {
            await CreateNormalizedContactForCustomerAsync(tenantId, customer, request);
        }

        return customer;
    }

    private async Task CreateNormalizedAddressForCustomerAsync(Guid tenantId, Customer customer, CreateCustomerRequest request)
    {
        Guid? countryId = null;
        string? unmatchedCountry = request.Country?.Trim();

        if (!string.IsNullOrWhiteSpace(unmatchedCountry))
        {
            var country = await _addressRepository.GetCountryByCodeOrNameAsync(unmatchedCountry);
            if (country != null)
            {
                countryId = country.CountryId;
                unmatchedCountry = null;
            }
        }

        var address = Address.Create(
            tenantId,
            $"{customer.CustomerName} - Default Address",
            request.AddressLine1!.Trim(),
            request.City.Trim(),
            request.AddressLine2?.Trim(),
            null,
            request.PostalCode?.Trim(),
            countryId,
            unmatchedCountry);

        await _addressRepository.AddAddressAsync(address);
        await _addressRepository.SaveChangesAsync();

        // Get or default address type GENERAL / OTHER
        var addressType = await _addressRepository.GetAddressTypeByCodeAsync(tenantId, "GENERAL")
            ?? await _addressRepository.GetAddressTypeByCodeAsync(tenantId, "OTHER")
            ?? (await _addressRepository.GetAddressTypesAsync(tenantId)).FirstOrDefault();

        if (addressType != null)
        {
            var association = CustomerAddress.Create(tenantId, customer.CustomerId, address.AddressId, addressType.AddressTypeId, true);
            await _repository.AddAddressAssociationAsync(association);
            await _repository.SaveChangesAsync();
        }
    }

    private async Task CreateNormalizedContactForCustomerAsync(Guid tenantId, Customer customer, CreateCustomerRequest request)
    {
        string contactName = !string.IsNullOrWhiteSpace(request.ContactPerson) ? request.ContactPerson.Trim() : $"{customer.CustomerName} Contact";
        var contact = global::Domain.Features.MasterData.Contact.Contact.Create(
            tenantId,
            contactName,
            null,
            request.Email?.Trim(),
            request.Phone?.Trim());

        await _addressRepository.AddContactAsync(contact);
        await _addressRepository.SaveChangesAsync();

        var contactType = await _addressRepository.GetContactTypeByCodeAsync(tenantId, "PRIMARY")
            ?? await _addressRepository.GetContactTypeByCodeAsync(tenantId, "GENERAL")
            ?? (await _addressRepository.GetContactTypesAsync(tenantId)).FirstOrDefault();

        if (contactType != null)
        {
            var association = CustomerContact.Create(tenantId, customer.CustomerId, contact.ContactId, contactType.ContactTypeId, true);
            await _repository.AddContactAssociationAsync(association);
            await _repository.SaveChangesAsync();
        }
    }

    public async Task<Customer> UpdateCustomerAsync(Guid tenantId, Guid customerId, UpdateCustomerRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var customer = await _repository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("Customer name is required.", nameof(request.CustomerName));

        string name = request.CustomerName.Trim();
        if (customer.CustomerName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Customer name '{name}' already exists for this tenant.");

        customer.Update(
            name,
            request.ContactPerson?.Trim(),
            request.Email?.Trim(),
            request.Phone?.Trim(),
            request.AddressLine1?.Trim(),
            request.AddressLine2?.Trim(),
            request.City?.Trim(),
            request.PostalCode?.Trim(),
            request.Country?.Trim(),
            request.TaxNumber?.Trim(),
            request.CreditLimit,
            request.PaymentTerms?.Trim());

        await _repository.SaveChangesAsync();
        return customer;
    }

    public async Task DeactivateCustomerAsync(Guid tenantId, Guid customerId)
    {
        var customer = await _repository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        customer.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<CustomerAddress>> GetAddressesAsync(Guid tenantId, Guid customerId)
    {
        return await _repository.GetAddressesAsync(tenantId, customerId);
    }

    public async Task<CustomerAddress> LinkAddressAsync(Guid tenantId, Guid customerId, CustomerAddressLinkRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var customer = await _repository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        // Tenant ownership check on address
        var address = await _addressRepository.GetAddressByIdAsync(tenantId, request.AddressId);
        if (address == null)
            throw new InvalidOperationException("Address not found or does not belong to the current tenant.");

        // Ownership / system check on AddressType (null or current tenant)
        var addressType = await _addressRepository.GetAddressTypeByIdAsync(tenantId, request.AddressTypeId);
        if (addressType == null)
            throw new InvalidOperationException("AddressType not found or does not belong to the current tenant.");

        var existing = await _repository.GetAddressAssociationAsync(tenantId, customerId, request.AddressId, request.AddressTypeId);
        if (existing != null)
            throw new InvalidOperationException("This address association already exists for this customer.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        if (request.IsDefault)
        {
            var activeDefaults = (await _repository.GetAddressesAsync(tenantId, customerId))
                .Where(x => x.AddressTypeId == request.AddressTypeId && x.IsDefault && x.IsActive)
                .ToList();

            foreach (var d in activeDefaults)
            {
                d.SetAsDefault(false);
            }
            await _repository.SaveChangesAsync();
        }

        var association = CustomerAddress.Create(
            tenantId,
            customerId,
            request.AddressId,
            request.AddressTypeId,
            request.IsDefault);

        await _repository.AddAddressAssociationAsync(association);
        await _repository.SaveChangesAsync();

        if (request.IsDefault)
        {
            // Mirror to customer flat fields
            string? countryName = null;
            if (address.CountryId.HasValue)
            {
                var country = await _addressRepository.GetCountryByIdAsync(address.CountryId.Value);
                countryName = country?.CountryName;
            }
            else
            {
                countryName = address.UnmatchedCountryText;
            }

            customer.SyncDefaultAddress(address.AddressLine1, address.AddressLine2, address.City, address.PostalCode, countryName);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return association;
    }

    public async Task SetDefaultAddressAsync(Guid tenantId, Guid customerId, Guid customerAddressId)
    {
        var customer = await _repository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var association = await _repository.GetAddressAssociationByIdAsync(tenantId, customerAddressId)
            ?? throw new KeyNotFoundException("Customer address association not found.");

        if (association.CustomerId != customerId)
            throw new InvalidOperationException("Customer address does not belong to this customer.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var activeDefaults = (await _repository.GetAddressesAsync(tenantId, customerId))
            .Where(x => x.AddressTypeId == association.AddressTypeId && x.IsDefault && x.IsActive)
            .ToList();

        foreach (var d in activeDefaults)
        {
            d.SetAsDefault(false);
        }
        await _repository.SaveChangesAsync();

        association.SetAsDefault(true);
        await _repository.SaveChangesAsync();

        var address = await _addressRepository.GetAddressByIdAsync(tenantId, association.AddressId);
        if (address != null)
        {
            string? countryName = null;
            if (address.CountryId.HasValue)
            {
                var country = await _addressRepository.GetCountryByIdAsync(address.CountryId.Value);
                countryName = country?.CountryName;
            }
            else
            {
                countryName = address.UnmatchedCountryText;
            }

            customer.SyncDefaultAddress(address.AddressLine1, address.AddressLine2, address.City, address.PostalCode, countryName);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task RemoveAddressLinkAsync(Guid tenantId, Guid customerId, Guid customerAddressId)
    {
        var association = await _repository.GetAddressAssociationByIdAsync(tenantId, customerAddressId)
            ?? throw new KeyNotFoundException("Customer address association not found.");

        if (association.CustomerId != customerId)
            throw new InvalidOperationException("Customer address does not belong to this customer.");

        association.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<CustomerContact>> GetContactsAsync(Guid tenantId, Guid customerId)
    {
        return await _repository.GetContactsAsync(tenantId, customerId);
    }

    public async Task<CustomerContact> LinkContactAsync(Guid tenantId, Guid customerId, CustomerContactLinkRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var customer = await _repository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var contact = await _addressRepository.GetContactByIdAsync(tenantId, request.ContactId);
        if (contact == null)
            throw new InvalidOperationException("Contact not found or does not belong to the current tenant.");

        var contactType = await _addressRepository.GetContactTypeByIdAsync(tenantId, request.ContactTypeId);
        if (contactType == null)
            throw new InvalidOperationException("ContactType not found or does not belong to the current tenant.");

        var existing = await _repository.GetContactAssociationAsync(tenantId, customerId, request.ContactId, request.ContactTypeId);
        if (existing != null)
            throw new InvalidOperationException("This contact association already exists for this customer.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        if (request.IsPrimary)
        {
            var activePrimaries = (await _repository.GetContactsAsync(tenantId, customerId))
                .Where(x => x.ContactTypeId == request.ContactTypeId && x.IsPrimary && x.IsActive)
                .ToList();

            foreach (var p in activePrimaries)
            {
                p.SetAsPrimary(false);
            }
            await _repository.SaveChangesAsync();
        }

        var association = CustomerContact.Create(
            tenantId,
            customerId,
            request.ContactId,
            request.ContactTypeId,
            request.IsPrimary);

        await _repository.AddContactAssociationAsync(association);
        await _repository.SaveChangesAsync();

        if (request.IsPrimary)
        {
            customer.SyncPrimaryContact(contact.ContactName, contact.Email, contact.Phone);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return association;
    }

    public async Task SetPrimaryContactAsync(Guid tenantId, Guid customerId, Guid customerContactId)
    {
        var customer = await _repository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var association = await _repository.GetContactAssociationByIdAsync(tenantId, customerContactId)
            ?? throw new KeyNotFoundException("Customer contact association not found.");

        if (association.CustomerId != customerId)
            throw new InvalidOperationException("Customer contact does not belong to this customer.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var activePrimaries = (await _repository.GetContactsAsync(tenantId, customerId))
            .Where(x => x.ContactTypeId == association.ContactTypeId && x.IsPrimary && x.IsActive)
            .ToList();

        foreach (var p in activePrimaries)
        {
            p.SetAsPrimary(false);
        }
        await _repository.SaveChangesAsync();

        association.SetAsPrimary(true);
        await _repository.SaveChangesAsync();

        var contact = await _addressRepository.GetContactByIdAsync(tenantId, association.ContactId);
        if (contact != null)
        {
            customer.SyncPrimaryContact(contact.ContactName, contact.Email, contact.Phone);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task RemoveContactLinkAsync(Guid tenantId, Guid customerId, Guid customerContactId)
    {
        var association = await _repository.GetContactAssociationByIdAsync(tenantId, customerContactId)
            ?? throw new KeyNotFoundException("Customer contact association not found.");

        if (association.CustomerId != customerId)
            throw new InvalidOperationException("Customer contact does not belong to this customer.");

        association.Deactivate();
        await _repository.SaveChangesAsync();
    }
}
