import { describe, it, expect } from 'vitest';
import {
  buildUpgradeVariableState,
  computeSharedVariables,
  getStackSpecificVariables,
  parseEnvContent,
  withoutStoredSecretDefaults,
  withoutUntouchedSecrets,
} from './upgradeVariables';
import type { GetProductDeploymentResponse, DeploymentVariableDto } from '../api/deployments';
import type { Product, ProductStack, StackVariable } from '../api/stacks';

// ── Builders ──────────────────────────────────────────────────────────────────

function variable(name: string, over: Partial<StackVariable> = {}): StackVariable {
  return { name, isRequired: false, ...over };
}

function stack(name: string, variables: StackVariable[], id = `sid:${name}`): ProductStack {
  return { id, name, services: [], variables };
}

function product(stacks: ProductStack[]): Product {
  return {
    id: 'stacks:p:2.0.0',
    groupId: 'stacks:p',
    sourceId: 'stacks',
    sourceName: 'stacks',
    name: 'p',
    stacks,
  } as Product;
}

function deployed(name: string, over: Partial<DeploymentVariableDto> = {}): DeploymentVariableDto {
  return { name, value: null, isSecret: false, hasValue: false, ...over };
}

function deployment(
  stacks: Array<{ stackName: string; variables: DeploymentVariableDto[] }>,
  sharedVariables: DeploymentVariableDto[] = [],
): GetProductDeploymentResponse {
  return {
    sharedVariables,
    stacks: stacks.map(s => ({ ...s, variables: s.variables })),
  } as GetProductDeploymentResponse;
}

const SECRET_STORED = { isSecret: true, hasValue: true, value: null };
const SECRET_EMPTY = { isSecret: true, hasValue: false, value: null };

// ── computeSharedVariables ────────────────────────────────────────────────────

describe('computeSharedVariables', () => {
  it('returns variables that appear in two or more stacks', () => {
    const result = computeSharedVariables([
      stack('a', [variable('SHARED'), variable('ONLY_A')]),
      stack('b', [variable('SHARED'), variable('ONLY_B')]),
    ]);

    expect(result.map(v => v.name)).toEqual(['SHARED']);
  });

  it('treats a single-stack product as having no shared variables', () => {
    const result = computeSharedVariables([stack('a', [variable('X'), variable('Y')])]);

    expect(result).toEqual([]);
  });

  it('returns nothing for a product without stacks', () => {
    expect(computeSharedVariables([])).toEqual([]);
  });

  it('keeps the first stack definition of a duplicated variable', () => {
    const result = computeSharedVariables([
      stack('a', [variable('SHARED', { defaultValue: 'from-a' })]),
      stack('b', [variable('SHARED', { defaultValue: 'from-b' })]),
    ]);

    expect(result[0].defaultValue).toBe('from-a');
  });

  it('is case sensitive — the backend keys variables by exact name', () => {
    const result = computeSharedVariables([
      stack('a', [variable('SHARED')]),
      stack('b', [variable('shared')]),
    ]);

    expect(result).toEqual([]);
  });
});

// ── getStackSpecificVariables ─────────────────────────────────────────────────

describe('getStackSpecificVariables', () => {
  it('drops the variables edited at product level', () => {
    const result = getStackSpecificVariables(
      stack('a', [variable('SHARED'), variable('OWN')]), new Set(['SHARED']));

    expect(result.map(v => v.name)).toEqual(['OWN']);
  });

  it('returns every variable when nothing is shared', () => {
    const result = getStackSpecificVariables(
      stack('a', [variable('ONE'), variable('TWO')]), new Set());

    expect(result.map(v => v.name)).toEqual(['ONE', 'TWO']);
  });
});

// ── withoutStoredSecretDefaults (#472) ────────────────────────────────────────

