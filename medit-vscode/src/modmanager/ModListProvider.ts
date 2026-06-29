import * as vscode from 'vscode';
import type { IModlistSource, Mod, Separator } from './model';
import { groupModlist, type ModlistTree } from './modlistTree';

/** Non-interactive first root item: "247 active / 312 installed". */
export class CountNode extends vscode.TreeItem {
  readonly kind = 'count' as const;
  constructor(activeCount: number, installedCount: number) {
    super(`${activeCount} active / ${installedCount} installed`, vscode.TreeItemCollapsibleState.None);
    this.contextValue = 'modCount';
  }
}

/** Collapsible separator; children are the mods that follow it in modlist.txt. */
export class SeparatorNode extends vscode.TreeItem {
  readonly kind = 'separator' as const;
  constructor(public readonly separator: Separator, public readonly mods: Mod[]) {
    super(separator.name, vscode.TreeItemCollapsibleState.Collapsed);
    this.contextValue = 'separator';
  }
}

/** A mod row with a native checkbox, version description, and tooltip. */
export class ModNode extends vscode.TreeItem {
  readonly kind = 'mod' as const;
  constructor(public readonly mod: Mod) {
    super(mod.name, vscode.TreeItemCollapsibleState.None);
    this.description = mod.version ?? '';
    this.tooltip = [mod.name, mod.version, mod.nexusId, mod.archiveFilename]
      .filter((s): s is string => !!s)
      .join(' · ');
    this.iconPath = new vscode.ThemeIcon('package');
    this.contextValue = 'mod';
    this.checkboxState = mod.enabled
      ? vscode.TreeItemCheckboxState.Checked
      : vscode.TreeItemCheckboxState.Unchecked;
  }
}

export type ModlistNode = CountNode | SeparatorNode | ModNode;

/** Sidebar Mod List (Loadout) tree over an MO2 instance's active profile. */
export class ModListProvider implements vscode.TreeDataProvider<ModlistNode> {
  private readonly _onDidChangeTreeData = new vscode.EventEmitter<ModlistNode | undefined>();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  private tree?: ModlistTree;
  private readonly log: (msg: string) => void;

  constructor(private readonly source: IModlistSource, log?: (msg: string) => void) {
    this.log = log ?? (() => {});
  }

  refresh(): void {
    this.tree = undefined;
    this._onDidChangeTreeData.fire(undefined);
  }

  getTreeItem(element: ModlistNode): vscode.TreeItem {
    return element;
  }

  async getChildren(element?: ModlistNode): Promise<ModlistNode[]> {
    if (element instanceof SeparatorNode) return element.mods.map((m) => new ModNode(m));
    if (element) return [];

    const tree = await this.load();
    if (!tree) return [];
    return [
      new CountNode(tree.activeCount, tree.installedCount),
      ...tree.ungrouped.map((m) => new ModNode(m)),
      ...tree.groups.map((g) => new SeparatorNode(g.separator, g.mods)),
    ];
  }

  /** Toggle a mod's enabled state, writing through the source, then refresh. */
  async setModEnabled(modName: string, enabled: boolean): Promise<void> {
    await this.source.setEnabled(modName, enabled);
    this.refresh();
  }

  /** Persist the active profile and refresh the tree. */
  async switchProfile(name: string): Promise<void> {
    await this.source.setActiveProfile(name);
    this.refresh();
  }

  private async load(): Promise<ModlistTree | undefined> {
    if (this.tree) return this.tree;
    try {
      this.tree = groupModlist(await this.source.readModlist());
      return this.tree;
    } catch (e) {
      this.log(`[ModListProvider] readModlist failed: ${e instanceof Error ? e.message : String(e)}`);
      return undefined;
    }
  }
}
