import { LookupItem } from '../../../../shared/components/lookup-dropdown/lookup-item.model';

export interface SalesOrderItem {
  lineId: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  status?: 'Fulfilled' | 'Shipped' | 'Processing' | 'Backorder' | 'Cancelled';
}

export const SO_LOOKUP_PRODUCTS: LookupItem[] = [
  { id: 'FIN-001', code: 'FIN-001', name: 'Steel Enclosure 400x300', category: 'Finished Goods', isEligible: true, meta: { price: 85.00 } },
  { id: 'FIN-002', code: 'FIN-002', name: 'PLC Automation Control Panel', category: 'Finished Goods', isEligible: true, meta: { price: 320.00 } },
  { id: 'FIN-003', code: 'FIN-003', name: 'Modular Power Distribution Rack', category: 'Finished Goods', isEligible: true, meta: { price: 145.00 } },
  { id: 'FIN-004', code: 'FIN-004', name: 'Industrial Heavy Duty Cabinet', category: 'Finished Goods', isEligible: true, meta: { price: 95.00 } },
  { id: 'FIN-005', code: 'FIN-005', name: 'Custom Rack Mounting Unit', category: 'Finished Goods', isEligible: true, meta: { price: 210.00 } }
];

export const SO_PRODUCT_OPTIONS = [
  { label: 'FIN-001 - Steel Cabinet ($85.00)', value: 'FIN-001', name: 'Steel Cabinet', price: 85.00 },
  { label: 'FIN-002 - Control Panel ($320.00)', value: 'FIN-002', name: 'Control Panel', price: 320.00 },
  { label: 'FIN-003 - Modular Enclosure ($145.00)', value: 'FIN-003', name: 'Modular Enclosure', price: 145.00 },
  { label: 'FIN-004 - Industrial Rack ($95.00)', value: 'FIN-004', name: 'Industrial Rack', price: 95.00 }
];

export const SAMPLE_SALES_ORDER_ITEMS: SalesOrderItem[] = [
  {
    lineId: 'SOL-001',
    productCode: 'FIN-001',
    productName: 'Steel Enclosure 400x300',
    quantity: 10,
    unitPrice: 85.00,
    lineTotal: 850.00,
    status: 'Processing'
  },
  {
    lineId: 'SOL-002',
    productCode: 'FIN-002',
    productName: 'PLC Automation Control Panel',
    quantity: 4,
    unitPrice: 320.00,
    lineTotal: 1280.00,
    status: 'Shipped'
  },
  {
    lineId: 'SOL-003',
    productCode: 'FIN-003',
    productName: 'Modular Power Distribution Rack',
    quantity: 15,
    unitPrice: 145.00,
    lineTotal: 2175.00,
    status: 'Processing'
  }
];
