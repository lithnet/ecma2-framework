# Lithnet.Ecma2Framework.Shim

The **shim** is the net48 management-agent DLL that loads inside the FIM/MIM sync engine (`miiserver.exe`) and presents a v3 ECMA 2.2 management agent to it. It implements the host `IMAExtensible2*` interfaces, but it contains none of the actual MA logic. Instead, every host call is spawned out to, and marshalled across a local named pipe to, an out-of-process net8 **worker** (`Lithnet.Ecma2Framework.Worker`) that hosts the consumer's providers. The shim is the FIM-facing half of the split; the worker is the logic-bearing half.

## Why the shim exists

`miiserver.exe` loads every management-agent extension into a single shared AppDomain with a shared bin/probing path. Any two MAs (or an MA and the host itself) that bring incompatible versions of a shared dependency collide — the "shared-bin dependency hell" that has historically broken MIM extensions at load or schema time. The v3 architecture escapes this by keeping the in-process footprint of each MA down to a single dependency-free net48 DLL whose entire reference closure is `{ host MMS, the .NET Framework BCL, CsWin32 (build-time only) }`. All third-party and modern dependencies (Microsoft.Extensions DI, StreamJsonRpc, the consumer's own packages) live in the separate net8 worker process and never touch `miiserver`'s AppDomain.

## Where it sits

```
  miiserver.exe (net48, FIM/MIM sync engine, single shared AppDomain)
        |
        |  loads <MAName>.Ecma2.dll  (THIS shim — dependency-free)
        v
  +-------------------------------------------------------------+
  |  Ecma2Implementation / Ecma2ImportImplementation /          |  public host-facing
  |  Ecma2ExportImplementation / Ecma2PasswordImplementation    |  IMAExtensible2* impls
  +-------------------------------------------------------------+
        |  forwards to internal connection
        v
  +-------------------------------------------------------------+
  |  SchemaConnection / CapabilitiesConnection /                |  internal connections
  |  ParametersConnection / ImportConnection /                  |  (serialise args, own the
  |  ExportConnection / PasswordConnection                      |   WorkerSession)
  +-------------------------------------------------------------+
        |  WorkerSession.Open(): create pipe -> spawn worker -> connect
        v
  +-------------------------------------------------------------+
  |  WorkerProcessHost (+ JobObject)   JsonRpcPipeClient        |
  |  spawns/owns the worker process    Content-Length JSON-RPC  |
  +-------------------------------------------------------------+
        |  local named pipe  (lithnet-ecma2-<GUID>)  same-identity only
        v
  Lithnet.Ecma2Framework.Worker.exe (net8, out of process — real MMS types + consumer providers)
```

## Key types and responsibilities

### Public host-facing implementations (the only public types in the shim)

FIM scans the public types of the per-MA shim assembly and instantiates the one(s) implementing the interface it needs. There are exactly four:

- **`Ecma2Implementation`** — implements `IMAExtensible2GetSchema`, `IMAExtensible2GetCapabilitiesEx`, `IMAExtensible2GetParametersEx`, and `IMAExtensible2GetParameters` (the config-UI surface). It owns one `SchemaConnection`, one `CapabilitiesConnection`, and one `ParametersConnection`. The legacy non-`Ex` parameter members forward to the `Ex` connection with `pageNumber = 1`.
- **`Ecma2ImportImplementation`** — implements `IMAExtensible2CallImport`; owns an `ImportConnection`.
- **`Ecma2ExportImplementation`** — implements `IMAExtensible2CallExport`; owns an `ExportConnection`.
- **`Ecma2PasswordImplementation`** — implements `IMAExtensible2Password`; owns a `PasswordConnection`.

These four types are plain **shared source** compiled into every per-MA shim (not source-generated). Their fixed type names do not collide across MAs because FIM resolves the extension type from the specific, uniquely-named shim assembly recorded in each MA's config. Each constructor resolves the worker path eagerly via `WorkerPathResolver.Resolve()` and hands it to its connection(s).

The shim's public surface is deliberately limited to these four classes; everything else is `internal`. The shared transport/serialization types (which are `public` in the worker's `ObjectModel`/`Serialization` assemblies) are flipped to `internal` when compiled into the shim by the `ECMA2_SHIM_INTERNAL` compile constant, preserving that invariant.

### Connection classes (internal) — one per host surface

Each connection translates the host's real-typed call into the worker's JSON-RPC method and back:

