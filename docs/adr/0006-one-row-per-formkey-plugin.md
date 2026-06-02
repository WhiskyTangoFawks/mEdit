# One index row per (FormKey, Plugin) pair

The records table uses `(form_key, plugin)` as its composite primary key. The same FormKey appears once per plugin that contains it — the original definition and every override. This makes every major operation a direct SQL query:

- **Winning record** — `WHERE form_key = ? AND is_winner = true`
- **Full override stack** — `WHERE form_key = ? ORDER BY load_order_idx`
- **Conflict detection** — `GROUP BY form_key HAVING COUNT(*) > 1`
- **ITM detection** — self-join where `data` is identical across two rows for the same FormKey
- **Field-level conflicts** — join two rows and compare individual typed columns
