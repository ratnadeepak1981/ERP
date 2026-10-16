import { LookupItem } from '../../../../shared/components/lookup-dropdown/lookup-item.model';

export interface PurchaseOrderItem {
  lineId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
  status?: 'Approved' | 'Pending' | 'Draft' | 'Received';
}

export const PO_LOOKUP_PRODUCTS: LookupItem[] = [
  { id: 'RAW-001', code: 'RAW-001', name: 'Steel Sheet (Hot Rolled)', category: 'Raw Materials', isEligible: true, meta: { cost: 12.50 } },
  { id: 'RAW-002', code: 'RAW-002', name: 'Aluminium Rod 20mm', category: 'Raw Materials', isEligible: true, meta: { cost: 8.75 } },
  { id: 'RAW-003', code: 'RAW-003', name: 'Copper Bar 15mm', category: 'Raw Materials', isEligible: true, meta: { cost: 24.00 } },
  { id: 'RAW-004', code: 'RAW-004', name: 'Brass Fitting M10', category: 'Hardware', isEligible: true, meta: { cost: 6.20 } },
  { id: 'RAW-005', code: 'RAW-005', name: 'Titanium Fastener Grade 5', category: 'Hardware', isEligible: true, meta: { cost: 45.00 } }
];

export const PO_PRODUCT_OPTIONS = [
  { label: 'RAW-001 - Steel Sheet ($12.50)', value: 'RAW-001', name: 'Steel Sheet', cost: 12.50 },
  { label: 'RAW-002 - Aluminium Rod ($8.75)', value: 'RAW-002', name: 'Aluminium Rod', cost: 8.75 },
  { label: 'RAW-003 - Copper Bar ($24.00)', value: 'RAW-003', name: 'Copper Bar', cost: 24.00 },
  { label: 'RAW-004 - Brass Fitting ($6.20)', value: 'RAW-004', name: 'Brass Fitting', cost: 6.20 }
];

export const SAMPLE_PURCHASE_ORDER_ITEMS: PurchaseOrderItem[] = [
  {
    lineId: 'POL-001',
    productCode: 'RAW-001',
    productName: 'Steel Sheet (Hot Rolled)',
    quantity: 100,
    unitCost: 12.50,
    lineTotal: 1250.00,
    status: 'Approved'
  },
  {
    lineId: 'POL-002',
    productCode: 'RAW-002',
    productName: 'Aluminium Rod 20mm',
    quantity: 80,
    unitCost: 8.75,
    lineTotal: 700.00,
    status: 'Approved'
  },
  {
    lineId: 'POL-003',
    productCode: 'RAW-003',
    productName: 'Copper Bar 15mm',
    quantity: 25,
    unitCost: 24.00,
    lineTotal: 600.00,
    status: 'Pending'
  }
];
