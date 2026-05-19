import { type VariableInputProps, getVariableLabel } from '../VariableInput';

/**
 * Toggle switch for Boolean type variables.
 */
export default function BooleanInput({ variable, value, onChange, error, disabled }: VariableInputProps) {
  const label = getVariableLabel(variable);
  const isChecked = value === 'true' || value === '1';

  const handleToggle = () => {
    onChange(isChecked ? 'false' : 'true');
  };

  return (
    <div>
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0 flex-1">
          <label className="block text-sm text-gray-600 dark:text-gray-400">
            {label}
            {variable.isRequired && <span className="text-red-500 ml-1">*</span>}
          </label>
          {variable.description && (
            <p className="text-xs text-gray-500 dark:text-gray-500">{variable.description}</p>
          )}
        </div>
        <button
          type="button"
          role="switch"
          aria-checked={isChecked}
          aria-label={label}
          onClick={handleToggle}
          disabled={disabled}
          className={`relative inline-flex h-7 w-14 flex-shrink-0 items-center rounded-full border-2 transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 dark:focus:ring-offset-gray-900
            ${isChecked
              ? 'bg-brand-600 border-brand-600'
              : 'bg-gray-300 border-gray-400 dark:bg-gray-600 dark:border-gray-500'
            }
            ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}
          `}
        >
          <span
            className={`inline-block h-5 w-5 transform rounded-full bg-white shadow-md ring-1 ring-black/10 transition-transform
              ${isChecked ? 'translate-x-7' : 'translate-x-0.5'}
            `}
          />
          <span className="sr-only">{isChecked ? 'On' : 'Off'}</span>
        </button>
        <span className={`text-sm font-medium tabular-nums ${isChecked ? 'text-brand-600 dark:text-brand-400' : 'text-gray-500 dark:text-gray-400'}`}>
          {isChecked ? 'On' : 'Off'}
        </span>
      </div>
      {error && (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      )}
      {variable.defaultValue && (
        <p className="mt-1 text-xs text-gray-400">
          Default: {variable.defaultValue}
        </p>
      )}
    </div>
  );
}
