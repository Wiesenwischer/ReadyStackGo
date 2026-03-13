import { useEffect, useState, useMemo } from "react";
import { Link } from "react-router-dom";

interface LicenseEntry {
  name: string;
  version: string;
  license: string;
  repository?: string;
  author?: string;
  category: "npm-webui" | "npm-publicweb" | "dotnet";
}

interface NpmLicenseData {
  [key: string]: {
    licenses: string;
    repository?: string;
    publisher?: string;
    url?: string;
  };
}

interface DotnetLicenseEntry {
  PackageId: string;
  PackageVersion: string;
  License?: string;
  PackageProjectUrl?: string;
  Authors?: string;
}

const categoryLabels: Record<LicenseEntry["category"], string> = {
  "npm-webui": "npm (WebUI)",
  "npm-publicweb": "npm (PublicWeb)",
  dotnet: ".NET (NuGet)",
};

const categoryColors: Record<LicenseEntry["category"], string> = {
  "npm-webui": "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400",
  "npm-publicweb": "bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-400",
  dotnet: "bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400",
};

function parseNpmLicenses(data: NpmLicenseData, category: LicenseEntry["category"]): LicenseEntry[] {
  return Object.entries(data).map(([key, value]) => {
    const atIndex = key.lastIndexOf("@");
    const name = atIndex > 0 ? key.substring(0, atIndex) : key;
    const version = atIndex > 0 ? key.substring(atIndex + 1) : "";
    return {
      name,
      version,
      license: value.licenses ?? "Unknown",
      repository: value.repository,
      author: value.publisher,
      category,
    };
  });
}

function parseDotnetLicenses(data: DotnetLicenseEntry[]): LicenseEntry[] {
  return data.map((entry) => ({
    name: entry.PackageId,
    version: entry.PackageVersion,
    license: entry.License ?? "Unknown",
    repository: entry.PackageProjectUrl,
    author: entry.Authors,
    category: "dotnet",
  }));
}

export default function Licenses() {
  const [entries, setEntries] = useState<LicenseEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [licenseFilter, setLicenseFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");

  useEffect(() => {
    const loadLicenses = async () => {
      const allEntries: LicenseEntry[] = [];

      const npmSources: { url: string; category: LicenseEntry["category"] }[] = [
        { url: "/licenses/npm-webui-licenses.json", category: "npm-webui" },
        { url: "/licenses/npm-publicweb-licenses.json", category: "npm-publicweb" },
      ];

      for (const source of npmSources) {
        try {
          const res = await fetch(source.url);
          if (res.ok) {
            const data: NpmLicenseData = await res.json();
            allEntries.push(...parseNpmLicenses(data, source.category));
          }
        } catch {
          // Silently skip unavailable license files
        }
      }

      try {
        const res = await fetch("/licenses/dotnet-licenses.json");
        if (res.ok) {
          const data: DotnetLicenseEntry[] = await res.json();
          allEntries.push(...parseDotnetLicenses(data));
        }
      } catch {
        // Silently skip unavailable license file
      }

      allEntries.sort((a, b) => a.name.localeCompare(b.name));
      setEntries(allEntries);
      setLoading(false);
    };

    loadLicenses();
  }, []);

  const licenseTypes = useMemo(
    () => [...new Set(entries.map((e) => e.license))].sort(),
    [entries]
  );

  const filtered = useMemo(() => {
    const q = search.toLowerCase();
    return entries.filter((e) => {
      if (q && !e.name.toLowerCase().includes(q) && !e.author?.toLowerCase().includes(q)) return false;
      if (licenseFilter && e.license !== licenseFilter) return false;
      if (categoryFilter && e.category !== categoryFilter) return false;
      return true;
    });
  }, [entries, search, licenseFilter, categoryFilter]);

  const categoryCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const e of entries) {
      counts[e.category] = (counts[e.category] ?? 0) + 1;
    }
    return counts;
  }, [entries]);

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <nav className="mb-6 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link to="/settings" className="hover:text-brand-600 dark:hover:text-brand-400">
          Settings
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white">Licenses</span>
      </nav>

      {/* Header */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-black dark:text-white">
          Third-Party Licenses
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Open-source packages used in ReadyStackGo
        </p>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-brand-500 border-t-transparent" />
        </div>
      ) : (
        <>
          {/* Category badges */}
          <div className="mb-6 flex flex-wrap gap-3">
            {(Object.keys(categoryLabels) as LicenseEntry["category"][]).map((cat) =>
              categoryCounts[cat] ? (
                <span
                  key={cat}
                  className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium ${categoryColors[cat]}`}
                >
                  {categoryLabels[cat]}
                  <span className="font-semibold">{categoryCounts[cat]}</span>
                </span>
              ) : null
            )}
            <span className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 px-3 py-1 text-xs font-medium text-gray-700 dark:bg-gray-800 dark:text-gray-300">
              Total
              <span className="font-semibold">{entries.length}</span>
            </span>
          </div>

          {/* Filters */}
          <div className="mb-6 flex flex-wrap gap-3">
            <input
              type="text"
              placeholder="Search packages..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-64 rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white dark:placeholder-gray-500"
            />
            <select
              value={licenseFilter}
              onChange={(e) => setLicenseFilter(e.target.value)}
              className="rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
            >
              <option value="">All licenses</option>
              {licenseTypes.map((lt) => (
                <option key={lt} value={lt}>{lt}</option>
              ))}
            </select>
            <select
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value)}
              className="rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
            >
              <option value="">All categories</option>
              {(Object.keys(categoryLabels) as LicenseEntry["category"][]).map((cat) => (
                <option key={cat} value={cat}>{categoryLabels[cat]}</option>
              ))}
            </select>
          </div>

          {/* Results count */}
          <p className="mb-4 text-sm text-gray-500 dark:text-gray-400">
            Showing {filtered.length} of {entries.length} packages
          </p>

          {/* Package table */}
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-200 dark:border-gray-700">
                    <th className="px-6 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Package</th>
                    <th className="px-6 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Version</th>
                    <th className="px-6 py-3 text-left font-medium text-gray-500 dark:text-gray-400">License</th>
                    <th className="px-6 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Category</th>
                    <th className="px-6 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Author</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
                  {filtered.map((entry, i) => (
                    <tr key={`${entry.category}-${entry.name}-${i}`} className="hover:bg-gray-50 dark:hover:bg-white/[0.02]">
                      <td className="px-6 py-3 font-medium text-gray-900 dark:text-white">
                        {entry.repository ? (
                          <a
                            href={entry.repository}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="hover:text-brand-600 dark:hover:text-brand-400 hover:underline"
                          >
                            {entry.name}
                          </a>
                        ) : (
                          entry.name
                        )}
                      </td>
                      <td className="px-6 py-3 font-mono text-xs text-gray-600 dark:text-gray-400">
                        {entry.version}
                      </td>
                      <td className="px-6 py-3">
                        <span className="inline-block rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/30 dark:text-green-400">
                          {entry.license}
                        </span>
                      </td>
                      <td className="px-6 py-3">
                        <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${categoryColors[entry.category]}`}>
                          {categoryLabels[entry.category]}
                        </span>
                      </td>
                      <td className="px-6 py-3 text-gray-600 dark:text-gray-400">
                        {entry.author ?? "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {filtered.length === 0 && (
              <div className="px-6 py-12 text-center text-sm text-gray-500 dark:text-gray-400">
                No packages found matching your filters.
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
