import type { GetProductDeploymentResponse } from '../api/deployments';
import type { Product, ProductStack, StackVariable } from '../api/stacks';

/**
 * Pure variable logic of the product upgrade form.
 *
 * Kept out of the hook so it can be unit tested: every bug this form has produced so far lived here
 * (#452, #470, #472) and none of it needs React to be exercised.
 */

/** Variables that appear in two or more stacks are edited once, at product level. */
export function computeSharedVariables(stacks: ProductStack[]): StackVariable[] {
  const varCount = new Map<string, { count: number; variable: StackVariable }>();
  for (const stack of stacks) {
    for (const v of stack.variables) {
      const existing = varCount.get(v.name);
      if (existing) {
        existing.count++;
      } else {
        varCount.set(v.name, { count: 1, variable: v });
      }
    }
  }
  return Array.from(varCount.values())
    .filter(e => e.count >= 2)
    .map(e => e.variable);
}

/** The variables of a stack that are not edited at product level. */
export function getStackSpecificVariables(
  stack: ProductStack,
  sharedNames: Set<string>,
): StackVariable[] {
  return stack.variables.filter(v => !sharedNames.has(v.name));
}

/**
 * Empties the field of every variable that already has a stored secret value.
 *
 * Fields are seeded from the target version's defaults, but for a stored secret that default is a
 * value the user never typed — submitting it would replace what is stored with the manifest default
 * on every upgrade (#472). An empty field is also what "leave empty to keep it" promises.
 */
export function withoutStoredSecretDefaults(
  values: Record<string, string>,
  storedSecretNames: Set<string>,
): Record<string, string> {
  if (storedSecretNames.size === 0) return values;

  return Object.fromEntries(
    Object.entries(values).map(([name, value]) => [name, storedSecretNames.has(name) ? '' : value]),
  );
}

/**
 * Drops variables that are stored secrets the user did not retype. Their field is empty because the
 * server withholds the value; submitting that empty string would win over the stored value when the
 * backend merges variables, silently wiping a password on upgrade.
 */
export function withoutUntouchedSecrets(
  values: Record<string, string>,
  storedSecretNames: Set<string>,
): Record<string, string> {
  if (storedSecretNames.size === 0) return values;

  return Object.fromEntries(
    Object.entries(values).filter(([name, value]) => value !== '' || !storedSecretNames.has(name)),
  );
}

/** Parses .env file content into key-value pairs. */
export function parseEnvContent(content: string): Record<string, string> {
  const result: Record<string, string> = {};
  const lines = content.split('\n');
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eqIndex = trimmed.indexOf('=');
    if (eqIndex === -1) continue;
    const key = trimmed.substring(0, eqIndex).trim();
    let value = trimmed.substring(eqIndex + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (key) {
      result[key] = value;
    }
  }
  return result;
}

/** Everything the upgrade form needs to render its variable section. */
export interface UpgradeVariableState {
  /** Variables edited once at product level. */
  sharedVars: StackVariable[];
  sharedVarNames: Set<string>;
  /** Field values at product level, keyed by variable name. */
  sharedVariableValues: Record<string, string>;
  /** Field values per stack, keyed by stack id then variable name. */
  perStackVariableValues: Record<string, Record<string, string>>;
  /**
   * Secret variables that already have a stored value. Their input stays empty: leaving it empty
   * keeps the stored value, typing a new one replaces it.
   */
  storedSecretNames: Set<string>;
  /** Variable names the user chose NOT to persist. */
  excludeFromStorage: Set<string>;
  /** Ids of the stacks whose section starts expanded. */
  expandedStacks: Set<string>;
}

/**
 * Builds the initial state of the upgrade form from the target version and the running deployment.
 *
 * Priority per field: target version default, overlaid with the value stored on the deployment.
 * Secrets are the exception — the server withholds their value, so all that is learned about them is
 * whether one is stored.
 */
