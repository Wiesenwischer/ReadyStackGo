import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  listInvitations,
  createInvitation,
  revokeInvitation,
  type InvitationDto,
} from '@rsgo/core';

const inputClass =
  'w-full h-11 px-4 py-2.5 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600';
const labelClass = 'block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300';

// Roles and the scope they apply at (mirrors the backend Role definitions).
const ROLE_OPTIONS = [
  { id: 'Viewer', label: 'Viewer (read-only)', scopeType: 'Organization' },
  { id: 'Operator', label: 'Operator (deploy/manage)', scopeType: 'Organization' },
  { id: 'OrganizationOwner', label: 'Organization Owner', scopeType: 'Organization' },
  { id: 'SystemAdmin', label: 'System Administrator', scopeType: 'Global' },
];

export default function InvitationsPage() {
  const [invitations, setInvitations] = useState<InvitationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [email, setEmail] = useState('');
  const [roleId, setRoleId] = useState('Operator');
  const [scopeId, setScopeId] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const selectedRole = ROLE_OPTIONS.find((r) => r.id === roleId) ?? ROLE_OPTIONS[1];
  const needsScopeId = selectedRole.scopeType !== 'Global';

  const reload = () => {
    setLoading(true);
    listInvitations()
      .then(setInvitations)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load invitations'))
      .finally(() => setLoading(false));
  };

  useEffect(reload, []);

  const handleInvite = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setSubmitting(true);
    try {
      await createInvitation({
        email,
        roleId,
        scopeType: selectedRole.scopeType,
        scopeId: needsScopeId ? scopeId : undefined,
      });
      setSuccess(`Invitation sent to ${email}.`);
      setEmail('');
      setScopeId('');
      reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to send invitation');
    } finally {
      setSubmitting(false);
    }
  };

  const handleRevoke = async (id: string) => {
    setError('');
    try {
      await revokeInvitation(id);
      reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to revoke invitation');
    }
  };

  return (
    <div className="max-w-3xl p-6 mx-auto">
      <div className="mb-6">
        <Link to="/settings" className="text-sm text-gray-500 hover:text-brand-600">← Settings</Link>
        <h2 className="mt-2 text-2xl font-bold text-black dark:text-white">User Invitations</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Invite people by email. They confirm ownership and set a password via the invitation link.
          Requires email (SMTP) to be configured.
        </p>
      </div>

      {error && (
        <div className="mb-4 p-4 text-sm border rounded-lg border-red-300 bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}
      {success && (
        <div className="mb-4 p-4 text-sm border rounded-lg border-green-300 bg-green-50 text-green-800 dark:bg-green-900/20 dark:border-green-800 dark:text-green-400">
          {success}
        </div>
      )}

      <form onSubmit={handleInvite} className="p-5 mb-8 border rounded-2xl border-gray-200 dark:border-gray-800 dark:bg-white/[0.03]">
        <h3 className="mb-4 text-lg font-semibold text-black dark:text-white">Invite a user</h3>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className={labelClass}>Email <span className="text-error-500">*</span></label>
            <input className={inputClass} type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="user@example.com" required />
          </div>
          <div>
            <label className={labelClass}>Role <span className="text-error-500">*</span></label>
            <select className={inputClass} value={roleId} onChange={(e) => setRoleId(e.target.value)}>
              {ROLE_OPTIONS.map((r) => (
                <option key={r.id} value={r.id}>{r.label}</option>
              ))}
            </select>
          </div>
        </div>

        {needsScopeId && (
          <div className="mt-4">
            <label className={labelClass}>Organization ID <span className="text-error-500">*</span></label>
            <input className={inputClass} value={scopeId} onChange={(e) => setScopeId(e.target.value)} placeholder="your-organization-id" required />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              The organization this role applies to. System Administrators are not scoped to an organization.
            </p>
          </div>
        )}

        <button
          type="submit"
          disabled={submitting}
          className="mt-5 px-5 py-2.5 text-sm font-medium text-white rounded-lg bg-brand-600 hover:bg-brand-700 disabled:opacity-50"
        >
          {submitting ? 'Sending…' : 'Send invitation'}
        </button>
      </form>

      <h3 className="mb-3 text-lg font-semibold text-black dark:text-white">Invitations</h3>
      {loading ? (
        <p className="text-gray-500 dark:text-gray-400">Loading…</p>
      ) : invitations.length === 0 ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">No invitations yet.</p>
      ) : (
        <div className="overflow-hidden border rounded-2xl border-gray-200 dark:border-gray-800">
          <table className="w-full text-sm">
            <thead className="text-left text-gray-500 bg-gray-50 dark:bg-white/[0.03] dark:text-gray-400">
              <tr>
                <th className="px-4 py-3 font-medium">Email</th>
                <th className="px-4 py-3 font-medium">Role</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Expires</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
              {invitations.map((inv) => (
                <tr key={inv.id} className="text-gray-700 dark:text-gray-300">
                  <td className="px-4 py-3">{inv.email}</td>
                  <td className="px-4 py-3">{inv.roleId}</td>
                  <td className="px-4 py-3">{inv.status}</td>
                  <td className="px-4 py-3">{new Date(inv.expiresAt).toLocaleDateString()}</td>
                  <td className="px-4 py-3 text-right">
                    {inv.status === 'Pending' && (
                      <button onClick={() => handleRevoke(inv.id)} className="text-sm text-red-600 hover:text-red-700">
                        Revoke
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
