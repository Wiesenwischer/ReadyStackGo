import { type VariableInputProps, getVariableLabel } from '../VariableInput';

/**
 * Get the appropriate HTML5 input type based on variable type.
 */
function getInputType(variableType?: string): string {
  switch (variableType) {
    case 'Url':
      return 'url';
    case 'Email':
      return 'email';
    default:
      return 'text';
  }
}

/**
 * Text input for String type variables.
 * Supports pattern validation display, and special types like Url, Email, Path, MultiLine.
 */
export default function StringInput({ variable, value, onChange, error, disabled }: VariableInputProps) {
  const label = getVariableLabel(variable);
  const isMultiLine = variable.type === 'MultiLine';
  const inputType = getInputType(variable.type);

  const baseClasses = `w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:text-white
    ${error
      ? 'border-red-500 dark:border-red-500'
      : 'border-gray-300 dark:border-gray-600'
    }
    ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
  `;

  return (
    <div>
      <label className="block mb-1 text-sm text-gray-600 dark:text-gray-400">
        {label}
        {variable.isRequired && <span className="text-red-500 ml-1">*</span>}
        {variable.defaultValue && (
          <span className="ml-2 text-xs text-gray-500">
            (default: {variable.defaultValue.length > 30 ? variable.defaultValue.slice(0, 30) + '...' : variable.defaultValue})
          </span>
        )}
      </label>
      {variable.description && (
        <p className="text-xs text-gray-500 dark:text-gray-500 mb-1">{variable.description}</p>
      )}
      {isMultiLine ? (
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          rows={5}
          className={baseClasses}
          placeholder={variable.placeholder || variable.defaultValue || `Enter ${variable.name}`}
        />
      ) : (
        <input
          type={inputType}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          className={baseClasses}
          placeholder={variable.placeholder || variable.defaultValue || `Enter ${variable.name}`}
        />
      )}
      {error && (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      )}
      {variable.pattern && !error && (
        <p className="mt-1 text-xs text-gray-400">
          {variable.patternError || `Pattern: ${variable.pattern}`}
        </p>
      )}
    </div>
  );
}
