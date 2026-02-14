---
title: Stack Sources
description: Manage stack sources – catalog, import/export, and setup wizard
---

ReadyStackGo discovers deployable stacks from configurable **Stack Sources**. Sources can be local directories, Git repositories, or curated entries from the **Source Catalog**. They can be imported, exported, and configured during initial setup via the **Wizard**.

## Overview

| Feature | Description |
|---------|-------------|
| **Source Catalog** | Add curated, preconfigured Git sources with one click |
| **Import / Export** | Back up source configurations as JSON and transfer them to other instances |
| **Wizard Integration** | Select recommended sources during initial setup |
| **Manual Sources** | Add local directories or any Git repository |

---

## Step by Step: Managing Sources

### Step 1: Open Settings

Navigate to **Settings** in the main menu. You'll find the **Stack Sources** card.

![Settings page with Stack Sources card](/images/docs/sources-01-settings-nav.png)

---

### Step 2: Stack Sources Overview

Click on **Stack Sources**. You'll see the list of all configured sources with action buttons:

- **Add Source** – Add a new source
- **Export** – Export all sources as JSON
- **Import** – Import sources from a JSON file
- **Sync All** – Synchronize all active sources

![Stack Sources overview page with action buttons](/images/docs/sources-02-list-page.png)

Each source shows its type (Git / Local), status (Enabled / Disabled), and offers the actions **Sync**, **Disable/Enable**, and **Delete**.

---

## Adding a Source from the Catalog

The **Source Catalog** contains curated Git repositories that can be added with a single click.

### Step 1: Choose Source Type

Click **Add Source**. You'll see three options:

- **Local Directory** – A local directory on the server
- **Git Repository** – Any Git repository (URL + branch)
- **From Catalog** – A preconfigured source from the catalog

Select **From Catalog** and click **Continue**.

![Source type selection with three options](/images/docs/sources-03-add-type-select.png)

---

### Step 2: Browse the Catalog

You'll see the available catalog entries with name, description, and the number of included stacks. Sources that have already been added are marked with an **Already added** badge.

Click **Add** next to the desired source.

![Catalog page with available sources](/images/docs/sources-04-catalog-browse.png)

:::tip[Duplicate Detection]
ReadyStackGo detects already-added sources by their Git URL (case-insensitive, ignores trailing slashes). Duplicate sources are automatically prevented.
:::

---

### Step 3: Source Added

After adding, you'll be redirected to the sources overview. The new source appears in the list and is automatically synchronized.

![Sources list after adding a catalog source](/images/docs/sources-05-source-added.png)

---

## Import and Export

Stack source configurations can be exported and imported as JSON files. This is useful for transferring sources between instances or as a backup.

### Export

Click **Export** in the sources overview. A JSON file named `rsgo-sources-YYYY-MM-DD.json` will be downloaded.

### Import

Click **Import** and select a previously exported JSON file. The import logic:

- **New sources** are created
- **Duplicates** (same Git URL or same path) are skipped
- **Unknown types** are ignored
- Git sources without a branch default to `main`
- Local sources without a file pattern default to `*.yml;*.yaml`

![Export and Import buttons in the sources overview](/images/docs/sources-06-export-import.png)

:::note[Import Format]
The JSON file contains a version, timestamp, and an array of sources with name, type, enabled status, and type-specific fields (Git URL/branch or path/file pattern).
:::

---

## Wizard Integration

During the initial setup of ReadyStackGo, the **Setup Wizard** (step 4 of 5) offers stack source configuration. The wizard displays the available catalog entries with checkboxes:

- **Featured** sources are pre-selected
- You can select or deselect any number of sources
- Click **Add N source(s)** to add the selected sources
- Click **Skip for now** to skip this step

![Wizard step 4 – Stack Sources selection](/images/docs/sources-08-wizard-sources.png)

:::tip[Add Later]
Sources can be added, modified, or removed at any time via **Settings → Stack Sources** – the wizard step is optional.
:::

---

## Manual Sources

In addition to the catalog, you can manually add sources:

### Local Directory

A directory on the server containing stack manifest files.

| Field | Description |
|-------|-------------|
| **Name** | Display name of the source |
| **Path** | Absolute path to the directory (e.g., `/opt/stacks`) |
| **File Pattern** | Glob pattern for manifest files (default: `*.yml;*.yaml`) |

### Git Repository

A Git repository containing stack manifest files.

| Field | Description |
|-------|-------------|
| **Name** | Display name of the source |
| **Git URL** | Repository URL (e.g., `https://github.com/org/stacks.git`) |
| **Branch** | Branch name (default: `main`) |
| **SSL Verify** | SSL certificate verification (default: enabled) |

---

## Error Handling

| Situation | Behavior |
|-----------|----------|
| Git source without URL | Source is skipped during import |
| Local source without path | Source is skipped during import |
| Duplicate detected | Source is skipped (no error) |
| Unknown source type | Source is skipped |
| Sync failed | Error message in the sources overview, other sources continue to sync |
