import { useState, useRef, useEffect } from "react";

const NotificationDropdown = () => {
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
        className="relative flex items-center justify-center w-10 h-10 text-gray-700 rounded-lg hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
        aria-label="Notifications"
      >
        <svg
          width="20"
          height="20"
          viewBox="0 0 20 20"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <path
            fillRule="evenodd"
            clipRule="evenodd"
            d="M8.75 3.5C8.75 2.53 9.53 1.75 10.5 1.75C11.47 1.75 12.25 2.53 12.25 3.5V3.76C13.58 4.28 14.55 5.52 14.7 7.01L15.07 10.76C15.15 11.67 15.54 12.53 16.17 13.2C16.52 13.57 16.48 14.16 16.09 14.49C15.87 14.67 15.59 14.77 15.31 14.77H5.69C5.41 14.77 5.13 14.67 4.91 14.49C4.52 14.16 4.48 13.57 4.83 13.2C5.46 12.53 5.85 11.67 5.93 10.76L6.3 7.01C6.45 5.52 7.42 4.28 8.75 3.76V3.5ZM10.5 3.25C10.36 3.25 10.25 3.36 10.25 3.5V3.77H10.75V3.5C10.75 3.36 10.64 3.25 10.5 3.25ZM9.25 5.25C8.42 5.25 7.72 5.83 7.54 6.63L7.17 10.38C7.06 11.53 6.56 12.62 5.75 13.48C5.68 13.56 5.69 13.68 5.76 13.75C5.78 13.77 5.81 13.77 5.84 13.77H15.16C15.19 13.77 15.22 13.77 15.24 13.75C15.31 13.68 15.32 13.56 15.25 13.48C14.44 12.62 13.94 11.53 13.83 10.38L13.46 6.63C13.28 5.83 12.58 5.25 11.75 5.25H9.25Z"
            fill="currentColor"
          />
          <path
            d="M8.25 16C8.25 15.59 8.59 15.25 9 15.25H12C12.41 15.25 12.75 15.59 12.75 16C12.75 17.52 11.52 18.75 10 18.75C8.48 18.75 7.25 17.52 7.25 16C7.25 15.59 7.59 15.25 8 15.25H8.25ZM10 17.25C10.69 17.25 11.25 16.69 11.25 16H8.75C8.75 16.69 9.31 17.25 10 17.25Z"
            fill="currentColor"
          />
        </svg>
        <span className="absolute top-0 right-0 z-1 flex h-2 w-2">
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-red-400 opacity-75"></span>
          <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500"></span>
        </span>
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-80 rounded-lg border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900">
          <div className="border-b border-gray-200 px-5 py-3 dark:border-gray-800">
            <h5 className="text-sm font-medium text-gray-900 dark:text-white">
              Notifications
            </h5>
          </div>
          <div className="p-4">
            <p className="text-sm text-gray-500 dark:text-gray-400">
              No new notifications
            </p>
          </div>
        </div>
      )}
    </div>
  );
};

export default NotificationDropdown;
