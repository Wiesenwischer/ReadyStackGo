import { useState } from 'react';
import { useProfileStore, type ExternalIdentityDto } from '@rsgo/core';
import ConnectedAccounts from './ConnectedAccounts';
import SetLocalPasswordCard from './SetLocalPasswordCard';
import { Alert } from '../../components/ui/Alert';

function formatDate(isoString?: string): string {
  if (!isoString) return "-";
  return new Date(isoString).toLocaleDateString(undefined, {
    year: "numeric",
    month: "long",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function Profile() {
  const store = useProfileStore();
  const [identities, setIdentities] = useState<ExternalIdentityDto[]>([]);
  const hasPassword = store.profile?.hasPassword ?? true;
  const providerName = identities[0]?.displayName ?? 'single sign-on';

  const handleChangePassword = (e: React.FormEvent) => {
    e.preventDefault();
    store.changePassword();
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Header */}
      <div className="mb-8">
        <h2 className="text-[26px] font-bold leading-[34px] text-fg">
          Profile
        </h2>
        <p className="mt-1 text-sm text-fg-secondary">
          Your account information and password management
        </p>
      </div>

      {store.isLoading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      ) : store.error ? (
        <div role="alert" className="rounded-xl border border-status-unhealthy/35 bg-status-unhealthy-bg px-4 py-3 text-sm text-fg">
          {store.error}
        </div>
      ) : (
        <div className="space-y-6">
          {store.profile?.noAdminWithPassword && (
            <Alert
              tone="warning"
              testId="no-password-warning"
              title={
                store.profile.systemAdminCount > 1
                  ? 'No system administrator has a local password'
                  : 'You are the only system administrator and have no local password'
              }
            >
              You sign in with {providerName}. If {providerName} is unavailable, nobody can sign in. Set a local password below.
            </Alert>
          )}

          {/* Account Information */}
          <div className="rounded-2xl border border-line bg-surface">
            <div className="border-b border-line px-6 py-[18px]">
              <h3 className="text-[17px] font-semibold text-fg">
                Account Information
              </h3>
            </div>
            <div className="px-6 py-5">
              <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <dt className="text-xs font-medium text-fg-muted">
                    Username
                  </dt>
                  <dd className="mt-1 text-sm text-fg">
                    {store.profile?.username}
                  </dd>
                </div>
                <div>
                  <dt className="text-xs font-medium text-fg-muted">
                    Role
                  </dt>
                  <dd className="mt-1">
                    <span className="inline-flex items-center rounded-full bg-primary-subtle px-2.5 py-0.5 text-xs font-medium text-fg-brand">
                      {store.roleLabel}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt className="text-xs font-medium text-fg-muted">
                    Member since
                  </dt>
                  <dd className="mt-1 text-sm text-fg">
                    {formatDate(store.profile?.createdAt)}
                  </dd>
                </div>
                {hasPassword ? (
                  <div>
                    <dt className="text-xs font-medium text-fg-muted">
                      Password last changed
                    </dt>
                    <dd className="mt-1 text-sm text-fg">
                      {formatDate(store.profile?.passwordChangedAt)}
                    </dd>
                  </div>
                ) : (
                  <div>
                    <dt className="text-xs font-medium text-fg-muted">Password</dt>
                    <dd className="mt-1 text-sm text-status-degraded" data-testid="password-not-set">
                      Not set
                    </dd>
                  </div>
                )}
              </dl>
            </div>
          </div>

          {/* Change Password, or "Set a local password" for accounts without one */}
          {hasPassword ? (
          <div className="rounded-2xl border border-line bg-surface">
            <div className="border-b border-line px-6 py-[18px]">
              <h3 className="text-[17px] font-semibold text-fg">
                Change Password
              </h3>
            </div>
            <div className="px-6 py-5">
              <form onSubmit={handleChangePassword} className="max-w-md space-y-4">
                <div>
                  <label
                    htmlFor="currentPassword"
                    className="mb-1.5 block text-sm font-medium text-fg"
                  >
                    Current Password
                  </label>
                  <input
                    id="currentPassword"
                    type="password"
                    value={store.currentPassword}
                    onChange={(e) => store.setCurrentPassword(e.target.value)}
                    required
                    className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none transition focus:border-primary focus:ring-1 focus:ring-primary"
                  />
                </div>
                <div>
                  <label
                    htmlFor="newPassword"
                    className="mb-1.5 block text-sm font-medium text-fg"
                  >
                    New Password
                  </label>
                  <input
                    id="newPassword"
                    type="password"
                    value={store.newPassword}
                    onChange={(e) => store.setNewPassword(e.target.value)}
                    required
                    minLength={8}
                    className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none transition focus:border-primary focus:ring-1 focus:ring-primary"
                  />
                  <p className="mt-1.5 text-xs text-fg-muted">
                    Minimum 8 characters with at least one uppercase letter, one
                    lowercase letter, and one digit.
                  </p>
                </div>
                <div>
                  <label
                    htmlFor="confirmPassword"
                    className="mb-1.5 block text-sm font-medium text-fg"
                  >
                    Confirm New Password
                  </label>
                  <input
                    id="confirmPassword"
                    type="password"
                    value={store.confirmPassword}
                    onChange={(e) => store.setConfirmPassword(e.target.value)}
                    required
                    minLength={8}
                    className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none transition focus:border-primary focus:ring-1 focus:ring-primary"
                  />
                </div>

                {store.changeError && (
                  <div role="alert" className="rounded-xl border border-status-unhealthy/35 bg-status-unhealthy-bg px-4 py-3 text-sm text-fg">
                    {store.changeError}
                  </div>
                )}

                {store.changeSuccess && (
                  <div role="status" className="rounded-xl border border-status-healthy/35 bg-status-healthy-bg px-4 py-3 text-sm text-fg">
                    {store.changeSuccess}
                  </div>
                )}

                <button
                  type="submit"
                  disabled={!store.canSubmitPasswordChange}
                  className="inline-flex h-10 items-center gap-2 rounded-[10px] bg-primary px-[18px] text-sm font-semibold text-on-primary transition-colors hover:bg-primary-hover disabled:cursor-not-allowed disabled:opacity-45"
                >
                  {store.changing ? (
                    <>
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-on-primary border-t-transparent" />
                      Changing...
                    </>
                  ) : (
                    "Change Password"
                  )}
                </button>
              </form>
            </div>
          </div>
          ) : (
            <SetLocalPasswordCard
              username={store.profile?.username ?? ''}
              providerName={providerName}
              onDone={store.reload}
            />
          )}

          {/* Connected accounts (OIDC) */}
          <ConnectedAccounts hasPassword={hasPassword} onIdentities={setIdentities} />
        </div>
      )}
    </div>
  );
}
