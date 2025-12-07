import { type VariableInputProps, getVariableLabel } from '../VariableInput';

/**
 * Port number input with 1-65535 validation.
 */
export default function PortInput({ variable, value, onChange, error, disabled }: VariableInputProps) {
  const label = getVariableLabel(variable);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.value;
    // Allow empty string or numbers only
    if (newValue === '' || /^\d+$/.test(newValue)) {
      onChange(newValue);
    }
  };

  // Validate port range
  const portNumber = value ? parseInt(value, 10) : null;
  const isValidPort = portNumber === null || (portNumber >= 1 && portNumber <= 65535);

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
      <input
        type="text"
        inputMode="numeric"
        value={value}
        onChange={handleChange}
        disabled={disabled}
        className={`w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:text-white
          ${(error || !isValidPort)
            ? 'border-red-500 dark:border-red-500'
            : 'border-gray-300 dark:border-gray-600'
          }
          ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
        `}
        placeholder={variable.placeholder || variable.defaultValue || '8080'}
      />
      {error && (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      )}
      {!error && !isValidPort && (
        <p className="mt-1 text-xs text-red-500">Port must be between 1 and 65535</p>
      )}
      {!error && isValidPort && (
        <p className="mt-1 text-xs text-gray-400">Valid range: 1 - 65535</p>
      )}
    </div>
  );
}
