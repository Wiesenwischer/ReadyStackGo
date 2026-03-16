import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useEnvironmentStore, type CreateEnvironmentRequest, type EnvironmentType } from '@rsgo/core';

export default function AddEnvironment() {
  const navigate = useNavigate();
  const store = useEnvironmentStore();
  const [envType, setEnvType] = useState<EnvironmentType>('DockerSocket');
  const [formData, setFormData] = useState<CreateEnvironmentRequest>({
    name: "",
    type: "DockerSocket",
    socketPath: "",
  });
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [showSecret, setShowSecret] = useState(false);

  useEffect(() => {
    store.loadDefaultSocketPath();
  }, [store.loadDefaultSocketPath]);

  useEffect(() => {
    if (store.defaultSocketPath && !formData.socketPath && envType === 'DockerSocket') {
      setFormData(prev => ({ ...prev, socketPath: store.defaultSocketPath }));
    }
  }, [store.defaultSocketPath, formData.socketPath, envType]);

  const handleTypeChange = (type: EnvironmentType) => {
    setEnvType(type);
    setTestResult(null);
    if (type === 'DockerSocket') {
      setFormData({
        name: formData.name,
        type: 'DockerSocket',
        socketPath: store.defaultSocketPath || "",
      });
    } else {
      setFormData({
        name: formData.name,
        type: 'SshTunnel',
        sshHost: "",
        sshPort: 22,
        sshUsername: "",
        sshAuthMethod: "PrivateKey",
        sshSecret: "",
        remoteSocketPath: "/var/run/docker.sock",
      });
    }
  };

  const handleTestConnection = async () => {
    setTestResult(null);
    const request = envType === 'SshTunnel'
      ? {
          type: 'SshTunnel' as const,
          sshHost: formData.sshHost,
          sshPort: formData.sshPort,
          sshUsername: formData.sshUsername,
          sshAuthMethod: formData.sshAuthMethod,
          sshSecret: formData.sshSecret,
          remoteSocketPath: formData.remoteSocketPath,
        }
      : {
          type: 'DockerSocket' as const,
          dockerHost: formData.socketPath,
        };
    const result = await store.testConn(request);
    if (result) {
      setTestResult({ success: result.success, message: result.message });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    store.clearError();
    const success = await store.create(formData);
    if (success) {
      navigate("/environments");
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/environments" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Environments
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Add Environment</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-brand-100 text-brand-600 dark:bg-brand-900/30 dark:text-brand-400">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Add Environment
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Connect ReadyStackGo to a Docker daemon
              </p>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          {store.error && (
            <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
            </div>
          )}

          <div className="space-y-6 max-w-2xl">
            {/* Environment Name */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Environment Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="My Docker Server"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                A descriptive name for this Docker environment
              </p>
            </div>

            {/* Environment Type Selector */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Connection Type
              </label>
              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={() => handleTypeChange('DockerSocket')}
                  className={`flex-1 rounded-lg border-2 px-4 py-3 text-sm font-medium transition-colors ${
                    envType === 'DockerSocket'
                      ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-400 dark:bg-brand-900/20 dark:text-brand-300'
                      : 'border-gray-200 text-gray-600 hover:border-gray-300 dark:border-gray-700 dark:text-gray-400 dark:hover:border-gray-600'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2" />
                    </svg>
                    Local Docker Socket
                  </div>
                  <p className="mt-1 text-xs font-normal opacity-70">Direct connection via Unix socket</p>
                </button>
                <button
                  type="button"
                  onClick={() => handleTypeChange('SshTunnel')}
                  className={`flex-1 rounded-lg border-2 px-4 py-3 text-sm font-medium transition-colors ${
                    envType === 'SshTunnel'
                      ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-400 dark:bg-brand-900/20 dark:text-brand-300'
                      : 'border-gray-200 text-gray-600 hover:border-gray-300 dark:border-gray-700 dark:text-gray-400 dark:hover:border-gray-600'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                    </svg>
                    SSH Tunnel
                  </div>
                  <p className="mt-1 text-xs font-normal opacity-70">Remote Docker via SSH connection</p>
                </button>
              </div>
            </div>

            {/* DockerSocket fields */}
            {envType === 'DockerSocket' && (
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Docker Socket Path <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.socketPath || ''}
                  onChange={(e) => setFormData({ ...formData, socketPath: e.target.value })}
                  placeholder={store.defaultSocketPath || "Loading..."}
                  required
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Path to the Docker daemon socket (auto-detected from server)
                </p>
              </div>
            )}

            {/* SSH Tunnel fields */}
            {envType === 'SshTunnel' && (
              <>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                      SSH Host <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={formData.sshHost || ''}
                      onChange={(e) => setFormData({ ...formData, sshHost: e.target.value })}
                      placeholder="192.168.1.100"
                      required
                      className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                      SSH Port
                    </label>
                    <input
                      type="number"
                      value={formData.sshPort ?? 22}
                      onChange={(e) => setFormData({ ...formData, sshPort: parseInt(e.target.value) || 22 })}
                      min={1}
                      max={65535}
                      className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                    />
                  </div>
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    SSH Username <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={formData.sshUsername || ''}
                    onChange={(e) => setFormData({ ...formData, sshUsername: e.target.value })}
                    placeholder="root"
                    required
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Authentication Method
                  </label>
                  <div className="flex gap-3">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name="sshAuthMethod"
                        value="PrivateKey"
                        checked={formData.sshAuthMethod === 'PrivateKey'}
                        onChange={() => setFormData({ ...formData, sshAuthMethod: 'PrivateKey', sshSecret: '' })}
                        className="text-brand-600 focus:ring-brand-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-gray-300">Private Key</span>
                    </label>
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name="sshAuthMethod"
                        value="Password"
                        checked={formData.sshAuthMethod === 'Password'}
                        onChange={() => setFormData({ ...formData, sshAuthMethod: 'Password', sshSecret: '' })}
                        className="text-brand-600 focus:ring-brand-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-gray-300">Password</span>
                    </label>
                  </div>
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    {formData.sshAuthMethod === 'Password' ? 'Password' : 'Private Key'} <span className="text-red-500">*</span>
                  </label>
                  {formData.sshAuthMethod === 'Password' ? (
                    <div className="relative">
                      <input
                        type={showSecret ? "text" : "password"}
                        value={formData.sshSecret || ''}
                        onChange={(e) => setFormData({ ...formData, sshSecret: e.target.value })}
                        placeholder="SSH password"
                        required
                        className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white pr-10"
                      />
                      <button
                        type="button"
                        onClick={() => setShowSecret(!showSecret)}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                      >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          {showSecret ? (
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                          ) : (
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                          )}
                        </svg>
                      </button>
                    </div>
                  ) : (
                    <textarea
                      value={formData.sshSecret || ''}
                      onChange={(e) => setFormData({ ...formData, sshSecret: e.target.value })}
                      placeholder={"-----BEGIN OPENSSH PRIVATE KEY-----\n...\n-----END OPENSSH PRIVATE KEY-----"}
                      required
                      rows={6}
                      className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                    />
                  )}
                  <p className="mt-1 text-xs text-gray-500">
                    {formData.sshAuthMethod === 'Password'
                      ? 'SSH password for authentication'
                      : 'Paste the full private key content (PEM format)'}
                  </p>
                </div>

                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Remote Docker Socket Path
                  </label>
                  <input
                    type="text"
                    value={formData.remoteSocketPath || '/var/run/docker.sock'}
                    onChange={(e) => setFormData({ ...formData, remoteSocketPath: e.target.value })}
                    placeholder="/var/run/docker.sock"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                  <p className="mt-1 text-xs text-gray-500">
                    Path to the Docker socket on the remote server
                  </p>
                </div>
              </>
            )}

            {/* Test Connection */}
            <div className="pt-2">
              <button
                type="button"
                onClick={handleTestConnection}
                disabled={store.actionLoading === 'testing'}
                className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
                {store.actionLoading === 'testing' ? 'Testing...' : 'Test Connection'}
              </button>
              {testResult && (
                <div className={`mt-2 rounded-md p-3 text-sm ${
                  testResult.success
                    ? 'bg-green-50 text-green-800 dark:bg-green-900/20 dark:text-green-200'
                    : 'bg-red-50 text-red-800 dark:bg-red-900/20 dark:text-red-200'
                }`}>
                  {testResult.message}
                </div>
              )}
            </div>
          </div>

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/environments"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </Link>
            <button
              type="submit"
              disabled={store.actionLoading === 'creating'}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {store.actionLoading === 'creating' ? "Creating..." : "Create Environment"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
