import { useState, useEffect, useRef } from 'react';
import { useParams, useSearchParams, Link } from 'react-router';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useAuth } from '../../context/AuthContext';
import { type ConnectionState, useContainerLogsStore } from '@rsgo/core';

export default function ContainerLogs() {
  const { id: containerId } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const containerName = searchParams.get('name') || containerId || '';
  const { activeEnvironment } = useEnvironment();
  const { token } = useAuth();

  const {
    lines, loading, error, tail, setTail,
    streamEnded, connectionState, tailOptions,
  } = useContainerLogsStore(token, activeEnvironment?.id, containerId);

  const [autoScroll, setAutoScroll] = useState(true);
  const logContainerRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  // Auto-scroll detection via IntersectionObserver
  useEffect(() => {
    const sentinel = bottomRef.current;
    if (!sentinel) return;

    const observer = new IntersectionObserver(
      ([entry]) => setAutoScroll(entry.isIntersecting),
      { root: logContainerRef.current, threshold: 0.1 }
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, []);

  // Auto-scroll when new lines arrive
  useEffect(() => {
    if (autoScroll && bottomRef.current) {
      bottomRef.current.scrollIntoView({ behavior: 'auto' });
    }
  }, [lines, autoScroll]);

  const scrollToBottom = () => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const connectionIndicator = (state: ConnectionState) => {
    switch (state) {
      case 'connected': return <span className="inline-block w-2 h-2 rounded-full bg-green-400" title="Connected" />;
      case 'connecting': return <span className="inline-block w-2 h-2 rounded-full bg-yellow-400 animate-pulse" title="Connecting" />;
      case 'reconnecting': return <span className="inline-block w-2 h-2 rounded-full bg-yellow-400 animate-pulse" title="Reconnecting" />;
      case 'disconnected': return <span className="inline-block w-2 h-2 rounded-full bg-red-400" title="Disconnected" />;
    }
  };

  return (
    <div className="-m-4 md:-m-6 flex flex-col h-[calc(100vh-64px)]">
      {/* Sticky header bar */}
      <div className="sticky top-0 z-10 flex items-center justify-between px-4 py-3 md:px-6 bg-white dark:bg-gray-900 border-b border-gray-200 dark:border-gray-800">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            to="/containers"
            className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white shrink-0"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Containers
          </Link>
          <span className="text-gray-300 dark:text-gray-700 shrink-0">/</span>
          <h1 className="text-sm font-semibold text-gray-900 dark:text-white truncate" title={containerName}>
            {containerName}
          </h1>
          <div className="flex items-center gap-1.5 shrink-0">
            {connectionIndicator(connectionState)}
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {connectionState === 'connected' ? 'Live' : connectionState}
            </span>
          </div>
        </div>

        <div className="flex items-center gap-3 shrink-0">
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-gray-500 dark:text-gray-400">Lines:</span>
            {tailOptions.map(opt => (
              <button
                key={opt}
                onClick={() => setTail(opt)}
                className={`px-2 py-0.5 text-xs rounded transition-colors ${
                  tail === opt
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-100 text-gray-600 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700'
                }`}
              >
                {opt}
              </button>
            ))}
          </div>
          <span className="text-xs text-gray-400 dark:text-gray-500 tabular-nums">{lines.length} lines</span>
        </div>
      </div>

      {/* Scrollable log content */}
      <div
        ref={logContainerRef}
        className="flex-1 overflow-y-auto bg-gray-950 p-4 md:px-6 font-mono text-xs leading-5"
      >
        {loading && lines.length === 0 ? (
          <div className="text-gray-500">Loading logs...</div>
        ) : error ? (
          <div className="text-red-400">{error}</div>
        ) : lines.length === 0 ? (
          <div className="text-gray-500">No log output</div>
        ) : (
          lines.map((line, i) => (
            <div key={i} className="text-gray-300 whitespace-pre-wrap break-all hover:bg-white/5">
              {line}
            </div>
          ))
        )}

        {streamEnded && (
          <div className="mt-4 px-3 py-2 bg-yellow-900/30 border border-yellow-700/50 rounded text-yellow-400 text-xs">
            Stream ended — container may have stopped.
          </div>
        )}

        <div ref={bottomRef} />
      </div>

      {/* Scroll to bottom FAB */}
      {!autoScroll && (
        <button
          onClick={scrollToBottom}
          className="fixed bottom-6 right-6 inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs rounded-full shadow-lg z-20 transition-colors"
        >
          <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 14l-7 7m0 0l-7-7m7 7V3" />
          </svg>
          Scroll to bottom
        </button>
      )}
    </div>
  );
}
