import { useState, useRef, useEffect } from "react";

const UserDropdown = () => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-3 text-left"
        aria-label="User menu"
      >
        <span className="hidden text-right lg:block">
          <span className="block text-sm font-medium text-gray-900 dark:text-white">
            Admin User
          </span>
          <span className="block text-xs text-gray-500 dark:text-gray-400">
            Administrator
          </span>
        </span>

        <span className="flex items-center justify-center w-10 h-10 rounded-full bg-gray-200 dark:bg-gray-700">
          <svg
            width="20"
            height="20"
            viewBox="0 0 20 20"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            className="text-gray-600 dark:text-gray-300"
          >
            <path
              fillRule="evenodd"
              clipRule="evenodd"
              d="M10 3.25C7.92893 3.25 6.25 4.92893 6.25 7C6.25 9.07107 7.92893 10.75 10 10.75C12.0711 10.75 13.75 9.07107 13.75 7C13.75 4.92893 12.0711 3.25 10 3.25ZM7.75 7C7.75 5.75736 8.75736 4.75 10 4.75C11.2426 4.75 12.25 5.75736 12.25 7C12.25 8.24264 11.2426 9.25 10 9.25C8.75736 9.25 7.75 8.24264 7.75 7Z"
              fill="currentColor"
            />
            <path
              fillRule="evenodd"
              clipRule="evenodd"
              d="M10 12.25C7.37665 12.25 5.02098 13.4385 3.48223 15.3054C3.23799 15.6034 3.28431 16.0461 3.58235 16.2904C3.88039 16.5346 4.32309 16.4883 4.56733 16.1903C5.85402 14.6358 7.81348 13.75 10 13.75C12.1865 13.75 14.146 14.6358 15.4327 16.1903C15.6769 16.4883 16.1196 16.5346 16.4176 16.2904C16.7157 16.0461 16.762 15.6034 16.5178 15.3054C14.979 13.4385 12.6234 12.25 10 12.25Z"
              fill="currentColor"
            />
          </svg>
        </span>

        <svg
          className={`hidden lg:block transform transition-transform ${isOpen ? "rotate-180" : ""}`}
          width="12"
          height="8"
          viewBox="0 0 12 8"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path
            fillRule="evenodd"
            clipRule="evenodd"
            d="M0.410765 0.910765C0.736202 0.585327 1.26384 0.585327 1.58928 0.910765L6.00003 5.32151L10.4108 0.910765C10.7362 0.585327 11.2638 0.585327 11.5893 0.910765C11.9147 1.2362 11.9147 1.76384 11.5893 2.08928L6.58928 7.08928C6.26384 7.41471 5.73621 7.41471 5.41077 7.08928L0.410765 2.08928C0.0853277 1.76384 0.0853277 1.2362 0.410765 0.910765Z"
            fill="currentColor"
          />
        </svg>
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-56 rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900">
          <div className="p-1">
            <button className="flex w-full items-center gap-3.5 rounded-lg px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-800">
              <svg
                width="18"
                height="18"
                viewBox="0 0 18 18"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
              >
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M9 2.25C6.37665 2.25 4.25 4.37665 4.25 7C4.25 9.62335 6.37665 11.75 9 11.75C11.6234 11.75 13.75 9.62335 13.75 7C13.75 4.37665 11.6234 2.25 9 2.25ZM5.75 7C5.75 5.20507 7.20507 3.75 9 3.75C10.7949 3.75 12.25 5.20507 12.25 7C12.25 8.79493 10.7949 10.25 9 10.25C7.20507 10.25 5.75 8.79493 5.75 7Z"
                  fill="currentColor"
                />
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M9 13.25C6.87827 13.25 4.92798 14.1488 3.51364 15.5631C3.22075 15.856 2.74588 15.856 2.45299 15.5631C2.1601 15.2702 2.1601 14.7954 2.45299 14.5025C4.14702 12.8085 6.46238 11.75 9 11.75C11.5376 11.75 13.853 12.8085 15.547 14.5025C15.8399 14.7954 15.8399 15.2702 15.547 15.5631C15.2541 15.856 14.7793 15.856 14.4864 15.5631C13.072 14.1488 11.1217 13.25 9 13.25Z"
                  fill="currentColor"
                />
              </svg>
              Profile
            </button>
            <button className="flex w-full items-center gap-3.5 rounded-lg px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-800">
              <svg
                width="18"
                height="18"
                viewBox="0 0 18 18"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
              >
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M10.5 2.25C8.42893 2.25 6.75 3.92893 6.75 6C6.75 6.41421 6.41421 6.75 6 6.75C5.58579 6.75 5.25 6.41421 5.25 6C5.25 3.10051 7.60051 0.75 10.5 0.75H12C12.4142 0.75 12.75 1.08579 12.75 1.5C12.75 1.91421 12.4142 2.25 12 2.25H10.5Z"
                  fill="currentColor"
                />
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M7.5 15.75C9.57107 15.75 11.25 14.0711 11.25 12C11.25 11.5858 11.5858 11.25 12 11.25C12.4142 11.25 12.75 11.5858 12.75 12C12.75 14.8995 10.3995 17.25 7.5 17.25H6C5.58579 17.25 5.25 16.9142 5.25 16.5C5.25 16.0858 5.58579 15.75 6 15.75H7.5Z"
                  fill="currentColor"
                />
              </svg>
              Settings
            </button>
            <hr className="my-1 border-gray-200 dark:border-gray-800" />
            <button className="flex w-full items-center gap-3.5 rounded-lg px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/10">
              <svg
                width="18"
                height="18"
                viewBox="0 0 18 18"
                fill="none"
                xmlns="http://www.w3.org/2000/svg"
              >
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M11.4697 3.21967C11.7626 2.92678 12.2374 2.92678 12.5303 3.21967L15.5303 6.21967C15.8232 6.51256 15.8232 6.98744 15.5303 7.28033L12.5303 10.2803C12.2374 10.5732 11.7626 10.5732 11.4697 10.2803C11.1768 9.98744 11.1768 9.51256 11.4697 9.21967L13.1893 7.5H6.75C6.33579 7.5 6 7.16421 6 6.75C6 6.33579 6.33579 6 6.75 6H13.1893L11.4697 4.28033C11.1768 3.98744 11.1768 3.51256 11.4697 3.21967Z"
                  fill="currentColor"
                />
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M3.75 3C3.33579 3 3 3.33579 3 3.75V14.25C3 14.6642 3.33579 15 3.75 15H9C9.41421 15 9.75 15.3358 9.75 15.75C9.75 16.1642 9.41421 16.5 9 16.5H3.75C2.50736 16.5 1.5 15.4926 1.5 14.25V3.75C1.5 2.50736 2.50736 1.5 3.75 1.5H9C9.41421 1.5 9.75 1.83579 9.75 2.25C9.75 2.66421 9.41421 3 9 3H3.75Z"
                  fill="currentColor"
                />
              </svg>
              Logout
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default UserDropdown;