export function buildUpgradeVariableState(
  product: Product,
  deployment: GetProductDeploymentResponse,
  newStacks: string[] | null | undefined,
): UpgradeVariableState {
  const sharedVars = computeSharedVariables(product.stacks);
  const sharedVarNames = new Set(sharedVars.map(v => v.name));

  // Initialize shared variable values with target defaults
  const sharedInit: Record<string, string> = {};
  for (const v of sharedVars) {
    sharedInit[v.name] = v.defaultValue || '';
  }

  // Every variable the target version still knows, shared or stack-specific. A stored secret counts
  // as stored no matter which scope holds it — a shared variable is persisted per stack as well, and
  // deployments upgraded before #470 kept only that copy.
  const targetVariableNames = new Set(
    product.stacks.flatMap(s => s.variables.map(v => v.name)));

  // Overlay with current deployment shared variables. A secret arrives without its value; all we
  // learn is whether one is stored, which is enough to satisfy validation later.
  const storedSecretNames = new Set<string>();
  for (const variable of deployment.sharedVariables ?? []) {
    if (variable.isSecret) {
      if (variable.hasValue && targetVariableNames.has(variable.name)) {
        storedSecretNames.add(variable.name);
      }
      continue;
    }

    if (!Object.prototype.hasOwnProperty.call(sharedInit, variable.name)) continue;
    sharedInit[variable.name] = variable.value ?? '';
  }

  // Initialize per-stack variable values
  const perStackInit: Record<string, Record<string, string>> = {};
  const expandedStacks = new Set<string>();

  for (const stack of product.stacks) {
    const stackVars = getStackSpecificVariables(stack, sharedVarNames);
    const varValues: Record<string, string> = {};
    let hasRequiredMissing = false;

    for (const v of stackVars) {
      varValues[v.name] = v.defaultValue || '';
      if (v.isRequired && !v.defaultValue) hasRequiredMissing = true;
    }

    // Overlay the existing stack deployment's per-stack values so the upgrade form is pre-filled
    // with what the user configured at deploy time. Without this, required per-stack variables would
    // appear empty and the upgrade would be blocked by validation even though the backend already
    // merges the stored values. Only overlay variables that exist in the target version's variable
    // set so removed variables don't leak forward.
    const existingStack = deployment.stacks.find(
      s => s.stackName.toLowerCase() === stack.name.toLowerCase()
    );
    if (existingStack?.variables) {
      const targetNames = new Set(stackVars.map(v => v.name));
      for (const variable of existingStack.variables) {
        if (variable.isSecret) {
          // Shared secrets are stored per stack too, so this is also where a shared secret is
          // recognised when the deployment's own shared entry was lost (#470).
          if (variable.hasValue && targetVariableNames.has(variable.name)) {
            storedSecretNames.add(variable.name);
          }
          continue;
        }

        if (!targetNames.has(variable.name)) continue;
        varValues[variable.name] = variable.value ?? '';
      }
    }

    perStackInit[stack.id] = varValues;

    // A required secret with a stored value is not missing, even though the field stays empty.
    if (hasRequiredMissing) {
      hasRequiredMissing = stackVars.some(
        v => v.isRequired && !varValues[v.name] && !storedSecretNames.has(v.name));
    }

    // Expand stacks that are new or have required variables missing values
    const isNew = newStacks?.some(n => n.toLowerCase() === stack.name.toLowerCase());
    if (isNew || hasRequiredMissing || stackVars.length > 0) {
      expandedStacks.add(stack.id);
    }
  }

  // Default the opt-out from the target version's hints, plus every secret the current deployment
  // holds no value for — that is the trace of a deploy-time "do not save".
  const excludeFromStorage = new Set<string>();
  for (const stack of product.stacks) {
    for (const v of stack.variables) {
      if (v.defaultTransient) excludeFromStorage.add(v.name);
    }
  }
  // A secret without a stored value anywhere was declined at deploy time. One that is stored in any
  // scope was not, so it must not land in the opt-out — that would drop it on this upgrade.
  for (const variable of deployment.sharedVariables ?? []) {
    if (variable.isSecret && !variable.hasValue) excludeFromStorage.add(variable.name);
  }
  for (const stack of deployment.stacks) {
    for (const variable of stack.variables ?? []) {
      if (variable.isSecret && !variable.hasValue) excludeFromStorage.add(variable.name);
    }
  }
  for (const name of storedSecretNames) {
    excludeFromStorage.delete(name);
  }

  return {
    sharedVars,
    sharedVarNames,
    // Applied once the full set of stored secrets is known — a name can be marked by any stack.
    sharedVariableValues: withoutStoredSecretDefaults(sharedInit, storedSecretNames),
    perStackVariableValues: Object.fromEntries(
      Object.entries(perStackInit).map(([stackId, values]) =>
        [stackId, withoutStoredSecretDefaults(values, storedSecretNames)])),
    storedSecretNames,
    excludeFromStorage,
    expandedStacks,
  };
}
