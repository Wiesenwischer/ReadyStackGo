import { type StackVariable } from '@rsgo/core';
import StringInput from './inputs/StringInput';
import NumberInput from './inputs/NumberInput';
import BooleanInput from './inputs/BooleanInput';
import SelectInput from './inputs/SelectInput';
import PasswordInput from './inputs/PasswordInput';
import PortInput from './inputs/PortInput';
import ConnectionStringInput from './inputs/ConnectionStringInput';

export interface VariableInputProps {
  variable: StackVariable;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  disabled?: boolean;
  /** Whether this variable will be saved (persisted). undefined = no checkbox shown. */
  saveValue?: boolean;
  /** Callback when user toggles the save checkbox. */
  onSaveValueChange?: (save: boolean) => void;
}

/**
 * Factory component that renders the appropriate input based on variable type.
 */
export default function VariableInput(props: VariableInputProps) {
  const { variable, saveValue, onSaveValueChange } = props;
  const type = variable.type || 'String';
  const inputElement = renderInput(props, type);

  if (onSaveValueChange === undefined) {
    return inputElement;
  }

  return (
    <div>
      {inputElement}
      <label className="mt-1.5 flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400 cursor-pointer">
        <input
          type="checkbox"
          checked={saveValue ?? true}
          onChange={(e) => onSaveValueChange(e.target.checked)}
          className="rounded border-gray-300 text-brand-600 focus:ring-brand-500 h-3.5 w-3.5"
        />
        Save value
        {!saveValue && (
          <span className="text-amber-600 dark:text-amber-400">— will not be stored, must be re-entered on redeploy</span>
        )}
      </label>
    </div>
  );
}

function renderInput(props: VariableInputProps, type: string) {
  switch (type) {
    case 'Number':
      return <NumberInput {...props} />;

    case 'Boolean':
      return <BooleanInput {...props} />;

    case 'Select':
      return <SelectInput {...props} />;

    case 'Password':
      return <PasswordInput {...props} />;

    case 'Port':
      return <PortInput {...props} />;

    case 'Url':
    case 'Email':
    case 'Path':
      // Use StringInput with appropriate HTML5 type validation
      return <StringInput {...props} />;

    case 'MultiLine':
      // Use StringInput which handles multiline when type is MultiLine
      return <StringInput {...props} />;

    case 'ConnectionString':
    case 'SqlServerConnectionString':
    case 'PostgresConnectionString':
    case 'MySqlConnectionString':
    case 'EventStoreConnectionString':
    case 'MongoConnectionString':
    case 'RedisConnectionString':
      return <ConnectionStringInput {...props} />;

    case 'String':
    default:
      return <StringInput {...props} />;
  }
}

/**
 * Helper to get display label for a variable.
 */
export function getVariableLabel(variable: StackVariable): string {
  return variable.label || variable.name;
}

/**
 * Helper to group variables by their group property.
 */
export function groupVariables(variables: StackVariable[]): Map<string, StackVariable[]> {
  const groups = new Map<string, StackVariable[]>();

  variables.forEach(v => {
    const groupName = v.group || 'General';
    const existing = groups.get(groupName) || [];
    existing.push(v);
    groups.set(groupName, existing);
  });

  // Sort variables within each group by order
  groups.forEach((vars, key) => {
    groups.set(key, vars.sort((a, b) => (a.order || 0) - (b.order || 0)));
  });

  return groups;
}
