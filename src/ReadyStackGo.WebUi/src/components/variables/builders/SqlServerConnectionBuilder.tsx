import { useState, useEffect } from 'react';
import { apiPost } from '../../../api/client';

interface SqlServerConnectionBuilderProps {
  isOpen: boolean;
  onClose: () => void;
  value: string;
  onChange: (value: string) => void;
  variableName: string;
}

interface ConnectionParams {
  server: string;
  database: string;
  userId: string;
  password: string;
  port: string;
  trustServerCertificate: boolean;
  multipleActiveResultSets: boolean;
  encrypt: boolean;
  connectionTimeout: string;
  integratedSecurity: boolean;
}

interface TestConnectionResponse {
  success: boolean;
  message: string;
  serverVersion?: string;
}

/**
 * SQL Server connection string builder with individual field inputs.
 */
export default function SqlServerConnectionBuilder({
  isOpen,
  onClose,
  value,
  onChange,
  variableName,
}: SqlServerConnectionBuilderProps) {
  const [params, setParams] = useState<ConnectionParams>({
    server: '',
    database: '',
    userId: '',
    password: '',
    port: '1433',
    trustServerCertificate: true,
    multipleActiveResultSets: false,
    encrypt: true,
    connectionTimeout: '30',
    integratedSecurity: false,
  });
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<TestConnectionResponse | null>(null);

  // Parse existing connection string when dialog opens
  useEffect(() => {
    if (isOpen && value) {
      parseConnectionString(value);
    }
  }, [isOpen, value]);

  const parseConnectionString = (connStr: string) => {
    const newParams: ConnectionParams = {
      server: '',
      database: '',
      userId: '',
      password: '',
      port: '1433',
      trustServerCertificate: true,
      multipleActiveResultSets: false,
      encrypt: true,
      connectionTimeout: '30',
      integratedSecurity: false,
    };

    // Split by semicolon and parse key-value pairs
    const parts = connStr.split(';');
    parts.forEach((part) => {
      const [key, ...valueParts] = part.split('=');
      const val = valueParts.join('=').trim();
      const keyLower = key?.toLowerCase().trim();

      if (!keyLower) return;

      switch (keyLower) {
        case 'server':
        case 'data source':
          // Handle server,port format
          if (val.includes(',')) {
            const [srv, prt] = val.split(',');
            newParams.server = srv.trim();
            newParams.port = prt.trim();
          } else {
            newParams.server = val;
          }
          break;
        case 'database':
        case 'initial catalog':
          newParams.database = val;
          break;
        case 'user id':
        case 'uid':
          newParams.userId = val;
          break;
        case 'password':
        case 'pwd':
          newParams.password = val;
          break;
        case 'trustservercertificate':
          newParams.trustServerCertificate = val.toLowerCase() === 'true';
          break;
        case 'multipleactiveresultsets':
          newParams.multipleActiveResultSets = val.toLowerCase() === 'true';
          break;
        case 'encrypt':
          newParams.encrypt = val.toLowerCase() === 'true';
          break;
        case 'connection timeout':
        case 'connect timeout':
          newParams.connectionTimeout = val;
          break;
        case 'integrated security':
          newParams.integratedSecurity = val.toLowerCase() === 'true' || val.toLowerCase() === 'sspi';
          break;
      }
    });

    setParams(newParams);
  };

  const buildConnectionString = (): string => {
    const parts: string[] = [];

    // Server (with optional port)
    if (params.server) {
      const serverPart = params.port && params.port !== '1433'
        ? `${params.server},${params.port}`
        : params.server;
      parts.push(`Server=${serverPart}`);
    }

    // Database
    if (params.database) {
      parts.push(`Database=${params.database}`);
    }

    // Authentication
    if (params.integratedSecurity) {
      parts.push('Integrated Security=true');
    } else {
      if (params.userId) parts.push(`User Id=${params.userId}`);
      if (params.password) parts.push(`Password=${params.password}`);
    }

    // Options
    parts.push(`TrustServerCertificate=${params.trustServerCertificate}`);

    if (params.multipleActiveResultSets) {
      parts.push('MultipleActiveResultSets=true');
    }

    if (!params.encrypt) {
      parts.push('Encrypt=false');
    }

    if (params.connectionTimeout && params.connectionTimeout !== '30') {
      parts.push(`Connection Timeout=${params.connectionTimeout}`);
    }

    return parts.join(';');
  };

  const handleApply = () => {
    const connStr = buildConnectionString();
    onChange(connStr);
    onClose();
  };

  const handleTestConnection = async () => {
    setIsTesting(true);
    setTestResult(null);

    try {
      const connectionString = buildConnectionString();
      const result = await apiPost<TestConnectionResponse>('/api/connections/test/sqlserver', {
        connectionString,
      });
      setTestResult(result);
    } catch (err) {
      setTestResult({
        success: false,
        message: err instanceof Error ? err.message : 'Connection test failed',
      });
    } finally {
      setIsTesting(false);
    }
  };

  const updateParam = <K extends keyof ConnectionParams>(key: K, val: ConnectionParams[K]) => {
    setParams((prev) => ({ ...prev, [key]: val }));
    setTestResult(null); // Clear test result when params change
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-99999 flex items-center justify-center bg-black bg-opacity-50">
      <div className="w-full max-w-lg bg-white rounded-lg shadow-xl dark:bg-gray-800 max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            SQL Server Connection Builder
          </h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Content */}
        <div className="px-6 py-4 overflow-y-auto max-h-[60vh] space-y-4">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Building connection string for: <span className="font-medium">{variableName}</span>
          </p>

          {/* Server */}
          <div className="grid grid-cols-4 gap-3">
            <div className="col-span-3">
              <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
                Server <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={params.server}
                onChange={(e) => updateParam('server', e.target.value)}
                className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                placeholder="localhost or server-name"
              />
            </div>
            <div>
              <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
                Port
              </label>
              <input
                type="text"
                value={params.port}
                onChange={(e) => updateParam('port', e.target.value)}
                className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                placeholder="1433"
              />
            </div>
          </div>

          {/* Database */}
          <div>
            <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
              Database <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={params.database}
              onChange={(e) => updateParam('database', e.target.value)}
              className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="DatabaseName"
            />
          </div>

          {/* Authentication */}
          <div className="p-3 bg-gray-50 dark:bg-gray-700/50 rounded-lg space-y-3">
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="integratedSecurity"
                checked={params.integratedSecurity}
                onChange={(e) => updateParam('integratedSecurity', e.target.checked)}
                className="rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <label htmlFor="integratedSecurity" className="text-sm text-gray-700 dark:text-gray-300">
                Use Windows Authentication (Integrated Security)
              </label>
            </div>

            {!params.integratedSecurity && (
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
                    User ID
                  </label>
                  <input
                    type="text"
                    value={params.userId}
                    onChange={(e) => updateParam('userId', e.target.value)}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                    placeholder="sa"
                  />
                </div>
                <div>
                  <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
                    Password
                  </label>
                  <input
                    type="password"
                    value={params.password}
                    onChange={(e) => updateParam('password', e.target.value)}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                    placeholder="********"
                  />
                </div>
              </div>
            )}
          </div>

          {/* Options */}
          <div className="space-y-2">
            <p className="text-sm font-medium text-gray-700 dark:text-gray-300">Options</p>
            <div className="grid grid-cols-2 gap-2">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={params.trustServerCertificate}
                  onChange={(e) => updateParam('trustServerCertificate', e.target.checked)}
                  className="rounded border-gray-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-sm text-gray-600 dark:text-gray-400">Trust Server Certificate</span>
              </label>
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={params.encrypt}
                  onChange={(e) => updateParam('encrypt', e.target.checked)}
                  className="rounded border-gray-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-sm text-gray-600 dark:text-gray-400">Encrypt Connection</span>
              </label>
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={params.multipleActiveResultSets}
                  onChange={(e) => updateParam('multipleActiveResultSets', e.target.checked)}
                  className="rounded border-gray-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-sm text-gray-600 dark:text-gray-400">Multiple Active Result Sets</span>
              </label>
            </div>
          </div>

          {/* Connection Timeout */}
          <div>
            <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
              Connection Timeout (seconds)
            </label>
            <input
              type="number"
              value={params.connectionTimeout}
              onChange={(e) => updateParam('connectionTimeout', e.target.value)}
              className="w-32 px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              min="0"
              max="300"
            />
          </div>

          {/* Preview */}
          <div>
            <label className="block mb-1 text-sm font-medium text-gray-700 dark:text-gray-300">
              Connection String Preview
            </label>
            <div className="p-2 bg-gray-100 dark:bg-gray-700 rounded-lg">
              <code className="text-xs text-gray-800 dark:text-gray-200 break-all">
                {buildConnectionString() || '(empty)'}
              </code>
            </div>
          </div>

          {/* Test Result */}
          {testResult && (
            <div
              className={`p-3 rounded-lg text-sm ${
                testResult.success
                  ? 'bg-green-100 dark:bg-green-900/30 text-green-800 dark:text-green-300'
                  : 'bg-red-100 dark:bg-red-900/30 text-red-800 dark:text-red-300'
              }`}
            >
              <p className="font-medium">{testResult.success ? 'Connection successful!' : 'Connection failed'}</p>
              <p className="text-xs mt-1">{testResult.message}</p>
              {testResult.serverVersion && (
                <p className="text-xs mt-1">Server version: {testResult.serverVersion}</p>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex justify-between px-6 py-4 border-t border-gray-200 dark:border-gray-700">
          <button
            onClick={handleTestConnection}
            disabled={isTesting || !params.server || !params.database}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50"
          >
            {isTesting ? (
              <span className="flex items-center gap-2">
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                </svg>
                Testing...
              </span>
            ) : (
              'Test Connection'
            )}
          </button>
          <div className="flex gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Cancel
            </button>
            <button
              onClick={handleApply}
              disabled={!params.server || !params.database}
              className="px-4 py-2 text-sm font-medium text-white bg-brand-600 rounded-lg hover:bg-brand-700 disabled:opacity-50"
            >
              Apply
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
