import { type VariableInputProps, getVariableLabel } from '../VariableInput';

/**
 * Dropdown select for Select type variables.
 */
export default function SelectInput({ variable, value, onChange, error, disabled }: VariableInputProps) {
  const label = getVariableLabel(variable);
  const options = variable.options || [];

  return (
    <div>
      <label className="block mb-1 text-sm text-gray-600 dark:text-gray-400">
        {label}
        {variable.isRequired && <span className="text-red-500 ml-1">*</span>}
        {variable.defaultValue && (
          <span className="ml-2 text-xs text-gray-500">
            (default: {variable.defaultValue})
          </span>
        )}
      </label>
      {variable.description && (
        <p className="text-xs text-gray-500 dark:text-gray-500 mb-1">{variable.description}</p>
      )}
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        className={`w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:text-white
          ${error
            ? 'border-red-500 dark:border-red-500'
            : 'border-gray-300 dark:border-gray-600'
          }
          ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
        `}
      >
        {!variable.isRequired && (
          <option value="">-- Select --</option>
        )}
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label || opt.value}
          </option>
        ))}
      </select>
      {error && (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      )}
      {/* Show description for selected option */}
      {value && options.find(o => o.value === value)?.description && (
        <p className="mt-1 text-xs text-gray-400">
          {options.find(o => o.value === value)?.description}
        </p>
      )}
    </div>
  );
}
