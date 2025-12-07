import { useState } from 'react';
import { type VariableInputProps, getVariableLabel } from '../VariableInput';
import SqlServerConnectionBuilder from '../builders/SqlServerConnectionBuilder';

/**
 * Connection string input with optional builder dialog.
 */
export default function ConnectionStringInput({ variable, value, onChange, error, disabled }: VariableInputProps) {
  const label = getVariableLabel(variable);
  const [showBuilder, setShowBuilder] = useState(false);
  const type = variable.type || 'ConnectionString';

  // Determine which builder to show
  const hasBuilder = type !== 'ConnectionString'; // Generic has no builder
  const builderComponent = (() => {
    switch (type) {
      case 'SqlServerConnectionString':
        return (
          <SqlServerConnectionBuilder
            isOpen={showBuilder}
            onClose={() => setShowBuilder(false)}
            value={value}
            onChange={onChange}
            variableName={variable.name}
          />
        );
      // TODO: Add other builders
      case 'PostgresConnectionString':
      case 'MySqlConnectionString':
      case 'EventStoreConnectionString':
      case 'MongoConnectionString':
      case 'RedisConnectionString':
        // Placeholder - these will be implemented later
        return null;
      default:
        return null;
    }
  })();

  const getTypeLabel = () => {
    switch (type) {
      case 'SqlServerConnectionString':
        return 'SQL Server';
      case 'PostgresConnectionString':
        return 'PostgreSQL';
      case 'MySqlConnectionString':
        return 'MySQL';
      case 'EventStoreConnectionString':
        return 'EventStoreDB';
      case 'MongoConnectionString':
        return 'MongoDB';
      case 'RedisConnectionString':
        return 'Redis';
      default:
        return 'Connection String';
    }
  };

  return (
    <div>
      <label className="block mb-1 text-sm text-gray-600 dark:text-gray-400">
        {label}
        {variable.isRequired && <span className="text-red-500 ml-1">*</span>}
        <span className="ml-2 text-xs px-1.5 py-0.5 bg-gray-100 dark:bg-gray-700 rounded">
          {getTypeLabel()}
        </span>
      </label>
      {variable.description && (
        <p className="text-xs text-gray-500 dark:text-gray-500 mb-1">{variable.description}</p>
      )}
      <div className="flex gap-2">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          className={`flex-1 px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:text-white font-mono
            ${error
              ? 'border-red-500 dark:border-red-500'
              : 'border-gray-300 dark:border-gray-600'
            }
            ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
          `}
          placeholder={variable.placeholder || variable.defaultValue || 'Enter connection string'}
        />
        {hasBuilder && (
          <button
            type="button"
            onClick={() => setShowBuilder(true)}
            disabled={disabled}
            className="px-3 py-2 text-sm font-medium text-gray-700 bg-gray-100 border border-gray-300 rounded-lg hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-600 disabled:opacity-50"
            title="Open connection builder"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </button>
        )}
      </div>
      {error && (
        <p className="mt-1 text-xs text-red-500">{error}</p>
      )}

      {/* Builder dialog */}
      {builderComponent}
    </div>
  );
}