- **`SchemaConnection`**, **`CapabilitiesConnection`**, **`ParametersConnection`** — config-UI calls. Each spawns a fresh worker per call inside a `try/finally` and disposes it unconditionally, so no worker is left running after a config-UI interaction. (A `TODO(codegen/perf)` notes these could one day be answered locally from static metadata to avoid the spawn.)
- **`ImportConnection`** — `OpenImportConnection` spawns the worker and sends `OpenImport`; `GetImportEntries` calls `GetImportPage` and deserialises the page's real `CSEntryChange` list; `CloseImportConnection` sends `CloseImport` then disposes the worker in a `finally`. Reports `ImportDefaultPageSize = 100`, `ImportMaxPageSize = 10000`.
- **`ExportConnection`** — `OpenExportConnection` / `PutExportEntries` (sends the real `CSEntryChange` batch, deserialises the per-entry `CSEntryChangeResult` list) / `CloseExportConnection`. Same default/max page sizes (100 / 10000).
- **`PasswordConnection`** — `OpenPasswordConnection`, `GetConnectionSecurityLevel`, `SetPassword`, `ChangePassword`, `ClosePasswordConnection`. The host hands these a live `CSEntry`, an abstract engine object that cannot be reconstructed across the pipe, so `RealCSEntryToIdentity` extracts the identity subset a password provider needs into a framework-owned `CSEntryIdentity` and that is what crosses. Secrets are converted from `SecureString` to plaintext **only at the wire boundary** (`SecureStringConverter`) and never logged.

On each connection, the live FIM config-parameter / run-step / schema objects are serialised with the shared `MmsPipeSerializer` (see Transport below) before being passed to the pipe client.

### Process and pipe machinery (internal)

- **`WorkerSession`** — owns the lifecycle of exactly one worker: resolve path → create the pipe server → spawn the worker → wait for it to connect (30 s timeout) → return the connected `JsonRpcPipeClient`. If any step throws, everything already started is disposed so no worker is orphaned on a failed open. `Dispose()` tears down the pipe client and the worker host unconditionally and is safe to call more than once. Each connection holds one `WorkerSession`.
- **`WorkerProcessHost`** — launches the worker process with `--pipe <name>`, captures its stderr (so the shim and its tests can surface worker diagnostics), and assigns it to a `JobObject` immediately after start. Disposing it kills the worker via the job.
- **`JobObject`** — wraps a Windows Job Object configured with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. Closing the job handle (on dispose, or if the shim process itself exits) terminates the worker. The Win32 entry points are generated by **CsWin32** from `NativeMethods.txt`; there is no hand-rolled P/Invoke in the shim.
- **`JsonRpcPipeClient`** — a BCL-only JSON-RPC 2.0 client. The **shim is the pipe server** and the worker is the client. It writes/reads Content-Length-delimited frames (`Content-Length: N\r\n\r\n` + N UTF-8 bytes, no BOM) matching StreamJsonRpc's `HeaderDelimitedMessageHandler` on the worker side. It exposes a typed method per RPC (`GetSchema`, `GetCapabilities`, `OpenImport`, `GetImportPage`, `PutExport`, `SetPassword`, …) and parses the response envelope.

### Helpers and envelope types (internal)

