import { type ServiceHealthDto, getHealthStatusPresentation } from '../../api/health';

interface HealthServiceRowProps {
  service: ServiceHealthDto;
}

export default function HealthServiceRow({ service }: HealthServiceRowProps) {
  const presentation = getHealthStatusPresentation(service.status);

  return (
    <div className="flex items-center justify-between py-2 px-3 border-b border-gray-100 dark:border-gray-800 last:border-0 hover:bg-gray-50 dark:hover:bg-gray-800/30 transition-colors">
      <div className="flex items-center gap-3">
        <span
          className={`h-2 w-2 rounded-full ${
            service.status.toLowerCase() === 'healthy'
              ? 'bg-green-500'
              : service.status.toLowerCase() === 'degraded'
              ? 'bg-yellow-500'
              : service.status.toLowerCase() === 'unhealthy'
              ? 'bg-red-500'
              : 'bg-gray-400'
          }`}
        />
        <div>
          <span className="text-sm font-medium text-gray-900 dark:text-white">
            {service.name}
          </span>
          {service.containerName && (
            <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
              ({service.containerName})
            </span>
          )}
        </div>
      </div>
      <div className="flex items-center gap-4">
        {service.restartCount > 0 && (
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {service.restartCount} restart{service.restartCount !== 1 ? 's' : ''}
          </span>
        )}
        <span
          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${presentation.bgColor} ${presentation.textColor}`}
        >
          {presentation.label}
        </span>
      </div>
    </div>
  );
}
