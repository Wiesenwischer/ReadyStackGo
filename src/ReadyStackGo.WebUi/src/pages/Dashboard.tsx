import { useEffect, useState } from 'react';
import { dashboardApi, type DashboardStats } from '../api/dashboard';

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchStats();
    const interval = setInterval(fetchStats, 10000);
    return () => clearInterval(interval);
  }, []);

  const fetchStats = async () => {
    try {
      const data = await dashboardApi.getStats();
      setStats(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Dashboard
        </h1>
      </div>

      {error && (
        <div className="mb-4 rounded-sm border border-red-300 bg-red-50 px-7.5 py-4 text-red-800 dark:border-red-900 dark:bg-red-900/30 dark:text-red-300">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 xl:grid-cols-4 2xl:gap-7.5">
        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                {loading ? '...' : stats?.totalStacks ?? 0}
              </h4>
              <span className="text-sm font-medium">Total Stacks</span>
            </div>
          </div>
        </div>

        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                {loading ? '...' : stats?.deployedStacks ?? 0}
              </h4>
              <span className="text-sm font-medium">Deployed Stacks</span>
            </div>
          </div>
        </div>

        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                {loading ? '...' : stats?.totalContainers ?? 0}
              </h4>
              <span className="text-sm font-medium">Total Containers</span>
            </div>
          </div>
        </div>

        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                {loading ? '...' : stats?.runningContainers ?? 0}
              </h4>
              <span className="text-sm font-medium">Running Containers</span>
            </div>
          </div>
        </div>
      </div>

      <div className="mt-4 grid grid-cols-12 gap-4 md:mt-6 md:gap-6 2xl:mt-7.5 2xl:gap-7.5">
        <div className="col-span-12 xl:col-span-8">
          <div className="rounded-sm border border-stroke bg-white px-5 pb-5 pt-7.5 shadow-default dark:border-strokedark dark:bg-boxdark sm:px-7.5">
            <div className="mb-3 justify-between gap-4 sm:flex">
              <div>
                <h4 className="text-xl font-semibold text-black dark:text-white">
                  Container Overview
                </h4>
              </div>
            </div>
            <div className="flex flex-col gap-2">
              <div className="flex justify-between border-b border-stroke pb-2 dark:border-strokedark">
                <span className="text-sm font-medium">Running:</span>
                <span className="text-sm font-semibold text-meta-3">
                  {loading ? '...' : stats?.runningContainers ?? 0}
                </span>
              </div>
              <div className="flex justify-between border-b border-stroke pb-2 dark:border-strokedark">
                <span className="text-sm font-medium">Stopped:</span>
                <span className="text-sm font-semibold text-meta-1">
                  {loading ? '...' : stats?.stoppedContainers ?? 0}
                </span>
              </div>
              <div className="flex justify-between pt-2">
                <span className="text-sm font-medium">Total:</span>
                <span className="text-sm font-bold">
                  {loading ? '...' : stats?.totalContainers ?? 0}
                </span>
              </div>
            </div>
          </div>
        </div>

        <div className="col-span-12 xl:col-span-4">
          <div className="rounded-sm border border-stroke bg-white px-5 pb-5 pt-7.5 shadow-default dark:border-strokedark dark:bg-boxdark sm:px-7.5">
            <div className="mb-3">
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Stack Overview
              </h4>
            </div>
            <div className="flex flex-col gap-2">
              <div className="flex justify-between border-b border-stroke pb-2 dark:border-strokedark">
                <span className="text-sm font-medium">Deployed:</span>
                <span className="text-sm font-semibold text-meta-3">
                  {loading ? '...' : stats?.deployedStacks ?? 0}
                </span>
              </div>
              <div className="flex justify-between border-b border-stroke pb-2 dark:border-strokedark">
                <span className="text-sm font-medium">Not Deployed:</span>
                <span className="text-sm font-semibold text-meta-7">
                  {loading ? '...' : stats?.notDeployedStacks ?? 0}
                </span>
              </div>
              <div className="flex justify-between pt-2">
                <span className="text-sm font-medium">Total:</span>
                <span className="text-sm font-bold">
                  {loading ? '...' : stats?.totalStacks ?? 0}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
