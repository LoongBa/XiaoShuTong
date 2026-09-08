#!/usr/bin/env tsx
/**
 * gen-ts-client.ts — V4.4: DomainHostClient RPC constants + typed interfaces generator
 *
 * Reads schema.graphql from the WebApi project, extracts Query and Mutation
 * field names, return types, and argument types. Generates:
 *   - Query/Mutation operation constants
 *   - Service groupings + ServiceNames constants
 *   - Typed service interfaces with precise args and return types
 *     (schema-level type extraction, not document-level)
 *
 * Run via: npx tsx scripts/gen-ts-client.ts
 * (from the repo root)
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath, pathToFileURL } from 'url';
// 注意：@tkwf/tsclient 在 AdminWeb 的 node_modules 中（file: 链接），
// 本脚本由 tsx 从 AdminWeb 目录运行，但 tsx 从脚本目录（scripts/）解析模块。
// 因此使用动态 import 并显式指定 AdminWeb 的 node_modules 路径。
import type { GenerateGraphQLApiDocOptions } from '@tkwf/tsclient';

// ----- Paths -----

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// WebH5 node_modules 路径（@tkwf/tsclient 在此，file: 链接）
const WEBH5_NM = resolve(__dirname, '..', 'node_modules');

// From scripts/ -> WebH5 root
const WEBH5_ROOT = resolve(__dirname, '..');

// From scripts/ -> solution root
const SOLUTION_ROOT = resolve(__dirname, '..', '..', '..');

// schema.graphql 由 buildSchema.ps1 导出到 WebApi 项目（或 .TKWF/）
const SCHEMA_PATH = resolve(
  SOLUTION_ROOT,
  'src',
  'XiaoShuTong.WebApi',
  'schema.graphql',
);

const OUTPUT_PATH = resolve(
  WEBH5_ROOT,
  'src',
  'gql',
  'ts-client.g.ts',
);

// ----- Constants -----

const QUERY_PREFIXES = ['query', 'get', 'find', 'search', 'list'];
const MUTATION_PREFIXES = [
  'create',
  'update',
  'delete',
  'add',
  'remove',
  'lock',
  'unlock',
  'reset',
];
const ALL_PREFIXES = [...QUERY_PREFIXES, ...MUTATION_PREFIXES];

// Special-case fields mapped directly to a service name
const SPECIAL_SERVICES: Record<string, string> = {
  ping: 'Auth',
  logout: 'Auth',
};

// Max depth for nested type resolution to prevent explosions
const MAX_TYPE_DEPTH = 6;

// GraphQL scalar → TypeScript mapping
const SCALAR_MAP: Record<string, string> = {
  String: 'string',
  Int: 'number',
  Float: 'number',
  Boolean: 'boolean',
  ID: 'string',
  Decimal: 'number',
  Long: 'number',
  DateTime: 'string',
  LocalDate: 'string',
  Byte: 'number',
  Short: 'number',
  UnsignedByte: 'number',
  UUID: 'string',
  Url: 'string',
  Any: 'unknown',
};

// Internal/entity methods to skip when generating data interfaces
const SKIP_METHODS = new Set([
  'toDto', 'toEntity', 'applyToEntity',
]);

// Types that should not be resolved (internal C# HotChocolate types)
const SKIP_TYPES = new Set([
  'Mutation', 'Query', 'Subscription',
]);

// Custom type overrides for known types that are too complex to parse
const TYPE_OVERRIDES: Record<string, string> = {
  'Long': 'number',
  'Decimal': 'number',
  'DateTime': 'string',
  'UUID': 'string',
  'Url': 'string',
};

// Track which types we've already generated to avoid duplicates
const generatedTypes = new Set<string>();
let typeGenerationQueue: Array<{ typeName: string; depth: number }> = [];
const generatedInterfaces: Array<{ typeName: string; body: string }> = [];

// ----- Helper Types -----

type OpInfo = { field: string; type: 'query' | 'mutation' };

interface FieldArg {
  name: string;
  gqlType: string; // e.g., "Int", "MerchantUserInfoFilterInput"
  optional: boolean;
  isList: boolean;
}

interface FieldInfo {
  name: string;
  returnType: string; // e.g., "DashboardSummaryDto", "[MerchantUserInfoConnection]"
  returnOptional: boolean; // whether the return type is nullable
  isList: boolean; // whether the return type is a list
  args: FieldArg[];
}

interface TypeFieldDef {
  name: string;
  gqlType: string;
  optional: boolean;
}

// ----- Schema Parsing -----

/**
 * Extract the type definition block content for a given type name.
 * Returns the content between { and matching }.
 */
function extractTypeBlock(schema: string, typeName: string): string | null {
  // Match "type TypeName {" or "type TypeName\n{" (with optional directives between)
  const typeRegex = new RegExp(`type\\s+${escapeRegex(typeName)}\\b[^{]*\\{`);
  const match = typeRegex.exec(schema);
  if (!match) return null;

  const startIdx = match.index + match[0].length; // after `{`

  // Find matching closing `}` using brace counting
  let depth = 1;
  let endIdx = startIdx;
  while (endIdx < schema.length && depth > 0) {
    const ch = schema[endIdx];
    if (ch === '{') depth++;
    if (ch === '}') depth--;
    endIdx++;
  }
  return schema.slice(startIdx, endIdx - 1);
}

