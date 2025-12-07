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
      <div className="flex items-center justify-between">
        <div>
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
          onClick={handleToggle}
          disabled={disabled}
          className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors
            ${isChecked
              ? 'bg-brand-600'
              : 'bg-gray-200 dark:bg-gray-600'
            }
            ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}
          `}
        >
          <span
            className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform
              ${isChecked ? 'translate-x-6' : 'translate-x-1'}
            `}
          />
        </button>
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
