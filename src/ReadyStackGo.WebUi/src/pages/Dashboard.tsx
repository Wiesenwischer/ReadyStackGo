export default function Dashboard() {
  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Dashboard
        </h1>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 xl:grid-cols-4 2xl:gap-7.5">
        {/* Placeholder for dashboard widgets */}
        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                0
              </h4>
              <span className="text-sm font-medium">Total Containers</span>
            </div>
          </div>
        </div>

        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                0
              </h4>
              <span className="text-sm font-medium">Running Containers</span>
            </div>
          </div>
        </div>

        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                0
              </h4>
              <span className="text-sm font-medium">Active Stacks</span>
            </div>
          </div>
        </div>

        <div className="rounded-sm border border-stroke bg-white px-7.5 py-6 shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="flex items-end justify-between">
            <div>
              <h4 className="text-title-md font-bold text-black dark:text-white">
                Ready
              </h4>
              <span className="text-sm font-medium">System Status</span>
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
            <div className="flex flex-col">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                No containers configured yet. Use the Container Management page to add your first container.
              </p>
            </div>
          </div>
        </div>

        <div className="col-span-12 xl:col-span-4">
          <div className="rounded-sm border border-stroke bg-white px-5 pb-5 pt-7.5 shadow-default dark:border-strokedark dark:bg-boxdark sm:px-7.5">
            <div className="mb-3">
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Recent Activity
              </h4>
            </div>
            <div className="flex flex-col">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                No recent activity to display.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
