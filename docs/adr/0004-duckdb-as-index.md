# DuckDB as the in-process index for record queries

DuckDB is the in-process analytical query engine for the record index. It holds a queryable, typed read model of all loaded records, derived from plugins via Mutagen. It is a cache — deleting it loses nothing and it rebuilds from plugins at any time.

Key reasons over SQLite: columnar storage makes GROUP BY and aggregation across hundreds of thousands of records sub-100ms (conflict detection across a 200-mod load order is a GROUP BY); native JSON column support with path queries; parallel query execution; recursive CTE support for graph traversal. Still in-process like SQLite — no separate server.

## Considered options

**SQLite** — Adequate for single-plugin editing at modest scale. JSON support is an afterthought; analytical queries across full load orders are slow. Rejected.

**Kuzu (graph database)** — The reference graph is genuinely a graph problem and Kuzu handles reachability queries more elegantly. Excluded for v1 in favor of DuckDB recursive CTEs, which are sufficient. Revisit if reachability analysis becomes a priority.

**PostgreSQL / external databases** — Inappropriate for a local desktop tool. No server process should be required. Rejected.