- **`WorkerPathResolver`** — resolves the absolute worker exe path. The MA name is the shim's own assembly simple name (`Assembly.GetExecutingAssembly().GetName().Name`), so the generated code never has to bake it in. Resolution order: (1) the per-MA registry value `HKLM\Software\Lithnet\Ecma2\<MAName>\WorkerPath` (64-bit view, written by the installer — production); (2) the `LITHNET_ECMA2_WORKER_EXE` environment variable (dev/test). A missing path is a misconfiguration that throws `InvalidOperationException` naming both sources — it is never guessed or defaulted.
- **`RealCSEntryToIdentity`** — copies DN, RDN, ObjectType, ObjectClass, MA name, and every *present* attribute (with its value(s), selecting the typed accessor for the attribute's `AttributeType`) from a live `CSEntry` into a `CSEntryIdentity`. Carrying anchor attribute values lets directories that key on an anchor rather than the DN still locate the account.
- **`SecureStringConverter`** — converts a `SecureString` to plaintext via `Marshal.SecureStringToGlobalAllocUnicode`, zeroing and freeing the unmanaged buffer in a `finally` block. Must never be called from a logging path.
- **`ConfigParameterPayload`** — serialises a host config-parameter collection to the `List<ConfigParameter>` XML payload the worker expects (encrypted `SecureValue` content crosses verbatim through the surrogate).
- **`RpcResponseEnvelope`** + `StringResultEnvelope` / `BoolResultEnvelope` / `ImportPageResultEnvelope` (+ `ImportPageResultData`) — the `[DataContract]` JSON-RPC 2.0 response shapes the client deserialises with `DataContractJsonSerializer`; unknown members (`jsonrpc`, `id`) are ignored.
- **`RpcError`** — the JSON-RPC `error` object. Its `data` member carries the structured `MmsExceptionEnvelope` (from the worker) used to reconstruct the exact host exception.
- **`ImportPageResult`** — the shim-side carrier of one `GetImportPage` result (`EntriesXml` + `MoreToImport` + `CustomData`).
- **`EntryMarshalException`** — wraps a single-entry rebuild failure with the entry identifier so a poison entry can be isolated without failing the batch.

## End-to-end request/response flow

Taking import as the worked example (export and password follow the same shape):

1. FIM calls `Ecma2ImportImplementation.OpenImportConnection(configParameters, schema, runStep)`.
2. It forwards to `ImportConnection.OpenImportConnection`, which calls `WorkerSession.Open()`. The session generates a unique pipe name `lithnet-ecma2-<GUID>`, **creates the `JsonRpcPipeClient` pipe server first**, then spawns the worker (`WorkerProcessHost.Start()` → assigned to a `JobObject`), then waits for the worker to connect (verifying same-identity on connect).
3. `ImportConnection` serialises the real `OpenImportConnectionRunStep` and `Schema` with `MmsPipeSerializer`, captures the engine's `Utils.ExtensionsDirectory` (the worker's own `Utils` static would resolve to the wrong directory), and calls `client.OpenImport(runStepXml, schemaXml, extensionsDirectory)`.
4. `JsonRpcPipeClient` frames a JSON-RPC request, writes it to the pipe, reads the framed response, and returns the result string. FIM then calls `GetImportEntries` repeatedly: each call sends `GetImportPage`, and `ImportConnection.ProcessPage` deserialises the returned `EntriesXml` back into **real** `CSEntryChange` objects that are handed straight to the engine.
5. `CloseImportConnection` sends `CloseImport` (returning the outbound watermark) and then disposes the `WorkerSession` in a `finally`, killing the worker via its job object.

## Process and connection lifecycle

- **One worker per session.** Each connection's `WorkerSession` spawns at most one worker. Config-UI connections spawn-and-dispose per call; import/export/password sessions keep the worker alive across the open/page/close cycle and dispose at close.
- **No orphans.** A failed `Open()` disposes everything it started. `Close*Connection` disposes in a `finally` regardless of whether the close RPC succeeded. The `JobObject`'s `KILL_ON_JOB_CLOSE` is the backstop: if the shim (or `miiserver`) exits without an orderly close, the worker dies with the job.
- **Same-identity named-pipe security.** Passwords cross the pipe as plaintext at the boundary, so the channel is locked to a single identity by two layers. (1) The pipe ACL grants access to **only** the creating user's SID (`CreateCurrentUserOnlyPipeSecurity`), so the OS denies any other account at connect time, and `maxNumberOfServerInstances = 1` plus creating the server before the worker spawns prevents a rogue process squatting the unique pipe name. (2) After connect, `VerifyConnectedClientIsSameIdentity` reads the client's SID via `RunAsClient` and fails closed (disconnect + throw `UnauthorizedAccessException`) unless it equals the server's. The worker connects with `TokenImpersonationLevel.Identification` so the server can identify it without being able to act as it.

## Transport (Path C)

There is **no structured wire-DTO layer**. Every method's real `Microsoft.MetadirectoryServices`-typed payload (`Schema`, `CSEntryChange`, `MACapabilities`, `ConfigParameter`, the run-steps, …) crosses the pipe as a `MmsPipeSerializer` XML string carried as a JSON-RPC string argument or result; simple scalars (enum names, page sizes, custom-data watermarks) cross as plain JSON string/int values. `MmsPipeSerializer` lives in the shared `Lithnet.Ecma2Framework.Serialization` project and is **compiled into the shim by source** (the shim cannot reference a `Lithnet.*` assembly without breaking its closure invariant) and into the worker **as an assembly**, so both ends carry byte-identical wire encoding. It is a `DataContractSerializer` rooted at the real abstract type with the `*Serializable` surrogate DTOs as known types and `MmsSerializationSurrogate` attached (net4x via `IDataContractSurrogate`, net8 via `SetSerializationSurrogateProvider`). The worker re-materialises the real graph and the shim hands the real graph straight to FIM.