function escapeRegex(str: string): string {
  return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Extract field info (name, return type, args) from a type block.
 * Handles fields with 2-space indent, multi-line args, directives.
 */
function extractFieldInfos(schema: string, typeName: string): FieldInfo[] {
  const block = extractTypeBlock(schema, typeName);
  if (!block) return [];

  const fields: FieldInfo[] = [];
  const lines = block.split('\n');

  // We need to handle multi-line field definitions (when args span multiple lines)
  // Collect the full field definition by joining continuation lines
  let currentField = '';
  for (const line of lines) {
    if (!line.trim() || line.trim().startsWith('"')) continue;

    if (line.match(/^\s{2}\w/)) {
      // This is a top-level field definition
      currentField = line;
    } else if (currentField && line.startsWith('    ')) {
      // This is a continuation (more args or directives)
      currentField += ' ' + line.trim();
    }
  }

  // Now parse each complete field definition
  // We need to re-process: the lines join approach above is fragile.
  // Let's use a different approach: collect consecutive lines that form a field.

  interface RawFieldBlock {
    lines: string[];
  }

  const fieldBlocks: RawFieldBlock[] = [];
  let currentBlock: RawFieldBlock | null = null;

  for (const line of lines) {
    if (!line.trim() || line.trim().startsWith('"')) {
      continue;
    }

    if (line.match(/^\s{2}\w/)) {
      // Start of a new top-level field
      if (currentBlock) {
        fieldBlocks.push(currentBlock);
      }
      currentBlock = { lines: [line] };
    } else if (currentBlock && line.trim()) {
      // Continuation of current field (args or directives)
      currentBlock.lines.push(line);
    }
  }
  if (currentBlock) {
    fieldBlocks.push(currentBlock);
  }

  for (const block of fieldBlocks) {
    const joined = block.lines.map(l => l.trim()).join(' ');
    const field = parseFieldLine(joined);
    if (field) {
      fields.push(field);
    }
  }

  return fields;
}

/**
 * Parse a single field definition line into FieldInfo.
 * Expected formats:
 *   fieldName(args...): ReturnType @directives
 *   fieldName: ReturnType @directives
 */
function parseFieldLine(line: string): FieldInfo | null {
  // Strip inline comments ("""...""" can span lines, but inline "" are comments)
  // Remove GraphQL string literals / descriptions that are inline
  // Actually for our case, inline descriptions are inside """ so they end up as separate lines

  // Match: fieldName(...optional...): ReturnType
  const match = line.match(/^(\w+)\s*(\(.*?\))?\s*:\s*([^\s@]+)/);
  if (!match) return null;

  const name = match[1];
  const argsStr = match[2]; // e.g., "(first: Int, after: String)"
  const returnRaw = match[3]; // e.g., "DashboardSummaryDto" or "[MerchantUserInfo]", "Int!"

  // Skip built-in names
  if (name === 'query' || name === 'mutation') return null;

  // Parse return type
  const { baseType: returnBaseType, isList: returnIsList, optional: returnOptional } =
    parseGqlType(returnRaw);

  // Parse args
  const args = argsStr ? parseArgs(argsStr) : [];

  return {
    name,
    returnType: returnBaseType,
    returnOptional,
    isList: returnIsList,
    args,
  };
}

/**
 * Parse a GraphQL type string into its components.
 * Handles: "Int", "Int!", "[String]", "[String!]", "[MerchantUserInfo!]!", etc.
 */
function parseGqlType(raw: string): { baseType: string; isList: boolean; optional: boolean } {
  let type = raw.trim();
  let optional = true;

  // Check for non-null
  if (type.endsWith('!')) {
    optional = false;
    type = type.slice(0, -1);
  }

  // Check for list
  let isList = false;
  if (type.startsWith('[') && type.endsWith(']')) {
    isList = true;
    type = type.slice(1, -1);
    // Handle nested non-null inside list: [MerchantUserInfo!]
    if (type.endsWith('!')) {
      type = type.slice(0, -1);
    }
  }

  return { baseType: type, isList, optional };
}

/**
 * Parse argument list: "(arg1: Type!, arg2: Type, arg3: [String!])"
 * GraphQL schema args may be separated by commas, newlines, or just spaces.
 */
function parseArgs(argsStr: string): FieldArg[] {
  // Remove surrounding parentheses
  let inner = argsStr.slice(1, -1).trim();
  if (!inner) return [];

  // Strip directive calls like @cost(weight: "10") to avoid fake args
  inner = inner.replace(/@\w+(\([^)]*\))?/g, '').trim();

  const args: FieldArg[] = [];

  // Use regex to extract arg declarations: "name: Type" or "name: Type = default"
  // This handles comma-separated, newline-separated, or space-separated args.
  const argRegex = /(\w+)\s*:\s*([^\s,}]+(?:\s*[!=]\s*[^\s,]+)?)/g;
  let match: RegExpExecArray | null;
  while ((match = argRegex.exec(inner)) !== null) {
    const name = match[1];
    let typeStr = match[2].trim();

    // Strip trailing comma, paren, or brace (NOT ] which is valid in array types)
    typeStr = typeStr.replace(/[,)}]$/, '').trim();
    // Strip default value after '='
    const eqIdx = typeStr.indexOf('=');
    if (eqIdx >= 0) {
      typeStr = typeStr.slice(0, eqIdx).trim();
    }

    // Strip directives
    typeStr = typeStr.replace(/@\w+/g, '').trim();

    if (name && typeStr) {
      const { baseType, isList, optional } = parseGqlType(typeStr);
      args.push({ name, gqlType: baseType, optional, isList });
    }
  }

  return args;
}

/**
 * Parse a single argument: "name: Type!" or "name: Type"
 * Also strips directives like "@cost(weight: \"10\")"
 */