describe('withoutStoredSecretDefaults', () => {
  it('empties a stored secret that was pre-filled with the manifest default', () => {
    const result = withoutStoredSecretDefaults(
      { PASSWORD: 'manifest-default', HOST: 'localhost' }, new Set(['PASSWORD']));

    expect(result).toEqual({ PASSWORD: '', HOST: 'localhost' });
  });

  it('leaves a secret alone when nothing is stored for it', () => {
    const result = withoutStoredSecretDefaults(
      { PASSWORD: 'manifest-default' }, new Set());

    expect(result).toEqual({ PASSWORD: 'manifest-default' });
  });

  it('keeps the key present, so the field still renders', () => {
    const result = withoutStoredSecretDefaults({ PASSWORD: 'x' }, new Set(['PASSWORD']));

    expect(Object.prototype.hasOwnProperty.call(result, 'PASSWORD')).toBe(true);
  });

  it('ignores stored secrets that have no field', () => {
    const result = withoutStoredSecretDefaults({ HOST: 'localhost' }, new Set(['GONE']));

    expect(result).toEqual({ HOST: 'localhost' });
  });

  it('returns the same object when there is nothing to clear', () => {
    const values = { HOST: 'localhost' };

    expect(withoutStoredSecretDefaults(values, new Set())).toBe(values);
  });
});

// ── withoutUntouchedSecrets (#470) ────────────────────────────────────────────

describe('withoutUntouchedSecrets', () => {
  it('omits an untouched stored secret so the backend keeps its value', () => {
    const result = withoutUntouchedSecrets(
      { PASSWORD: '', HOST: 'localhost' }, new Set(['PASSWORD']));

    expect(result).toEqual({ HOST: 'localhost' });
  });

  it('submits a retyped secret', () => {
    const result = withoutUntouchedSecrets({ PASSWORD: 'new' }, new Set(['PASSWORD']));

    expect(result).toEqual({ PASSWORD: 'new' });
  });

  it('submits an emptied non-secret — clearing a visible field is deliberate', () => {
    const result = withoutUntouchedSecrets({ HOST: '' }, new Set(['PASSWORD']));

    expect(result).toEqual({ HOST: '' });
  });

  it('returns the same object when no secret is stored', () => {
    const values = { PASSWORD: '' };

    expect(withoutUntouchedSecrets(values, new Set())).toBe(values);
  });
});

// ── parseEnvContent ───────────────────────────────────────────────────────────

describe('parseEnvContent', () => {
  it('parses plain assignments', () => {
    expect(parseEnvContent('A=1\nB=two')).toEqual({ A: '1', B: 'two' });
  });

  it('skips comments and blank lines', () => {
    expect(parseEnvContent('# comment\n\n  \nA=1')).toEqual({ A: '1' });
  });

  it('strips matching surrounding quotes', () => {
    expect(parseEnvContent('A="quoted"\nB=\'single\'')).toEqual({ A: 'quoted', B: 'single' });
  });

  it('leaves unbalanced quotes untouched', () => {
    expect(parseEnvContent('A="half')).toEqual({ A: '"half' });
  });

  it('keeps everything after the first = — connection strings contain more', () => {
    expect(parseEnvContent('DB=Server=x;Password=y')).toEqual({ DB: 'Server=x;Password=y' });
  });

  it('accepts an empty value', () => {
    expect(parseEnvContent('A=')).toEqual({ A: '' });
  });

  it('ignores lines without = and lines with an empty key', () => {
    expect(parseEnvContent('NOEQUALS\n=orphan\nA=1')).toEqual({ A: '1' });
  });

  it('returns nothing for empty content', () => {
    expect(parseEnvContent('')).toEqual({});
  });

  it('handles CRLF line endings', () => {
    expect(parseEnvContent('A=1\r\nB=2')).toEqual({ A: '1', B: '2' });
  });

  it('lets a later assignment win', () => {
    expect(parseEnvContent('A=1\nA=2')).toEqual({ A: '2' });
  });
});

// ── buildUpgradeVariableState ─────────────────────────────────────────────────

