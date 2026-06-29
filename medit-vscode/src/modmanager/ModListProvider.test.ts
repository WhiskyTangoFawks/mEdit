import { describe, it, expect, vi } from 'vitest';
import type { IModlistSource, Mod, ModlistEntry, Separator } from './model';

vi.mock('vscode', () => ({
  TreeItem: class {
    label: string;
    description?: string;
    tooltip?: string;
    contextValue?: string;
    iconPath?: unknown;
    collapsibleState: number;
    command?: unknown;
    checkboxState?: number;
    constructor(label: string, collapsibleState = 0) {
      this.label = label;
      this.collapsibleState = collapsibleState;
    }
  },
  TreeItemCollapsibleState: { None: 0, Collapsed: 1, Expanded: 2 },
  TreeItemCheckboxState: { Unchecked: 0, Checked: 1 },
  EventEmitter: class {
    private readonly handlers: ((e: unknown) => void)[] = [];
    get event() { return (h: (e: unknown) => void) => { this.handlers.push(h); }; }
    fire(e?: unknown) { this.handlers.forEach(h => h(e)); }
  },
  ThemeIcon: class { constructor(public id: string) {} },
}));

import { ModListProvider, CountNode, SeparatorNode, ModNode } from './ModListProvider';

const mod = (name: string, enabled = true, extra: Partial<Mod> = {}): Mod => ({
  kind: 'mod', name, enabled, ...extra,
});
const sep = (name: string, enabled = false): Separator => ({ kind: 'separator', name, enabled });

class FakeSource implements IModlistSource {
  setEnabledCalls: { modName: string; enabled: boolean }[] = [];
  activeProfile = 'Default';
  profiles = ['Default', 'Secondary'];
  constructor(public entries: ModlistEntry[], private readonly throwOnRead = false) {}
  readModlist(): Promise<ModlistEntry[]> {
    if (this.throwOnRead) return Promise.reject(new Error('boom'));
    return Promise.resolve(this.entries);
  }
  setEnabled(modName: string, enabled: boolean): Promise<void> {
    this.setEnabledCalls.push({ modName, enabled });
    return Promise.resolve();
  }
  reorder(): Promise<void> { return Promise.resolve(); }
  listProfiles(): Promise<string[]> { return Promise.resolve(this.profiles); }
  getActiveProfile(): Promise<string> { return Promise.resolve(this.activeProfile); }
  setActiveProfile(name: string): Promise<void> { this.activeProfile = name; return Promise.resolve(); }
  readPluginOrder(): Promise<string[]> { return Promise.resolve([]); }
}

describe('ModListProvider', () => {
  it('builds root children: count node, ungrouped mods, then separators', async () => {
    const source = new FakeSource([
      mod('Ungrouped A'),
      mod('Ungrouped B', false),
      sep('Section 1'),
      mod('Child'),
    ]);
    const provider = new ModListProvider(source);
    const roots = await provider.getChildren();

    expect(roots[0]).toBeInstanceOf(CountNode);
    expect(roots[0].label).toBe('2 active / 3 installed');
    expect(roots[1]).toBeInstanceOf(ModNode);
    expect(roots[1].label).toBe('Ungrouped A');
    expect(roots[2]).toBeInstanceOf(ModNode);
    expect(roots[2].label).toBe('Ungrouped B');
    expect(roots[3]).toBeInstanceOf(SeparatorNode);
    expect(roots[3].label).toBe('Section 1');
  });

  it('returns a separator’s mods as ModNodes with checkbox, version, tooltip', async () => {
    const source = new FakeSource([
      sep('Section'),
      mod('UFO4P', true, { version: 'v2.1.5', nexusId: '4598', archiveFilename: 'UFO4P.7z' }),
      mod('Disabled Mod', false),
    ]);
    const provider = new ModListProvider(source);
    const roots = await provider.getChildren();
    const separator = roots.find((n): n is SeparatorNode => n instanceof SeparatorNode)!;
    const children = await provider.getChildren(separator);

    expect(children).toHaveLength(2);
    const [enabled, disabled] = children as ModNode[];
    expect(enabled.label).toBe('UFO4P');
    expect(enabled.description).toBe('v2.1.5');
    expect(enabled.checkboxState).toBe(1); // Checked
    expect(enabled.tooltip).toBe('UFO4P · v2.1.5 · 4598 · UFO4P.7z');
    expect(disabled.checkboxState).toBe(0); // Unchecked
    expect(disabled.tooltip).toBe('Disabled Mod'); // no extra fields
  });

  it('setModEnabled delegates to the source and fires a refresh', async () => {
    const source = new FakeSource([mod('A')]);
    const provider = new ModListProvider(source);
    let fired = false;
    provider.onDidChangeTreeData(() => { fired = true; });

    await provider.setModEnabled('A', false);

    expect(source.setEnabledCalls).toEqual([{ modName: 'A', enabled: false }]);
    expect(fired).toBe(true);
  });

  it('switchProfile persists the selection and fires a refresh', async () => {
    const source = new FakeSource([mod('A')]);
    const provider = new ModListProvider(source);
    let fired = false;
    provider.onDidChangeTreeData(() => { fired = true; });

    await provider.switchProfile('Secondary');

    expect(source.activeProfile).toBe('Secondary');
    expect(fired).toBe(true);
  });

  it('returns no children and does not throw when the source read fails', async () => {
    const logs: string[] = [];
    const source = new FakeSource([], /* throwOnRead */ true);
    const provider = new ModListProvider(source, (m) => logs.push(m));

    const roots = await provider.getChildren();

    expect(roots).toEqual([]);
    expect(logs.some((l) => l.includes('boom'))).toBe(true);
  });
});
