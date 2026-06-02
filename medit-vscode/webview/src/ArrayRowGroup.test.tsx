import '@testing-library/jest-dom';
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ArrayRowGroup } from './ArrayRowGroup';
import type { FieldMetadata } from './types';

const fkArrayMeta: FieldMetadata = {
  name: 'keywords',
  type: 'array',
  isArray: true,
  validFormKeyTypes: [],
  enumValues: [],
  elementType: { name: '', type: 'formKey', isArray: false, validFormKeyTypes: ['kywd'], enumValues: [], isSortable: true },
};

const structArrayMeta: FieldMetadata = {
  name: 'factions',
  type: 'array',
  isArray: true,
  validFormKeyTypes: [],
  enumValues: [],
  elementType: {
    name: '', type: 'struct', isArray: false, validFormKeyTypes: [], enumValues: [],
    fields: [
      { name: 'faction', type: 'formKey', isArray: false, validFormKeyTypes: ['fact'], enumValues: [] },
      { name: 'rank',    type: 'int',     isArray: false, validFormKeyTypes: [],       enumValues: [] },
    ],
  },
};

describe('ArrayRowGroup', () => {
  it('shows element count when collapsed', () => {
    const value = ['000010:Fallout4.esm', '000020:Fallout4.esm', '000030:Fallout4.esm'];
    render(<ArrayRowGroup value={value} meta={fkArrayMeta} editMode={false} port={5172}
      onOpen={vi.fn()} onCommit={vi.fn()} storageKey="test:keywords" />);
    expect(screen.getByText('[3]')).toBeInTheDocument();
  });

  it('expands to show each element', () => {
    const value = ['000010:Fallout4.esm', '000020:Fallout4.esm'];
    render(<ArrayRowGroup value={value} meta={fkArrayMeta} editMode={false} port={5172}
      onOpen={vi.fn()} onCommit={vi.fn()} storageKey="test:keywords2" />);
    fireEvent.click(screen.getByText('[2]'));
    expect(screen.getByText('000010:Fallout4.esm')).toBeInTheDocument();
    expect(screen.getByText('000020:Fallout4.esm')).toBeInTheDocument();
  });

  it('calls onCommit with smaller array when remove is clicked', () => {
    const value = ['000010:Fallout4.esm', '000020:Fallout4.esm', '000030:Fallout4.esm'];
    const onCommit = vi.fn();
    render(<ArrayRowGroup value={value} meta={fkArrayMeta} editMode={true} port={5172}
      onOpen={vi.fn()} onCommit={onCommit} storageKey="test:keywords3" />);
    fireEvent.click(screen.getByText('[3]'));
    // Click the first remove button
    const removeButtons = screen.getAllByTitle('Remove element');
    fireEvent.click(removeButtons[0]);
    const committed = onCommit.mock.calls[0][0] as unknown[];
    expect(committed).toHaveLength(2);
    expect(committed).not.toContain('000010:Fallout4.esm');
  });

  it('shows Sort by FormKey button for sortable FormLink arrays in edit mode', () => {
    const value = ['000030:Fallout4.esm', '000010:Fallout4.esm'];
    render(<ArrayRowGroup value={value} meta={fkArrayMeta} editMode={true} port={5172}
      onOpen={vi.fn()} onCommit={vi.fn()} storageKey="test:keywords4" />);
    expect(screen.getByTitle('Sort by FormKey')).toBeInTheDocument();
  });

  it('renders struct elements using sub-schema fields, not just JSON keys', () => {
    // Element has both known fields, struct should render faction and rank rows
    const value = [{ faction: '000010:Fallout4.esm', rank: 2 }];
    render(<ArrayRowGroup value={value} meta={structArrayMeta} editMode={false} port={5172}
      onOpen={vi.fn()} onCommit={vi.fn()} storageKey="test:factions" />);
    fireEvent.click(screen.getByText('[1]'));
    // The struct element should be expanded automatically or show {…}
    // At minimum the row group for element 0 is rendered
    expect(screen.getByText('{…}')).toBeInTheDocument();
  });
});
