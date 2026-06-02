# C# for the backend service

Everything that touches Mutagen or DuckDB is C#. This is not a preference — Mutagen is a C# NuGet library. Using it from any other language requires either a native interop layer or a process boundary, both adding complexity for no benefit. The backend is an ASP.NET Core minimal API running as a local process on localhost, emitting an OpenAPI spec via Swashbuckle.

## Considered options

**Python (FastAPI)** — Cannot use Mutagen directly. A Python layer in front of a C# Mutagen service is a pure proxy — latency and a language context switch with no benefit. Rejected.

**Node.js** — Same problem as Python. Also the approach zEdit took (Electron + native Node addon wrapping xedit-lib); zEdit is abandoned. Rejected.

**C / C++** — No justification. Mutagen is C#; C interop would be a significant maintenance burden. Rejected.
