const STATUS_CLASS = {
  Draft: 'status-draft',
  Submitted: 'status-submitted',
  Approved: 'status-approved',
  Rejected: 'status-rejected',
  Reimbursing: 'status-reimbursing',
  Reimbursed: 'status-reimbursed'
};

export default function StatusBadge({ status }) {
  return <span className={`badge ${STATUS_CLASS[status] || ''}`}>{status}</span>;
}
