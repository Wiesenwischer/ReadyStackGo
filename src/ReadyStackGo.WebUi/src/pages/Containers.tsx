export default function Containers() {
  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Container Management
        </h1>
        <button className="inline-flex items-center justify-center gap-2.5 rounded-md bg-primary px-10 py-4 text-center font-medium text-white hover:bg-opacity-90 lg:px-8 xl:px-10">
          Add Container
        </button>
      </div>

      <div className="flex flex-col gap-10">
        <div className="rounded-sm border border-stroke bg-white shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              All Containers
            </h4>
          </div>

          <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
            <div className="col-span-3 flex items-center">
              <p className="font-medium">Container Name</p>
            </div>
            <div className="col-span-2 hidden items-center sm:flex">
              <p className="font-medium">Image</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium">Status</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium">Port</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium">Actions</p>
            </div>
          </div>

          <div className="border-t border-stroke px-4 py-4.5 dark:border-strokedark md:px-6 2xl:px-7.5">
            <p className="text-center text-sm text-gray-500 dark:text-gray-400">
              No containers configured. Click "Add Container" to get started.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