function parseSingleArg(raw: string): FieldArg | null {
  // Remove directives (anything starting with @)
  const clean = raw.replace(/@\w+(\([^)]*\))?/g, '').trim();
  const match = clean.match(/^(\w+)\s*:\s*(.+)$/);
  if (!match) return null;

  const name = match[1];
  let typeStr = match[2].trim();

  // Check for default value: "= value"
  const eqIdx = typeStr.indexOf('=');
  if (eqIdx >= 0) {
    typeStr = typeStr.slice(0, eqIdx).trim();
  }

  const { baseType, optional } = parseGqlType(typeStr);

  return { name, gqlType: baseType, optional };
}

/**
 * Quick check if a type name is a GraphQL enum in the schema.
 */
function isEnumType(schema: string, typeName: string): boolean {
  return new RegExp(`enum\\s+${escapeRegex(typeName)}\\b`).test(schema);
}

// ----- Type Resolution -----

/**
 * Resolve a GraphQL type name to its TypeScript interface string.
 * Uses caching and depth limiting.
 */
function resolveType(schema: string, typeName: string, depth: number = 0): string | null {
  if (typeName.startsWith('__')) return null;
  if (SKIP_TYPES.has(typeName)) return null;

  // Check if it's a scalar
  if (SCALAR_MAP[typeName]) return SCALAR_MAP[typeName];

  // Check override
  if (TYPE_OVERRIDES[typeName]) return TYPE_OVERRIDES[typeName];

  // Check if it's a GraphQL enum — map to string
  if (isEnumType(schema, typeName)) {
    return 'string';
  }

  // Depth limit
  if (depth >= MAX_TYPE_DEPTH) return null;

  // Enqueue for generation
  const cacheKey = `${typeName}_d${depth}`;
  if (!generatedTypes.has(cacheKey)) {
    generatedTypes.add(cacheKey);

    // De-duplicate by type name for interface generation
    // (we generate interface only once regardless of depth)
    const ifaceKey = typeName;
    if (!generatedTypes.has(`iface_${ifaceKey}`)) {
      generatedTypes.add(`iface_${ifaceKey}`);
      typeGenerationQueue.push({ typeName: ifaceKey, depth });
    }
  }

  return typeName;
}

/**
 * Extract field definitions from a type block.
 */
function extractTypeFields(schema: string, typeName: string): TypeFieldDef[] {
  // Try "type TypeName {" first
  let block = extractTypeBlock(schema, typeName);

  // If not found, try "input TypeName {" (for input types)
  if (!block) {
    const inputMatch = schema.match(new RegExp(`input\\s+${escapeRegex(typeName)}\\b[^{]*\\{`));
    if (inputMatch) {
      const startIdx = inputMatch.index! + inputMatch[0].length;
      let depth = 1;
      let endIdx = startIdx;
      while (endIdx < schema.length && depth > 0) {
        if (schema[endIdx] === '{') depth++;
        if (schema[endIdx] === '}') depth--;
        endIdx++;
      }
      block = schema.slice(startIdx, endIdx - 1);
    }
  }

  if (!block) return [];

  const lines = block.split('\n');
  const fields: TypeFieldDef[] = [];

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('"') || trimmed.startsWith('}')) continue;

    // Skip 4+-space indent lines (argument continuations)
    if (line.match(/^    /) && !line.match(/^  \w/)) continue;

    // Skip entity internal methods
    const nameMatch = trimmed.match(/^(\w+)\s*(?:\(|:)/);
    if (!nameMatch) continue;
    if (SKIP_METHODS.has(nameMatch[1])) continue;

    // Extract type: "fieldName: Type" or "fieldName(...): Type"
    const typeMatch = trimmed.match(/^\w+\s*(?:\([^)]*\))?\s*:\s*([^\s@]+)/);
    if (!typeMatch) continue;

    const gqlTypeRaw = typeMatch[1];
    const { baseType, isList, optional } = parseGqlType(gqlTypeRaw);

    // Build the TS type string for display (without resolving yet)
    const scalarMapped = SCALAR_MAP[baseType] || TYPE_OVERRIDES[baseType] || baseType;
    let tsType: string;
    if (isList) {
      tsType = `Array<${scalarMapped}>`;
    } else {
      tsType = scalarMapped;
    }

    fields.push({
      name: nameMatch[1],
      gqlType: baseType,
      optional,
    });
  }

  return fields;
}

/**
 * Convert a GraphQL type+optional flag to a TypeScript string.
 */
function gqlToTypeScript(baseType: string, optional: boolean, isList: boolean): string {
  // If it's a known scalar, map directly
  const scalar = SCALAR_MAP[baseType] || TYPE_OVERRIDES[baseType];
  if (scalar) {
    if (isList) return `Array<${scalar}>`;
    return optional ? `${scalar} | null` : scalar;
  }

  // It's a named type — use the interface name
  if (isList) return `Array<${baseType}>`;
  return optional ? `${baseType} | null` : baseType;
}

/**
 * Generate a TypeScript interface body for a GraphQL type.
 */
