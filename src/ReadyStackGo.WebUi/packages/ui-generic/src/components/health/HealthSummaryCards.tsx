interface HealthSummaryCardsProps {
  healthyCount: number;
  degradedCount: number;
  unhealthyCount: number;
  totalCount: number;
}

export default function HealthSummaryCards({
  healthyCount,
  degradedCount,
  unhealthyCount,
  totalCount,
}: HealthSummaryCardsProps) {
  const cards = [
    {
      label: 'Healthy',
      count: healthyCount,
      bgColor: 'bg-green-50 dark:bg-green-900/20',
      textColor: 'text-green-600 dark:text-green-400',
      labelColor: 'text-green-700 dark:text-green-300',
      dotColor: 'bg-green-500',
    },
    {
      label: 'Degraded',
      count: degradedCount,
      bgColor: 'bg-yellow-50 dark:bg-yellow-900/20',
      textColor: 'text-yellow-600 dark:text-yellow-400',
      labelColor: 'text-yellow-700 dark:text-yellow-300',
      dotColor: 'bg-yellow-500',
    },
    {
      label: 'Unhealthy',
      count: unhealthyCount,
      bgColor: 'bg-red-50 dark:bg-red-900/20',
      textColor: 'text-red-600 dark:text-red-400',
      labelColor: 'text-red-700 dark:text-red-300',
      dotColor: 'bg-red-500',
    },
    {
      label: 'Total',
      count: totalCount,
      bgColor: 'bg-gray-50 dark:bg-gray-800/50',
      textColor: 'text-gray-600 dark:text-gray-400',
      labelColor: 'text-gray-700 dark:text-gray-300',
      dotColor: 'bg-gray-500',
    },
  ];

  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
      {cards.map((card) => (
        <div
          key={card.label}
          className={`rounded-xl p-4 ${card.bgColor} transition-all hover:scale-[1.02]`}
        >
          <div className="flex items-center gap-2 mb-2">
            <span className={`h-2.5 w-2.5 rounded-full ${card.dotColor}`} />
            <span className={`text-sm font-medium ${card.labelColor}`}>
              {card.label}
            </span>
          </div>
          <div className={`text-3xl font-bold ${card.textColor}`}>
            {card.count}
          </div>
        </div>
      ))}
    </div>
  );
}
