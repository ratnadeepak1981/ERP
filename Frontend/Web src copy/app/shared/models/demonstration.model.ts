export interface DemoSalesOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  orderDate: string;
  totalAmount: number;
  status: 'Draft' | 'Confirmed' | 'Processing' | 'Shipped' | 'Fulfilled' | 'Cancelled';
  itemCount: number;
  branch: string;
}