function generateInterfaceBody(schema: string, typeName: string, depth: number): string[] {
  // ── v1.0.5：基础类型由 SDK（@tkwf/tsclient）提供，这里只生成类型别名 ──
  // 标量操作符过滤器 → OperationFilterInput<TValue>
  if (typeName === 'LongOperationFilterInput' || typeName === 'IntOperationFilterInput' || typeName === 'DecimalOperationFilterInput') {
    return [`export type ${typeName} = OperationFilterInput<number>;`];
  }
  if (typeName === 'DateTimeOperationFilterInput' || typeName === 'LocalDateOperationFilterInput') {
    return [`export type ${typeName} = OperationFilterInput<string>;`];
  }
  // 字符串/布尔过滤器 → SDK 同名类型（已在头部 import），跳过本地生成
  if (typeName === 'StringOperationFilterInput' || typeName === 'BooleanOperationFilterInput') {
    return [];
  }
  // 枚举过滤器 → EnumOperationFilterInput<string>
  if (/^\w+EnumOperationFilterInput$/.test(typeName)) {
    return [`export type ${typeName} = EnumOperationFilterInput<string>;`];
  }
  // Connection → Connection<Node, Edge>
  const connMatch = typeName.match(/^(\w+)Connection$/);
  if (connMatch) {
    const block = extractTypeBlock(schema, typeName);
    if (block) {
      const nodesMatch = block.match(/nodes:\s*\[(\w+)!?\]/);
      const edgesMatch = block.match(/edges:\s*\[(\w+Edge)!?\]/);
      if (nodesMatch && edgesMatch) {
        const nodeType = nodesMatch[1];
        const edgeType = edgesMatch[1];
        // 确保节点/边类型进入生成队列（否则别名引用的类型不会生成）
        resolveType(schema, edgeType, depth + 1);
        resolveType(schema, nodeType, depth + 1);
        return [`export type ${typeName} = Connection<${nodeType}, ${edgeType}>;`];
      }
    }
  }
  // Edge → Edge<Node>
  const edgeMatch = typeName.match(/^(\w+)Edge$/);
  if (edgeMatch) {
    const block = extractTypeBlock(schema, typeName);
    if (block) {
      const nodeMatch = block.match(/node:\s*(\w+)/);
      if (nodeMatch) {
        const nodeType = nodeMatch[1];
        resolveType(schema, nodeType, depth + 1);
        return [`export type ${typeName} = Edge<${nodeType}>;`];
      }
    }
  }
  // PageInfo → SDK 同名类型（已在头部 import），跳过本地生成
  if (typeName === 'PageInfo') {
    return [];
  }

  const fields = extractTypeFields(schema, typeName);
  if (fields.length === 0) return [];

  const bodyLines: string[] = [];
  bodyLines.push(`export interface ${typeName} {`);

  for (const f of fields) {
    // Resolve the type
    const resolved = resolveType(schema, f.gqlType, depth + 1);

    // Determine if list
    const rawMatch = extractTypeBlock(schema, typeName);
    // We need to check if the original field is a list. 
    // Re-parse the field line to get isList info
    const originalBlock = extractTypeBlock(schema, typeName);
    let isList = false;
    if (originalBlock) {
      // Find the field's type in the original schema
      const lineMatch = originalBlock.split('\n')
        .find(l => l.trim().startsWith(f.name + ':') || l.trim().startsWith(f.name + '('));
      if (lineMatch) {
        const typeRawMatch = lineMatch.match(/:\s*([^\s@]+)/);
        if (typeRawMatch) {
          const parsed = parseGqlType(typeRawMatch[1]);
          isList = parsed.isList;
        }
      }
    }

    if (resolved) {
      // Resolved to a known TS type
      const tsType = isList ? `Array<${resolved}>` : (f.optional ? `${resolved} | null` : resolved);
      bodyLines.push(`  ${f.name}: ${tsType};`);
    } else {
      // Fallback for unresolvable types
      bodyLines.push(`  ${f.name}: ${f.optional ? 'unknown' : 'unknown'};`);
    }
  }

  bodyLines.push('}');
  return bodyLines;
}

/**
 * Process the type generation queue, generating interfaces for enqueued types.
 */
function processTypeQueue(schema: string): void {
  const seen = new Set<string>();
  // schema 有 328 个 type/input/enum，BFS 类型队列可超 200；去重逻辑已防无限循环，此计数仅为兜底
  let safety = 1000; // prevent infinite loops

  while (typeGenerationQueue.length > 0 && safety > 0) {
    safety--;
    const item = typeGenerationQueue.shift()!;
    if (seen.has(item.typeName)) continue;
    seen.add(item.typeName);

    const body = generateInterfaceBody(schema, item.typeName, item.depth);
    if (body.length > 0) {
      generatedInterfaces.push({ typeName: item.typeName, body: body.join('\n') });
    }
  }
}

// ----- Selection String Builder -----

const MAX_SELECTION_DEPTH = 3;

/**
 * Build a complete GraphQL subfield selection string for a given type.
 * Recursively walks the type's fields up to MAX_SELECTION_DEPTH,
 * generating nested selections for object-typed fields.
 * Skips internal methods (toDto, toEntity, etc.).
 */
function buildSelection(schema: string, typeName: string, depth: number = 0, visited: Set<string> = new Set()): string {
  if (depth > MAX_SELECTION_DEPTH) return '';
  // Prevent circular references within the same chain
  if (visited.has(typeName)) return '';
  visited = new Set(visited);
  visited.add(typeName);

  const fields = extractTypeFields(schema, typeName);
  const parts: string[] = [];

  for (const f of fields) {
    if (SKIP_METHODS.has(f.name)) continue;

    // Check if it's a scalar type — include field name directly
    if (SCALAR_MAP[f.gqlType] || TYPE_OVERRIDES[f.gqlType]) {
      parts.push(f.name);
    } else if (isEnumType(schema, f.gqlType)) {
      // Enums are string-equivalent — include as scalar
      parts.push(f.name);
    } else {
      // Object type — recurse for nested selection
      const nested = buildSelection(schema, f.gqlType, depth + 1, visited);
      if (nested) {
        parts.push(`${f.name} { ${nested} }`);
      }
    }
  }

  return parts.join(' ');
}

