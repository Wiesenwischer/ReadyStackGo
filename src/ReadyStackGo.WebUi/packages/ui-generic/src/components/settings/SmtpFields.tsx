import type { SmtpSettingsDto } from '@rsgo/core';

const inputClass =
  'w-full h-11 px-4 py-2.5 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:focus:border-brand-600';
const labelClass = 'block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300';

interface SmtpFieldsProps {
  value: SmtpSettingsDto;
  onChange: (patch: Partial<SmtpSettingsDto>) => void;
  /** Whether to show the "enable email sending" toggle (hidden in the wizard, where saving implies enable). */
  showEnableToggle?: boolean;
}

/**
 * Shared SMTP form fields, used by both the admin settings page and the setup wizard step,
 * so the field set and validation hints stay in one place.
 */
export default function SmtpFields({ value, onChange, showEnableToggle = true }: SmtpFieldsProps) {
  return (
    <div className="space-y-5">
      {showEnableToggle && (
        <label className="flex items-center gap-3">
          <input type="checkbox" checked={value.enabled} onChange={(e) => onChange({ enabled: e.target.checked })} />
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Enable email sending</span>
        </label>
      )}

      <div className="grid grid-cols-3 gap-4">
        <div className="col-span-2">
          <label className={labelClass}>SMTP host</label>
          <input className={inputClass} value={value.host} onChange={(e) => onChange({ host: e.target.value })} placeholder="smtp.example.com" />
        </div>
        <div>
          <label className={labelClass}>Port</label>
          <input className={inputClass} type="number" value={value.port} onChange={(e) => onChange({ port: Number(e.target.value) })} />
        </div>
      </div>

      <label className="flex items-center gap-3">
        <input type="checkbox" checked={value.useStartTls} onChange={(e) => onChange({ useStartTls: e.target.checked })} />
        <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Use STARTTLS</span>
      </label>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className={labelClass}>Username</label>
          <input className={inputClass} value={value.username ?? ''} onChange={(e) => onChange({ username: e.target.value })} autoComplete="off" />
        </div>
        <div>
          <label className={labelClass}>Password</label>
          <input
            className={inputClass}
            type="password"
            value={value.password ?? ''}
            onChange={(e) => onChange({ password: e.target.value })}
            placeholder={value.hasPassword ? '•••••••• (unchanged)' : ''}
            autoComplete="new-password"
          />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className={labelClass}>From address</label>
          <input className={inputClass} value={value.fromAddress} onChange={(e) => onChange({ fromAddress: e.target.value })} placeholder="noreply@example.com" />
        </div>
        <div>
          <label className={labelClass}>From name</label>
          <input className={inputClass} value={value.fromName} onChange={(e) => onChange({ fromName: e.target.value })} />
        </div>
      </div>
    </div>
  );
}