describe('buildUpgradeVariableState', () => {
  it('seeds fields from the target version defaults', () => {
    const state = buildUpgradeVariableState(
      product([stack('a', [variable('HOST', { defaultValue: 'localhost' })])]),
      deployment([]),
      null);

    expect(state.perStackVariableValues['sid:a']).toEqual({ HOST: 'localhost' });
  });

  it('overlays the value stored on the stack (#452)', () => {
    const state = buildUpgradeVariableState(
      product([stack('a', [variable('HOST', { defaultValue: 'localhost' })])]),
      deployment([{ stackName: 'a', variables: [deployed('HOST', { value: 'configured' })] }]),
      null);

    expect(state.perStackVariableValues['sid:a'].HOST).toBe('configured');
  });

  it('matches the stored stack case-insensitively', () => {
    const state = buildUpgradeVariableState(
      product([stack('Web', [variable('HOST')])]),
      deployment([{ stackName: 'web', variables: [deployed('HOST', { value: 'configured' })] }]),
      null);

    expect(state.perStackVariableValues['sid:Web'].HOST).toBe('configured');
  });

  it('does not carry a variable the target version dropped', () => {
    const state = buildUpgradeVariableState(
      product([stack('a', [variable('HOST')])]),
      deployment([{
        stackName: 'a',
        variables: [deployed('HOST', { value: 'h' }), deployed('REMOVED', { value: 'old' })],
      }]),
      null);

    expect(state.perStackVariableValues['sid:a']).toEqual({ HOST: 'h' });
  });

  it('overlays a shared variable from the deployment', () => {
    const state = buildUpgradeVariableState(
      product([
        stack('a', [variable('SHARED', { defaultValue: 'd' })]),
        stack('b', [variable('SHARED', { defaultValue: 'd' })]),
      ]),
      deployment([], [deployed('SHARED', { value: 'configured' })]),
      null);

    expect(state.sharedVariableValues).toEqual({ SHARED: 'configured' });
    expect(state.perStackVariableValues['sid:a']).toEqual({});
  });

  it('ignores a shared deployment variable the target version no longer shares', () => {
    const state = buildUpgradeVariableState(
      product([stack('a', [variable('HOST')])]),
      deployment([], [deployed('HOST', { value: 'from-shared' })]),
      null);

    expect(state.sharedVariableValues).toEqual({});
    expect(state.perStackVariableValues['sid:a'].HOST).toBe('');
  });

  it('treats a null value as empty', () => {
    const state = buildUpgradeVariableState(
      product([stack('a', [variable('HOST', { defaultValue: 'localhost' })])]),
      deployment([{ stackName: 'a', variables: [deployed('HOST', { value: null })] }]),
      null);

    expect(state.perStackVariableValues['sid:a'].HOST).toBe('');
  });

  describe('stored secrets', () => {
    it('marks a stored secret and never exposes a value for it', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('PASSWORD')])]),
        deployment([{ stackName: 'a', variables: [deployed('PASSWORD', SECRET_STORED)] }]),
        null);

      expect(state.storedSecretNames).toEqual(new Set(['PASSWORD']));
      expect(state.perStackVariableValues['sid:a'].PASSWORD).toBe('');
    });

    it('empties a stored secret that has a manifest default (#472)', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('PASSWORD', { defaultValue: 'manifest-default' })])]),
        deployment([{ stackName: 'a', variables: [deployed('PASSWORD', SECRET_STORED)] }]),
        null);

      expect(state.perStackVariableValues['sid:a'].PASSWORD).toBe('');
    });

    it('keeps the manifest default when no value is stored', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('PASSWORD', { defaultValue: 'manifest-default' })])]),
        deployment([{ stackName: 'a', variables: [deployed('PASSWORD', SECRET_EMPTY)] }]),
        null);

      expect(state.perStackVariableValues['sid:a'].PASSWORD).toBe('manifest-default');
      expect(state.storedSecretNames.has('PASSWORD')).toBe(false);
    });

    it('recognises a shared secret from its per-stack copy (#470)', () => {
      // Deployments upgraded before #470 lost the shared entry but kept the stack copy.
      const state = buildUpgradeVariableState(
        product([
          stack('a', [variable('PASSWORD', { defaultValue: 'd' })]),
          stack('b', [variable('PASSWORD', { defaultValue: 'd' })]),
        ]),
        deployment(
          [{ stackName: 'a', variables: [deployed('PASSWORD', SECRET_STORED)] }],
          []),
        null);

      expect(state.storedSecretNames.has('PASSWORD')).toBe(true);
      expect(state.sharedVariableValues.PASSWORD).toBe('');
    });

    it('marks a shared secret stored only at product level', () => {
      const state = buildUpgradeVariableState(
        product([
          stack('a', [variable('PASSWORD')]),
          stack('b', [variable('PASSWORD')]),
        ]),
        deployment([], [deployed('PASSWORD', SECRET_STORED)]),
        null);

      expect(state.storedSecretNames.has('PASSWORD')).toBe(true);
    });

    it('does not mark a stored secret the target version dropped', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('HOST')])]),
        deployment([{ stackName: 'a', variables: [deployed('GONE', SECRET_STORED)] }]),
        null);

      expect(state.storedSecretNames.size).toBe(0);
    });

    it('clears a stored secret marked by a later stack', () => {
      // storedSecretNames is completed across all stacks, so the clearing has to run afterwards.
      const state = buildUpgradeVariableState(
        product([
          stack('a', [variable('PASSWORD', { defaultValue: 'd' })]),
          stack('b', [variable('PASSWORD', { defaultValue: 'd' })]),
        ]),
        deployment([{ stackName: 'b', variables: [deployed('PASSWORD', SECRET_STORED)] }]),
        null);

      expect(state.sharedVariableValues.PASSWORD).toBe('');
    });
  });

  describe('save-value opt-out', () => {
    it('defaults to the target version transient hint', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('TOKEN', { defaultTransient: true })])]),
        deployment([]),
        null);

      expect(state.excludeFromStorage).toEqual(new Set(['TOKEN']));
    });

    it('opts out a secret the deployment holds no value for', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('PASSWORD')])]),
        deployment([{ stackName: 'a', variables: [deployed('PASSWORD', SECRET_EMPTY)] }]),
        null);

      expect(state.excludeFromStorage.has('PASSWORD')).toBe(true);
    });

    it('does not opt out a secret stored in another scope', () => {
      // Otherwise the opt-out would drop a value that is demonstrably being kept.
      const state = buildUpgradeVariableState(
        product([
          stack('a', [variable('PASSWORD')]),
          stack('b', [variable('PASSWORD')]),
        ]),
        deployment(
          [{ stackName: 'a', variables: [deployed('PASSWORD', SECRET_STORED)] }],
          [deployed('PASSWORD', SECRET_EMPTY)]),
        null);

      expect(state.excludeFromStorage.has('PASSWORD')).toBe(false);
    });

    it('keeps a transient hint for a variable that is not stored', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('TOKEN', { defaultTransient: true })])]),
        deployment([{ stackName: 'a', variables: [deployed('TOKEN', SECRET_EMPTY)] }]),
        null);

      expect(state.excludeFromStorage.has('TOKEN')).toBe(true);
    });
  });

  describe('expanded stacks', () => {
    it('expands a stack that has its own variables', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('HOST')])]), deployment([]), null);

      expect(state.expandedStacks).toEqual(new Set(['sid:a']));
    });

    it('expands a stack that is new in the target version', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [])]), deployment([]), ['A']);

      expect(state.expandedStacks.has('sid:a')).toBe(true);
    });

    it('leaves a carried-over stack without variables collapsed', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [])]), deployment([]), null);

      expect(state.expandedStacks.size).toBe(0);
    });
  });

  describe('empty and missing input', () => {
    it('handles a product without stacks', () => {
      const state = buildUpgradeVariableState(product([]), deployment([]), null);

      expect(state.sharedVars).toEqual([]);
      expect(state.perStackVariableValues).toEqual({});
      expect(state.storedSecretNames.size).toBe(0);
      expect(state.excludeFromStorage.size).toBe(0);
    });

    it('handles a deployment without shared variables', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('HOST', { defaultValue: 'd' })])]),
        { stacks: [], sharedVariables: undefined } as unknown as GetProductDeploymentResponse,
        null);

      expect(state.perStackVariableValues['sid:a'].HOST).toBe('d');
    });

    it('handles a stack whose stored variables are missing', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('HOST', { defaultValue: 'd' })])]),
        { stacks: [{ stackName: 'a' }], sharedVariables: [] } as unknown as GetProductDeploymentResponse,
        null);

      expect(state.perStackVariableValues['sid:a'].HOST).toBe('d');
    });

    it('handles a deployment stack the target version no longer has', () => {
      const state = buildUpgradeVariableState(
        product([stack('a', [variable('HOST')])]),
        deployment([{ stackName: 'removed', variables: [deployed('OLD', { value: 'x' })] }]),
        null);

      expect(Object.keys(state.perStackVariableValues)).toEqual(['sid:a']);
    });
  });
});