// ----- Field Return Type Extraction (for Query/Mutation fields) -----

/**
 * Extract return type string from a single line of field definition.
 * Handles: "fieldName: ReturnType", "fieldName(...): ReturnType",
 */
function extractReturnTypeFromLine(line: string): string | null {
  const m = line.match(/:\s*([^\s@]+)/);
  return m ? m[1] : null;
}

// ----- Service Builder (unchanged from V4.3) -----

/** PascalCase a camelCase identifier (first char uppercase). */
function pascalCase(name: string): string {
  if (!name) return '';
  return name.charAt(0).toUpperCase() + name.slice(1);
}

/** Convert a string to singular form (basic English rules). */
function singularize(word: string): string {
  if (word.endsWith('ies')) return word.slice(0, -3) + 'y';
  if (word.endsWith('ses')) return word.slice(0, -2);
  if (word.endsWith('s') && !word.endsWith('ss')) return word.slice(0, -1);
  return word;
}

function determineService(fieldName: string): string {
  if (SPECIAL_SERVICES[fieldName]) return SPECIAL_SERVICES[fieldName];
  if (fieldName.startsWith('loginBy')) return 'Auth';

  for (const prefix of ALL_PREFIXES) {
    if (fieldName.startsWith(prefix) && fieldName.length > prefix.length) {
      const rest = fieldName.slice(prefix.length);
      if (rest[0] === rest[0]?.toUpperCase()) {
        return pascalCase(rest);
      }
    }
  }

  return pascalCase(fieldName);
}

function buildServices(
  fields: FieldInfo[],
): Map<string, OpInfo[]> {
  const services = new Map<string, OpInfo[]>();

  function addField(fieldName: string, type: 'query' | 'mutation') {
    const service = determineService(fieldName);
    if (!services.has(service)) services.set(service, []);
    services.get(service)!.push({ field: fieldName, type });
  }

  for (const f of fields) {
    addField(f.name, f.type);
  }

  // Merge singular/plural duplicates
  let changed = true;
  while (changed) {
    changed = false;
    const entries = [...services.entries()];
    for (const [name, ops] of entries) {
      const singular = singularize(name);
      if (singular !== name && services.has(singular)) {
        services.get(singular)!.push(...ops);
        services.delete(name);
        changed = true;
        break;
      }
    }
  }

  return services;
}

// ----- Formatting helpers -----

function formatFieldEntry(fieldName: string, type: 'query' | 'mutation', pad: number): string {
  return `  ${fieldName.padEnd(pad)}: { field: '${fieldName}', type: '${type}' } as const,`;
}

// ----- Generation -----

