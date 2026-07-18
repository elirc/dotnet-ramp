export const money = (amount, currency = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount ?? 0);

export const date = (value) => (value ? new Date(value).toLocaleDateString() : '');

export const dateTime = (value) => (value ? new Date(value).toLocaleString() : '');

export const CATEGORIES = [
  'Meals', 'Travel', 'Lodging', 'Transportation',
  'OfficeSupplies', 'Software', 'Entertainment', 'Other'
];

export const STATUSES = [
  'Draft', 'Submitted', 'Approved', 'Rejected', 'Reimbursing', 'Reimbursed'
];