### Error propagation

When the worker's handler throws, it attaches a serialised `MmsExceptionEnvelope` to the JSON-RPC error `data`. `JsonRpcPipeClient.ThrowWorkerError` deserialises it and `MmsExceptionReconstructor` re-throws the **exact** real host exception (e.g. `OldPasswordIncorrectException`, `PasswordPolicyViolationException`) — or an `ExtensibleExtensionException` carrier for a non-host worker exception — so FIM's type-driven handling fires. Only an envelope-less transport/framing failure surfaces as a bare `InvalidOperationException`; on the password path that is converted to an `ExtensibleExtensionException` so MIM still handles it (fail-closed). The required worker path and the `--pipe` argument both fail loudly when missing rather than defaulting.

## How it is built and deployed

- **Target / closure.** `net48`; `LangVersion 12`; `Nullable disable`. `AllowUnsafeBlocks` is on only because CsWin32 generates unsafe code for HANDLE/pointer types. `PInvoke009` (CsWin32 suggesting `System.Memory`) is suppressed on purpose: pulling `System.Memory` into `miiserver`'s shared AppDomain would reintroduce exactly the shared-assembly conflict class this design eliminates. The reference closure is `{ host MMS (`Microsoft.MetadirectoryServicesEx.dll`, referenced locally and `Private=true`), BCL, CsWin32 (build-time only, no runtime dependency) }`.
- **Shared source set.** `Lithnet.Ecma2Framework.Shim.csproj` sets `EnableDefaultCompileItems=false` and imports `Lithnet.Ecma2Framework.Shim.Sources.targets`, which is the single source of truth for the shim's compile glob, the shared `Serialization` sources (linked in by absolute path so they resolve identically wherever imported), the `NativeMethods.txt` `AdditionalFiles` entry, and the CsWin32 package reference. The same `.targets` is imported by the source-generated per-MA shim `.csproj` (in a different directory) so both build from identical sources. Only `AssemblyInfo.cs` (carrying `InternalsVisibleTo` for the test assemblies) is project-owned and excluded from the shared glob.
- **Deployment.** Each MA ships its own uniquely-named, dependency-free shim DLL (`<MAName>.Ecma2.dll`), placed in FIM's `Extensions` directory and loaded by `miiserver`. The MA name baked into FIM's config equals the shim assembly's simple name. The per-MA registry value `HKLM\Software\Lithnet\Ecma2\<MAName>\WorkerPath` (written by the installer) points the shim at the net8 worker in Program Files.

## Key invariants

- **Shim dependency closure = `{ host MMS, BCL, CsWin32 }`.** No `Lithnet.*` assembly reference, no third-party runtime package. Anything richer goes in the worker.
- **Public surface = the four `IMAExtensible2*` impl classes only.** Everything else is `internal`; shared transport types are forced internal via `ECMA2_SHIM_INTERNAL`.
- **Same-identity, single-instance, plaintext-secret pipe.** ACL-to-own-SID + post-connect SID verification + unique per-session GUID name + `maxNumberOfServerInstances = 1` + create-server-before-spawn.
- **Lossless real-type marshalling.** Real MMS graphs cross as `MmsPipeSerializer` XML and are handed to FIM as real objects; no per-field translation, no mirror types, no defaults substituted.
- **No orphaned workers.** Every spawn path disposes on failure; close disposes in a `finally`; `JobObject` `KILL_ON_JOB_CLOSE` is the backstop.
- **Required inputs fail loudly.** The worker path and the `--pipe` argument are never defaulted or guessed.

## Related projects

- `Lithnet.Ecma2Framework.Worker` — the net8 process this shim spawns; hosts the consumer's providers over the real MMS types. See its README.
- `Lithnet.Ecma2Framework.Serialization` — the shared `MmsPipeSerializer` + surrogate + `*Serializable` DTOs, compiled into the shim by source and into the worker as an assembly.
- `Lithnet.Ecma2Framework` — the provider interfaces and DI runtime the worker hosts.