function generate(): void {
  if (!existsSync(SCHEMA_PATH)) {
    console.error(`[gen-ts-client] ERROR: Schema not found at ${SCHEMA_PATH}`);
    process.exit(1);
  }

  const schema = readFileSync(SCHEMA_PATH, 'utf-8');

  // Extract field info (name + return type + args) from Query and Mutation
  const queryFieldInfos = extractFieldInfos(schema, 'Query');
  const mutationFieldInfos = extractFieldInfos(schema, 'Mutation');

  // Tag each field with its type
  const queryFields = queryFieldInfos.map(f => ({ ...f, type: 'query' as const }));
  const mutationFields = mutationFieldInfos.map(f => ({ ...f, type: 'mutation' as const }));

  if (queryFields.length === 0 && mutationFields.length === 0) {
    console.error('[gen-ts-client] ERROR: No Query or Mutation fields found in schema.');
    process.exit(1);
  }

  const allFields = [...queryFields, ...mutationFields];
  const services = buildServices(allFields);

  // Build operation selection map for ServiceProxy
  const operationSelection: Map<string, string> = new Map();
  for (const f of allFields) {
    if (f.returnType && !SCALAR_MAP[f.returnType] && !TYPE_OVERRIDES[f.returnType] && !isEnumType(schema, f.returnType)) {
      const sel = buildSelection(schema, f.returnType);
      if (sel) {
        operationSelection.set(f.name, sel);
      }
    }
  }

  // Enqueue type resolution for all return types
  const returnTypeQueue = new Set<string>();
  for (const f of allFields) {
    if (f.returnType && !SCALAR_MAP[f.returnType] && !SKIP_TYPES.has(f.returnType)) {
      returnTypeQueue.add(f.returnType);
    }
    // Also enqueue arg types that are named types (not scalars)
    for (const arg of f.args) {
      if (arg.gqlType && !SCALAR_MAP[arg.gqlType]) {
        returnTypeQueue.add(arg.gqlType);
      }
    }
  }

  // Enqueue for type resolution
  for (const typeName of returnTypeQueue) {
    if (!generatedTypes.has(`iface_${typeName}`)) {
      generatedTypes.add(`iface_${typeName}`);
      typeGenerationQueue.push({ typeName, depth: 0 });
    }
  }

  // Process the type generation queue
  processTypeQueue(schema);

  // Also generate Arg interfaces for fields with named args
  const argTypeNameSet = new Set<string>();
  const argInterfaces: Array<{ argTypeName: string; body: string }> = [];
  for (const f of allFields) {
    if (f.args.length === 0) continue;
    // Only generate arg interfaces if args have named types (not just scalars or enums)
    const hasNamedTypes = f.args.some(a => 
      !SCALAR_MAP[a.gqlType] && !TYPE_OVERRIDES[a.gqlType] && !isEnumType(schema, a.gqlType)
    );
    if (!hasNamedTypes) continue;

    const argTypeName = `${pascalCase(f.name)}Args`;
    if (argTypeNameSet.has(argTypeName)) continue;
    argTypeNameSet.add(argTypeName);

    // Generate the arg interface
    const bodyLines: string[] = [];
    bodyLines.push(`export interface ${argTypeName} {`);
    for (const a of f.args) {
      const resolved = SCALAR_MAP[a.gqlType] || TYPE_OVERRIDES[a.gqlType] 
        || (isEnumType(schema, a.gqlType) ? 'string' : a.gqlType);
      if (a.isList) {
        bodyLines.push(`  ${a.name}?: Array<${resolved}>;`);
      } else {
        bodyLines.push(`  ${a.name}${a.optional ? '?' : ''}: ${resolved};`);
      }
    }
    bodyLines.push('}');
    argInterfaces.push({ argTypeName, body: bodyLines.join('\n') });
  }

  // Calculate padding for alignment (use field names only, unchanged)
  const queryFieldNames = queryFields.map(f => f.name);
  const mutationFieldNames = mutationFields.map(f => f.name);
  const queryPad = Math.max(...queryFieldNames.map((f) => f.length), 0) + 2;
  const mutationPad = Math.max(...mutationFieldNames.map((f) => f.length), 0) + 2;

  // Build lines
  const lines: string[] = [
    '// Auto-generated by scripts/gen-ts-client.ts',
    '// Do not edit manually. Run: npm run codegen',
    '',
    'import type { ChainablePromise } from "@tkwf/tsclient";',
    '',
    '// ===== Query Operations =====',
    `export const Query = {`,
  ];

  for (const f of queryFieldNames) {
    lines.push(formatFieldEntry(f, 'query', queryPad));
  }
  lines.push('} as const;');
  lines.push('');

  lines.push('// ===== Mutation Operations =====');
  lines.push(`export const Mutation = {`);
  for (const f of mutationFieldNames) {
    lines.push(formatFieldEntry(f, 'mutation', mutationPad));
  }
  lines.push('} as const;');
  lines.push('');

  // ===== Operation Selection Map (for Use/Call proxy subfield selection) =====
  if (operationSelection.size > 0) {
    lines.push('// ===== Operation Selection Map =====');
    lines.push('// Auto-generated subfield selections for object-typed returns.');
    lines.push('// Used by Use()/Call() proxy to satisfy GraphQL subfield requirement.');
    lines.push(`export const operationSelection: Record<string, string> = {`);
    const sortedOps = [...operationSelection.entries()].sort(([a], [b]) => a.localeCompare(b));
    for (const [fieldName, sel] of sortedOps) {
      lines.push(`  '${fieldName}': '${sel}',`);
    }
    lines.push('} as const;');
    lines.push('');
  }

  // ===== Generated TypeScript Interfaces for Schema Types =====
  if (generatedInterfaces.length > 0) {
    lines.push('// ===== Schema Types (Auto-generated from schema.graphql) =====');
    for (const iface of generatedInterfaces) {
      lines.push('');
      lines.push(iface.body);
    }
    lines.push('');
  }

  // ===== Generated Arg Interfaces =====
  if (argInterfaces.length > 0) {
    lines.push('// ===== Operation Argument Types =====');
    for (const iface of argInterfaces) {
      lines.push('');
      lines.push(iface.body);
    }
    lines.push('');
  }

  // ===== Service Typed Interfaces =====
  lines.push('// ===== Service Typed Interfaces =====');
  const sortedServices = [...services.entries()].sort(([a], [b]) =>
    a.localeCompare(b),
  );
  for (const [serviceName, ops] of sortedServices) {
    const ifaceName = `${serviceName}Service`;
    lines.push(`export interface ${ifaceName} {`);

    for (const op of ops) {
      // Find the field info matching this operation
      const fieldInfo = allFields.find(f => f.name === op.field);

      // Determine args type
      let argsType = 'Record<string, unknown>';
      const argTypeName = fieldInfo && fieldInfo.args.length > 0
        ? `${pascalCase(op.field)}Args`
        : null;
      if (argTypeName && argTypeNameSet.has(argTypeName)) {
        argsType = argTypeName;
      }

      // Determine return type
      let retType = 'unknown';
      if (fieldInfo && fieldInfo.returnType) {
        // Check if we generated an interface for this type
        const hasInterface = generatedInterfaces.some(
          gi => gi.typeName === fieldInfo.returnType
        );
        if (hasInterface) {
          retType = fieldInfo.returnType;
        } else if (SCALAR_MAP[fieldInfo.returnType]) {
          retType = SCALAR_MAP[fieldInfo.returnType]!;
        } else if (TYPE_OVERRIDES[fieldInfo.returnType]) {
          retType = TYPE_OVERRIDES[fieldInfo.returnType]!;
        }
      }

      // 列表返回类型用 `T[]` 后缀形式（而非 `Array<T>`），
      // 兼容 @tkwf/tsclient-mock 生成器对数组类型的解析（`T[]` 模式成熟）
      const retTypeTs = (fieldInfo?.isList && retType !== 'unknown')
        ? `${retType}[]`
        : retType;

      lines.push(`  ${op.field}(args?: ${argsType}): ChainablePromise<${retTypeTs}>;`);
    }

    lines.push('}');
    lines.push('');
  }

  // ===== QueryBuilder Types (V4.9.20) =====
  // 为具有 {Entity}FilterInput + {Entity}SortInput 的实体生成 QueryBuilder 支撑类型
  const queryBuilderSections = generateQueryBuilderSections(schema);
  if (queryBuilderSections.length > 0) {
    lines.push('// ===== QueryBuilder Types (V4.9.20) =====');
    lines.push('import { QueryBuilderBase, registerQueryBuilder } from "@tkwf/tsclient";');
    lines.push('import type {');
    lines.push('  StringFieldOperators, NumberFieldOperators,');
    lines.push('  DateFieldOperators, BooleanFieldOperators,');
    lines.push('  OperationFilterInput, StringOperationFilterInput, BooleanOperationFilterInput,');
    lines.push('  EnumOperationFilterInput, Connection, Edge,');
    lines.push('  SelectFieldsOf, OrderByFieldsOf,');
    lines.push('} from "@tkwf/tsclient";');
    lines.push('');
    for (const section of queryBuilderSections) {
      lines.push('');
      lines.push(section);
    }
    lines.push('');
  }

  // Ensure output directory exists
  const outDir = dirname(OUTPUT_PATH);
  if (!existsSync(outDir)) {
    mkdirSync(outDir, { recursive: true });
  }

  const output = lines.join('\n');
  writeFileSync(OUTPUT_PATH, output, 'utf-8');

  const totalFields = allFields.length;
  const resolvedTypes = generatedInterfaces.length + argInterfaces.length;
  console.log(
    `[gen-ts-client] OK: ${totalFields} operations → ${services.size} services, ${resolvedTypes} types written to ${OUTPUT_PATH}`,
  );
  console.log(`  Query fields:  ${queryFields.length}`);
  console.log(`  Mutation fields: ${mutationFields.length}`);
  console.log(`  Services:       ${services.size}`);
  console.log(`  Generated TS types: ${generatedInterfaces.length} schema interfaces + ${argInterfaces.length} arg interfaces`);
  console.log(`  QueryBuilder entities: ${queryBuilderSections.length}`);
  console.log(`  Unresolved return types: ${allFields.filter(f => !SCALAR_MAP[f.returnType] && !generatedInterfaces.some(gi => gi.typeName === f.returnType) && f.returnType).map(f => f.returnType).join(', ')}`);
}

