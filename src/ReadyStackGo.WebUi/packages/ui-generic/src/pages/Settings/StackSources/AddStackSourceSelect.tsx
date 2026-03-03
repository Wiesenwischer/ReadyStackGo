import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { TypeSelector, type TypeOption } from "../../../components/ui/TypeSelector";

type SourceType = "local" | "git" | "catalog";

const sourceTypeOptions: TypeOption<SourceType>[] = [
  {
    id: "local",
    label: "Local Directory",
    description: "Load stacks from a folder on the server's filesystem",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
      </svg>
    ),
  },
  {
    id: "git",
    label: "Git Repository",
    description: "Clone and sync stacks from a Git repository",
    icon: (
      <svg className="w-8 h-8" viewBox="0 0 24 24" fill="currentColor">
        <path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.894 7.894l-5.5 5.5a.75.75 0 01-1.06 0l-2.5-2.5a.75.75 0 011.06-1.06l1.97 1.97 4.97-4.97a.75.75 0 011.06 1.06z"/>
        <path fillRule="evenodd" d="M12 2C6.477 2 2 6.477 2 12s4.477 10 10 10 10-4.477 10-10S17.523 2 12 2zM5.5 12a6.5 6.5 0 1113 0 6.5 6.5 0 01-13 0z" clipRule="evenodd"/>
      </svg>
    ),
  },
  {
    id: "catalog",
    label: "From Catalog",
    description: "Add a curated source from the community catalog",
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
      </svg>
    ),
  },
];

export default function AddStackSourceSelect() {
  const navigate = useNavigate();
  const [selectedType, setSelectedType] = useState<SourceType | null>(null);

  const handleContinue = () => {
    if (selectedType) {
      navigate(`/settings/stack-sources/add/${selectedType}`);
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Settings
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/stack-sources" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Stack Sources
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Add Source</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <h4 className="text-xl font-semibold text-black dark:text-white">
            Add Stack Source
          </h4>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Choose the type of source you want to add
          </p>
        </div>

        <div className="p-6">
          <TypeSelector
            options={sourceTypeOptions}
            value={selectedType}
            onChange={setSelectedType}
            columns={3}
          />

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/settings/stack-sources"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </Link>
            <button
              onClick={handleContinue}
              disabled={!selectedType}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Continue
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
