import { type VariableInputProps, getVariableLabel } from '../VariableInput';

/**
 * Number input with min/max validation display.
 */
export default function NumberInput({ variable, value, onChange, error, disabled }: VariableInputProps) {
  const label = getVariableLabel(variable);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.value;
    // Allow empty string or valid numbers
    if (newValue === '' || !isNaN(Number(newValue))) {
      onChange(newValue);
    }
  };

  const rangeText = () => {
    if (variable.min !== undefined && variable.max !== undefined) {
      return `Range: ${variable.min} - ${variable.max}`;
    } else if (variable.min !== undefined) {
      return `Min: ${variable.min}`;
    } else if (variable.max !== undefined) {
      return `Max: ${variable.max}`;
    }
    return null;
  };

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
        type="number"
        value={value}
        onChange={handleChange}
        disabled={disabled}
        min={variable.min}
        max={variable.max}
        className={`w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:text-white
          ${error
            ? 'border-red-500 dark:border-red-500'
            : 'border-gray-300 dark:border-gray-600'
          }
          ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
        `}
        placeholder={variable.placeholder || variable.defaultValue || `Enter ${variable.name}`}
      />
      {error && (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      )}
      {!error && rangeText() && (
        <p className="mt-1 text-xs text-gray-400">{rangeText()}</p>
      )}
    </div>
  );
}