// ── QueryBuilder 生成（V4.9.20） ──

/**
 * 生成 QueryBuilder 支撑类型：
 *  - {Entity}Fields 字段代理接口
 *  - {Entity}SelectFields 字段选择类型（Partial）
 *  - {Entity}OrderByFields 排序字段代理
 *  - {Entity}QueryBuilder 子类（实现 compileGraphQL/字段代理/defaultFields）
 *  - registerQueryBuilder 注册调用
 *
 * 识别条件：schema 中存在 {Entity}FilterInput 类型（即该实体可过滤）。
 */
function generateQueryBuilderSections(schema: string): string[] {
  // 收集所有 FilterInput 类型名 → 推导实体名
  const entityNames = new Set<string>();
  const filterInputRe = /input\s+(\w+FilterInput)\b/gi;
  let m: RegExpExecArray | null;
  while ((m = filterInputRe.exec(schema)) !== null) {
    const filterName = m[1];
    // MerchantUserInfoFilterInput → MerchantUserInfo
    const entityName = filterName.replace(/FilterInput$/, '');
    if (entityName && entityName.length > 0) {
      entityNames.add(entityName);
    }
  }

  const sections: string[] = [];
  for (const entityName of [...entityNames].sort()) {
    const section = generateSingleEntityQueryBuilder(schema, entityName);
    if (section) sections.push(section);
  }
  return sections;
}

/** 生成单个实体的 QueryBuilder 类型块。 */
function generateSingleEntityQueryBuilder(schema: string, entityName: string): string | null {
  // 读取实体字段（从 type {Entity} { ... } 块）
  const entityFields = extractEntityFieldTypes(schema, entityName);
  if (entityFields.length === 0) return null;

  // 过滤掉导航属性（对象类型，非标量）——只保留可过滤的标量字段
  const scalarFields = entityFields.filter(f => mapFieldToOperator(f.gqlType, schema) !== null);

  // 生成字段代理接口
  const fieldsInterface = [
    `export interface ${entityName}Fields {`,
    ...scalarFields.map(f =>
      `  ${f.name}: ${mapFieldToOperator(f.gqlType, schema)}<${entityName}Fields>;`
    ),
    `}`,
  ].join('\n');

  // 生成 SelectFields 类型（SDK 泛型 SelectFieldsOf）
  const selectFields = `export type ${entityName}SelectFields = SelectFieldsOf<${entityName}Fields>;`;

  // 生成 OrderByFields 类型（SDK 泛型 OrderByFieldsOf）
  const orderByFields = `export type ${entityName}OrderByFields = OrderByFieldsOf<${entityName}Fields>;`;

  // 默认字段列表（全部标量字段名）
  const defaultFieldsList = scalarFields.map(f => `"${f.name}"`).join(', ');

  // QueryBuilder 子类
  const qbClass = [
    `export class ${entityName}QueryBuilder extends QueryBuilderBase<`,
    `  ${entityName},`,
    `  ${entityName}Fields,`,
    `  ${entityName}OrderByFields,`,
    `  ${entityName}SelectFields`,
    `> {`,
    `  protected defaultFields(): string[] {`,
    `    return [${defaultFieldsList}];`,
    `  }`,
    ``,
    `  protected createFieldsProxy(): ${entityName}Fields {`,
    `    return this.createFieldsProxyFrom<${entityName}Fields>({`,
    ...scalarFields.map(f => `      ${f.name}: "${f.gqlType}",`),
    `    });`,
    `  }`,
    `}`,
    ``,
    `registerQueryBuilder("${entityName}", (transport, field, sessionKey) =>`,
    `  new ${entityName}QueryBuilder(transport, field, sessionKey));`,
  ].join('\n');

  return [fieldsInterface, selectFields, orderByFields, qbClass].join('\n\n');
}

