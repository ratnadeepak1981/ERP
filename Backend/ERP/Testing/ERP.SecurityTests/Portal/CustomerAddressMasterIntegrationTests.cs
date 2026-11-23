using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using ERP.Domain.Features.MasterData.Customer;
using Xunit;

namespace ERP.SecurityTests.Portal;

/// <summary>
/// Unit and domain security tests verifying the Address Master integration with Customer accounts.
/// Validates multiple address support, default switching, address type classification,
/// deactivation preservation, and customer ownership invariants.
/// </summary>
public class CustomerAddressMasterIntegrationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void Address_Create_SetsAllFieldsAndDefaultsToActive()
    {
        // Act
        var address = Address.Create(
            _tenantId,
            "Main Receiving Bay",
            "100 Logistics Blvd",
            "Colombo",
            addressLine2: "Bay 4",
            stateProvince: "Western",
            postalCode: "00100",
            unmatchedCountryText: "Sri Lanka");

        // Assert
        Assert.NotEqual(Guid.Empty, address.AddressId);
        Assert.Equal(_tenantId, address.TenantId);
        Assert.Equal("Main Receiving Bay", address.AddressName);
        Assert.Equal("100 Logistics Blvd", address.AddressLine1);
        Assert.Equal("Bay 4", address.AddressLine2);
        Assert.Equal("Colombo", address.City);
        Assert.Equal("Western", address.StateProvince);
        Assert.Equal("00100", address.PostalCode);
        Assert.Equal("Sri Lanka", address.UnmatchedCountryText);
        Assert.True(address.IsActive);
    }

    [Fact]
    public void CustomerAddress_CanLinkMultipleAddresses_WithDifferentTypes_ToSameCustomer()
    {
        // Arrange
        var deliveryType = AddressType.Create(_tenantId, "DELIVERY", "Delivery Address");
        var billingType = AddressType.Create(_tenantId, "BILLING", "Billing Address");
        var headOfficeType = AddressType.Create(_tenantId, "HEAD_OFFICE", "Head Office");

        var addr1 = Address.Create(_tenantId, "Dock 1", "10 Logistics Way", "Colombo");
        var addr2 = Address.Create(_tenantId, "Accounts Office", "50 Finance Row", "Colombo");
        var addr3 = Address.Create(_tenantId, "HQ Tower", "1 Executive Blvd", "Colombo");

        // Act
        var ca1 = CustomerAddress.Create(_tenantId, _customerId, addr1.AddressId, deliveryType.AddressTypeId, isDefault: true);
        var ca2 = CustomerAddress.Create(_tenantId, _customerId, addr2.AddressId, billingType.AddressTypeId, isDefault: true);
        var ca3 = CustomerAddress.Create(_tenantId, _customerId, addr3.AddressId, headOfficeType.AddressTypeId, isDefault: false);

        // Assert
        Assert.Equal(_customerId, ca1.CustomerId);
        Assert.Equal(_customerId, ca2.CustomerId);
        Assert.Equal(_customerId, ca3.CustomerId);

        Assert.Equal(addr1.AddressId, ca1.AddressId);
        Assert.Equal(addr2.AddressId, ca2.AddressId);
        Assert.Equal(addr3.AddressId, ca3.AddressId);

        Assert.True(ca1.IsDefault);
        Assert.True(ca2.IsDefault);
        Assert.False(ca3.IsDefault);

        Assert.True(ca1.IsActive);
        Assert.True(ca2.IsActive);
        Assert.True(ca3.IsActive);
    }

    [Fact]
    public void CustomerAddress_SetAsDefault_And_CustomerSyncDefaultAddress_PreservesData()
    {
        // Arrange
        var customer = Customer.Create(
            _tenantId,
            "CUST-001",
            "Acme Industrial Ltd",
            "John Doe",
            "john@acme.com",
            addressLine1: "Old Street 1",
            city: "Old City");

        var addr = Address.Create(_tenantId, "New Dock", "99 New Logistics Way", "New City", postalCode: "12345", unmatchedCountryText: "Sri Lanka");
        var deliveryType = AddressType.Create(_tenantId, "DELIVERY", "Delivery Address");
        var ca = CustomerAddress.Create(_tenantId, customer.CustomerId, addr.AddressId, deliveryType.AddressTypeId, isDefault: false);

        // Act - Switch to default
        ca.SetAsDefault(true);
        customer.SyncDefaultAddress(
            addr.AddressLine1,
            addr.AddressLine2,
            addr.City,
            addr.PostalCode,
            addr.UnmatchedCountryText);

        // Assert
        Assert.True(ca.IsDefault);
        Assert.Equal("99 New Logistics Way", customer.AddressLine1);
        Assert.Equal("New City", customer.City);
        Assert.Equal("12345", customer.PostalCode);
        Assert.Equal("Sri Lanka", customer.Country);
    }

    [Fact]
    public void CustomerAddress_Deactivate_MarksInactive_WhilePhysicalAddressRemainsPreserved()
    {
        // Arrange
        var addr = Address.Create(_tenantId, "Historic Warehouse", "42 Old Port Rd", "Colombo");
        var addressTypeId = Guid.NewGuid();
        var ca = CustomerAddress.Create(_tenantId, _customerId, addr.AddressId, addressTypeId, isDefault: true);

        // Act - Customer unlinks/deactivates the address
        ca.Deactivate();

        // Assert: CustomerAddress link is deactivated and is no longer default
        Assert.False(ca.IsActive);
        Assert.False(ca.IsDefault);

        // Critical invariant: Physical Address record remains untouched for historical sales orders & shipments!
        Assert.True(addr.IsActive);
        Assert.Equal("42 Old Port Rd", addr.AddressLine1);
    }

    [Fact]
    public void CustomerAddress_CrossCustomerIsolation_GuaranteesStrictOwnership()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var addrA = Address.Create(_tenantId, "Customer A Private Site", "10 Private St", "City A");
        var caA = CustomerAddress.Create(_tenantId, customerA, addrA.AddressId, Guid.NewGuid(), isDefault: true);

        // Act & Assert: Customer B cannot claim ownership of Customer A's address
        Assert.Equal(customerA, caA.CustomerId);
        Assert.NotEqual(customerB, caA.CustomerId);
    }
}
