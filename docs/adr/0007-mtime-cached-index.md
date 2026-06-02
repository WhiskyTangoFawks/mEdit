# Session index uses mtime-based incremental reindex

**Status:** superseded by [ADR-0001](0001-delete-session-cache-use-incremental-indexing.md) — `SessionCache` was deleted; the mtime strategy cannot work with an in-memory DuckDB connection (`:memory:` vanishes on process exit, so the cache always misses).

On first load, Mutagen reads all plugins and populates DuckDB. On subsequent sessions, each plugin's `file_mtime` is compared against a stored timestamp; unchanged plugins are skipped. Only modified plugins are reprocessed. This makes repeat sessions feel instant for the typical workflow of editing one plugin in a stable load order.

A `load_order_hash` (hash of the plugin list + all mtimes) in `index_state` detects when the full load order has changed.
