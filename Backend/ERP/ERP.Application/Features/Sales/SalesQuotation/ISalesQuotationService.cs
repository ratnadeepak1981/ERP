using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.Procurement.Approval;
using Domain.Features.Sales.SalesQuotation;

namespace ERP.Application.Features.Sales.SalesQuotation;

public interface ISalesQuotationService
{
    Task<List<SalesQuotationDto>> GetQuotationsAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesQuotationStatus? status = null,
        SalesQuotationType? documentType = null);

    Task<SalesQuotationDto?> GetQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId);

    Task<List<SalesQuotationDto>> GetCustomerQuotationsAsync(
        Guid tenantId,
        Guid customerId,
        SalesQuotationStatus? status = null);

    Task<SalesQuotationDto> CreateQuotationDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesQuotationDraftRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null);

    Task<SalesQuotationDto> CreateCustomerQuotationRequestAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateCustomerQuotationRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null);

    Task<SalesQuotationDto> UpdateQuotationDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        UpdateSalesQuotationDraftRequest request);

    Task<SalesQuotationItemDto> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        CreateSalesQuotationItemRequest request);

    Task<SalesQuotationItemDto> UpdateItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid itemId,
        UpdateSalesQuotationItemRequest request);

    Task RemoveItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid itemId);

    Task<SalesQuotationDto> SubmitQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName);

    Task<SalesQuotationDto> ApproveQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null);

    Task<SalesQuotationDto> RejectQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid approverUserId,
        string approverUserName,
        string remarks);

    Task<SalesQuotationDto> CancelQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null);

    Task<SalesQuotationDto> ReturnToDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null);

    Task<SalesQuotationDto> PrepareCommercialQuotationFromRequestAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerRequestId,
        Guid actorUserId,
        string actorUserName);

    Task<SalesQuotationDto> SendQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName,
        bool isRetry = false);

    Task<SalesQuotationDto> AcceptQuotationAndConvertAtomicAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        Guid quotationId,
        AcceptQuotationRequest? request = null);

    Task<SalesQuotationDto> ConvertQuotationToOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName);

    Task<SalesQuotationDto> DeclineQuotationAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        Guid quotationId,
        DeclineQuotationRequest request);

    Task<SalesQuotationDto> CloneCustomerQuotationRequestFromOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid customerContactId,
        Guid sourceSalesOrderId,
        string? customerRemarks = null);

    Task<SalesQuotationDto> CloneCustomerQuotationRequestFromQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid customerContactId,
        Guid sourceSalesQuotationId,
        string? customerRemarks = null);

    Task<CustomerSalesQuotationDto?> GetCustomerQuotationDetailsAsync(
        Guid tenantId,
        Guid customerId,
        Guid quotationId);

    Task<byte[]> GenerateQuotationPdfAsync(
        Guid tenantId,
        Guid quotationId);

    Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid quotationId);
}