/** 从 type {Entity} 块提取标量字段（name + gqlType）。含枚举字段（枚举在 HC 协议层映射为字符串，可过滤）。 */
function extractEntityFieldTypes(schema: string, entityName: string): Array<{ name: string; gqlType: string }> {
  const block = extractTypeBlock(schema, entityName);
  if (!block) return [];

  const results: Array<{ name: string; gqlType: string }> = [];
  const fields = extractFieldInfos(schema, entityName);
  for (const f of fields) {
    // 去掉 Connection/Edge/Dto 等对象类型
    const base = f.returnType.replace(/[!\[\]]/g, '');
    // 标量 + 枚举 都保留（枚举在 HC 中序列化为字符串，可用 StringFieldOperators）
    if (SCALAR_MAP[base] || TYPE_OVERRIDES[base] || isEnumType(schema, base)) {
      results.push({ name: f.name, gqlType: base });
    }
  }
  return results;
}

/** 将 GraphQL 类型映射为操作符类型名（不可过滤返回 null）。枚举在 HC 协议层映射为字符串值。 */
function mapFieldToOperator(gqlType: string, schema?: string): string | null {
  // 枚举类型（不在 SCALAR_MAP 中，但 schema 中定义 enum）→ StringFieldOperators
  if (schema && isEnumType(schema, gqlType)) {
    return 'StringFieldOperators';
  }
  switch (gqlType) {
    case 'String': case 'ID': case 'Url': case 'UUID':
      return 'StringFieldOperators';
    case 'Int': case 'Float': case 'Decimal': case 'Long': case 'Byte': case 'Short':
      return 'NumberFieldOperators';
    case 'DateTime': case 'LocalDate':
      return 'DateFieldOperators';
    case 'Boolean':
      return 'BooleanFieldOperators';
    default:
      return null; // 对象类型（导航属性等）：不生成操作符
  }
}

// ── GraphQL_Api.md 文档生成（V1.0.6 新增） ──
// 输出到项目侧 .TKWF/（与 Domain_Api.md / DataService_API.md 同一活态文档目录），
// 而非框架侧 —— 用 SOLUTION_ROOT（= 解决方案根 XiaoShuTong/）定位，避免多退层级

const DOC_OUTPUT_PATH = resolve(
  SOLUTION_ROOT,
  '.TKWF',
  'GraphQL_Api.md',
);

const CODECONFIG_PATH = resolve(__dirname, 'codegen-config.json');

interface CodegenConfig {
  exposureStatus?: Record<string, 'exposed' | 'unexposed' | 'missing' | 'partial'>;
  groupOverrides?: Record<string, string>;
  pageMapping?: Record<string, string[]>;
  blockingPages?: Record<string, string>;
}

function loadCodegenConfig(): CodegenConfig {
  if (!existsSync(CODECONFIG_PATH)) {
    console.warn('[gen-ts-client] WARNING: codegen-config.json not found, using defaults.');
    return {};
  }
  try {
    return JSON.parse(readFileSync(CODECONFIG_PATH, 'utf-8'));
  } catch (err) {
    console.warn('[gen-ts-client] WARNING: Failed to parse codegen-config.json, using defaults:', err);
    return {};
  }
}

async function generateDoc(schema: string): Promise<void> {
  // 从 WebH5 的 node_modules 动态加载 @tkwf/tsclient
  const tsclientPath = resolve(WEBH5_NM, '@tkwf', 'tsclient');
  const tsclientUrl = pathToFileURL(resolve(tsclientPath, 'dist', 'index.js')).href;

  let tsclientMod: any;
  try {
    tsclientMod = await import(tsclientUrl);
  } catch {
    // 回退到 src 入口
    tsclientMod = await import(pathToFileURL(tsclientPath).href);
  }

  const { parseGraphQLSchema } = tsclientMod;
  const { generateGraphQLApiDoc: renderDoc } = tsclientMod;

  const parsed = await parseGraphQLSchema(schema);
  const config = loadCodegenConfig();

  const options: GenerateGraphQLApiDocOptions = {
    domainName: 'XiaoShuTong WebH5',
    ignoredFields: new Set(['isFromPersistentSource', 'toDto']),
    exposureStatus: config.exposureStatus,
    groupOverrides: config.groupOverrides,
    pageMapping: config.pageMapping,
    blockingPages: config.blockingPages,
  };

  const doc = renderDoc(parsed, options);

  const outputDir = dirname(DOC_OUTPUT_PATH);
  if (!existsSync(outputDir)) {
    mkdirSync(outputDir, { recursive: true });
  }
  writeFileSync(DOC_OUTPUT_PATH, doc, 'utf-8');
  console.log(`[gen-ts-client] GraphQL_Api.md written to ${DOC_OUTPUT_PATH}`);
}

// ----- Run -----
async function main(): Promise<void> {
  const schema = readFileSync(SCHEMA_PATH, 'utf-8');

  // Step 1: 生成 ts-client.g.ts（原有逻辑）
  generate();

  // Step 2: 生成 GraphQL_Api.md（V1.0.6 新增）
  await generateDoc(schema);
}

main().catch((err) => {
  console.error('[gen-ts-client] Fatal error:', err);
  process.exit(1);
});
