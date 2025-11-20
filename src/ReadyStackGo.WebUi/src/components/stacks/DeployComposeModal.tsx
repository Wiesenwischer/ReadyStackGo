import { useState, useRef } from 'react';
import { parseCompose, deployCompose, type EnvironmentVariableInfo } from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';

interface DeployComposeModalProps {
  isOpen: boolean;
  onClose: () => void;
  onDeploySuccess: () => void;
}

type Step = 'upload' | 'configure' | 'deploying' | 'success' | 'error';

export default function DeployComposeModal({ isOpen, onClose, onDeploySuccess }: DeployComposeModalProps) {
  const { activeEnvironment } = useEnvironment();
  const [step, setStep] = useState<Step>('upload');
  const [yamlContent, setYamlContent] = useState('');
  const [stackName, setStackName] = useState('');
  const [variables, setVariables] = useState<EnvironmentVariableInfo[]>([]);
  const [variableValues, setVariableValues] = useState<Record<string, string>>({});
  const [services, setServices] = useState<string[]>([]);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const resetModal = () => {
    setStep('upload');
    setYamlContent('');
    setStackName('');
    setVariables([]);
    setVariableValues({});
    setServices([]);
    setWarnings([]);
    setError('');
    setIsLoading(false);
  };

  const handleClose = () => {
    resetModal();
    onClose();
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (event) => {
        setYamlContent(event.target?.result as string);
      };
      reader.readAsText(file);
    }
  };

  const handleParse = async () => {
    if (!yamlContent.trim()) {
      setError('Please provide Docker Compose YAML content');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const response = await parseCompose({ yamlContent });

      if (!response.success) {
        setError(response.errors.join('\n') || response.message || 'Failed to parse compose file');
        return;
      }

      setVariables(response.variables);
      setServices(response.services);
      setWarnings(response.warnings);

      // Initialize variable values with defaults
      const initialValues: Record<string, string> = {};
      response.variables.forEach(v => {
        initialValues[v.name] = v.defaultValue || '';
      });
      setVariableValues(initialValues);

      setStep('configure');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to parse compose file');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDeploy = async () => {
    if (!stackName.trim()) {
      setError('Please provide a stack name');
      return;
    }

    if (!activeEnvironment) {
      setError('No environment selected');
      return;
    }

    // Check required variables
    const missingRequired = variables
      .filter(v => v.isRequired && !variableValues[v.name])
      .map(v => v.name);

    if (missingRequired.length > 0) {
      setError(`Missing required variables: ${missingRequired.join(', ')}`);
      return;
    }

    setIsLoading(true);
    setError('');
    setStep('deploying');

    try {
      const response = await deployCompose(activeEnvironment.id, {
        stackName,
        yamlContent,
        variables: variableValues,
      });

      if (!response.success) {
        setError(response.errors.join('\n') || response.message || 'Deployment failed');
        setStep('error');
        return;
      }

      setStep('success');
      setTimeout(() => {
        onDeploySuccess();
        handleClose();
      }, 2000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deployment failed');
      setStep('error');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="w-full max-w-2xl bg-white rounded-lg shadow-xl dark:bg-gray-800 max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
            Deploy Docker Compose Stack
          </h2>
          <button
            onClick={handleClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Content */}
        <div className="px-6 py-4 overflow-y-auto max-h-[60vh]">
          {/* Step 1: Upload */}
          {step === 'upload' && (
            <div className="space-y-4">
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Upload or paste your docker-compose.yml file to deploy it to{' '}
                <span className="font-medium">{activeEnvironment?.name || 'the selected environment'}</span>.
              </p>

              {/* File upload */}
              <div>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".yml,.yaml"
                  onChange={handleFileUpload}
                  className="hidden"
                />
                <button
                  onClick={() => fileInputRef.current?.click()}
                  className="w-full px-4 py-3 text-sm border-2 border-dashed border-gray-300 rounded-lg hover:border-brand-500 dark:border-gray-600 dark:hover:border-brand-500"
                >
                  <div className="flex flex-col items-center gap-2">
                    <svg className="w-8 h-8 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                    </svg>
                    <span className="text-gray-600 dark:text-gray-400">
                      Click to upload docker-compose.yml
                    </span>
                  </div>
                </button>
              </div>

              {/* Textarea */}
              <div>
                <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Or paste YAML content:
                </label>
                <textarea
                  value={yamlContent}
                  onChange={(e) => setYamlContent(e.target.value)}
                  rows={10}
                  className="w-full px-3 py-2 font-mono text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                  placeholder="version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - '80:80'"
                />
              </div>

              {error && (
                <div className="p-3 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
                  {error}
                </div>
              )}
            </div>
          )}

          {/* Step 2: Configure */}
          {step === 'configure' && (
            <div className="space-y-4">
              <div>
                <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Stack Name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={stackName}
                  onChange={(e) => setStackName(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  placeholder="my-stack"
                />
              </div>

              <div className="p-3 text-sm bg-gray-100 rounded-lg dark:bg-gray-700">
                <p className="font-medium text-gray-700 dark:text-gray-300">Services detected:</p>
                <p className="text-gray-600 dark:text-gray-400">{services.join(', ')}</p>
              </div>

              {warnings.length > 0 && (
                <div className="p-3 text-sm text-yellow-800 bg-yellow-100 rounded-lg dark:bg-yellow-900/30 dark:text-yellow-400">
                  <p className="font-medium">Warnings:</p>
                  <ul className="ml-4 list-disc">
                    {warnings.map((w, i) => (
                      <li key={i}>{w}</li>
                    ))}
                  </ul>
                </div>
              )}

              {variables.length > 0 && (
                <div>
                  <p className="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">
                    Environment Variables:
                  </p>
                  <div className="space-y-3">
                    {variables.map((v) => (
                      <div key={v.name}>
                        <label className="block mb-1 text-sm text-gray-600 dark:text-gray-400">
                          {v.name}
                          {v.isRequired && <span className="text-red-500 ml-1">*</span>}
                          {v.defaultValue && (
                            <span className="ml-2 text-xs text-gray-500">
                              (default: {v.defaultValue})
                            </span>
                          )}
                        </label>
                        <input
                          type="text"
                          value={variableValues[v.name] || ''}
                          onChange={(e) =>
                            setVariableValues({ ...variableValues, [v.name]: e.target.value })
                          }
                          className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                          placeholder={v.defaultValue || `Enter ${v.name}`}
                        />
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {error && (
                <div className="p-3 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
                  {error}
                </div>
              )}
            </div>
          )}

          {/* Step 3: Deploying */}
          {step === 'deploying' && (
            <div className="flex flex-col items-center py-8">
              <div className="w-12 h-12 mb-4 border-4 border-brand-600 border-t-transparent rounded-full animate-spin"></div>
              <p className="text-lg font-medium text-gray-900 dark:text-white">Deploying stack...</p>
              <p className="text-sm text-gray-600 dark:text-gray-400">This may take a few moments</p>
            </div>
          )}

          {/* Step 4: Success */}
          {step === 'success' && (
            <div className="flex flex-col items-center py-8">
              <div className="flex items-center justify-center w-12 h-12 mb-4 bg-green-100 rounded-full dark:bg-green-900/30">
                <svg className="w-6 h-6 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
              </div>
              <p className="text-lg font-medium text-gray-900 dark:text-white">Stack deployed successfully!</p>
            </div>
          )}

          {/* Step 5: Error */}
          {step === 'error' && (
            <div className="flex flex-col items-center py-8">
              <div className="flex items-center justify-center w-12 h-12 mb-4 bg-red-100 rounded-full dark:bg-red-900/30">
                <svg className="w-6 h-6 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </div>
              <p className="text-lg font-medium text-gray-900 dark:text-white">Deployment failed</p>
              <p className="mt-2 text-sm text-center text-red-600 dark:text-red-400">{error}</p>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-200 dark:border-gray-700">
          {step === 'upload' && (
            <>
              <button
                onClick={handleClose}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Cancel
              </button>
              <button
                onClick={handleParse}
                disabled={isLoading || !yamlContent.trim()}
                className="px-4 py-2 text-sm font-medium text-white bg-brand-600 rounded-lg hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isLoading ? 'Parsing...' : 'Continue'}
              </button>
            </>
          )}

          {step === 'configure' && (
            <>
              <button
                onClick={() => setStep('upload')}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Back
              </button>
              <button
                onClick={handleDeploy}
                disabled={isLoading || !stackName.trim()}
                className="px-4 py-2 text-sm font-medium text-white bg-brand-600 rounded-lg hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Deploy Stack
              </button>
            </>
          )}

          {(step === 'error') && (
            <button
              onClick={() => setStep('configure')}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Try Again
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
