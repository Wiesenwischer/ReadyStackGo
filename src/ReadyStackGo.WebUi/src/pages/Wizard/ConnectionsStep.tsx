import { useState, type FormEvent } from 'react';

interface ConnectionsStepProps {
  onNext: (data: { transport: string; persistence: string; eventStore?: string }) => Promise<void>;
  onBack?: () => void;
}

export default function ConnectionsStep({ onNext }: ConnectionsStepProps) {
  const [transport, setTransport] = useState('');
  const [persistence, setPersistence] = useState('');
  const [eventStore, setEventStore] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    // Validation
    if (!transport.trim()) {
      setError('Transport connection string is required');
      return;
    }

    if (!persistence.trim()) {
      setError('Persistence connection string is required');
      return;
    }

    setIsLoading(true);
    try {
      await onNext({
        transport: transport.trim(),
        persistence: persistence.trim(),
        eventStore: eventStore.trim() || undefined,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to set connections');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Configure Connections
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Set up global connection strings for your services (Simple Mode)
        </p>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="space-y-5">
          {error && (
            <div className="p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
              {error}
            </div>
          )}

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Transport Connection <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={transport}
              onChange={(e) => setTransport(e.target.value)}
              placeholder="amqp://rabbitmq:5672"
              required
              className="w-full h-12.5 px-4 py-3 text-sm font-mono bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              RabbitMQ or other message broker connection string
            </p>
          </div>

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Persistence Connection <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={persistence}
              onChange={(e) => setPersistence(e.target.value)}
              placeholder="Host=postgres;Database=readystackgo;Username=admin;Password=secret"
              required
              className="w-full h-12.5 px-4 py-3 text-sm font-mono bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              PostgreSQL, MySQL, or other database connection string
            </p>
          </div>

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Event Store Connection <span className="text-gray-400 text-xs">(Optional)</span>
            </label>
            <input
              type="text"
              value={eventStore}
              onChange={(e) => setEventStore(e.target.value)}
              placeholder="esdb://eventstore:2113"
              className="w-full h-12.5 px-4 py-3 text-sm font-mono bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              EventStore connection string (if using event sourcing)
            </p>
          </div>

          <div className="p-4 border border-blue-200 rounded-lg bg-blue-50 dark:bg-blue-900/20 dark:border-blue-800">
            <div className="flex gap-3">
              <svg className="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <div>
                <p className="text-sm font-medium text-blue-800 dark:text-blue-300">
                  Simple Connection Mode
                </p>
                <p className="mt-1 text-xs text-blue-700 dark:text-blue-400">
                  These connection strings will be used globally by all services. You can switch to Advanced Mode later for per-service configuration.
                </p>
              </div>
            </div>
          </div>

          <div className="pt-4">
            <button
              type="submit"
              disabled={isLoading}
              className="inline-flex items-center justify-center w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed px-7"
            >
              {isLoading ? 'Saving...' : 'Continue'}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}
