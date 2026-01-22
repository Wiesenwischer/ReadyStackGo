import type { ReactNode } from "react";

export interface TypeOption<T extends string> {
  id: T;
  label: string;
  description: string;
  icon: ReactNode;
}

interface TypeSelectorProps<T extends string> {
  options: TypeOption<T>[];
  value: T | null;
  onChange: (value: T) => void;
  columns?: 2 | 3 | 4;
}

export function TypeSelector<T extends string>({
  options,
  value,
  onChange,
  columns = 2,
}: TypeSelectorProps<T>) {
  const gridCols = {
    2: "grid-cols-1 sm:grid-cols-2",
    3: "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3",
    4: "grid-cols-1 sm:grid-cols-2 lg:grid-cols-4",
  };

  return (
    <div className={`grid gap-4 ${gridCols[columns]}`}>
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          onClick={() => onChange(option.id)}
          className={`
            relative flex flex-col items-center p-6 rounded-xl border-2 transition-all duration-200
            hover:border-brand-500 hover:shadow-md
            focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2
            ${
              value === option.id
                ? "border-brand-500 bg-brand-50 dark:bg-brand-900/20"
                : "border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-800"
            }
          `}
        >
          {value === option.id && (
            <div className="absolute top-3 right-3">
              <svg
                className="w-5 h-5 text-brand-600"
                fill="currentColor"
                viewBox="0 0 20 20"
              >
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
          )}
          <div
            className={`
              w-16 h-16 flex items-center justify-center rounded-full mb-4
              ${
                value === option.id
                  ? "bg-brand-100 text-brand-600 dark:bg-brand-800 dark:text-brand-300"
                  : "bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400"
              }
            `}
          >
            {option.icon}
          </div>
          <h3
            className={`text-base font-semibold mb-1 ${
              value === option.id
                ? "text-brand-700 dark:text-brand-300"
                : "text-gray-900 dark:text-white"
            }`}
          >
            {option.label}
          </h3>
          <p className="text-sm text-gray-500 dark:text-gray-400 text-center">
            {option.description}
          </p>
        </button>
      ))}
    </div>
  );
}
