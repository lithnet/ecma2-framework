# Lithnet.Ecma2Framework.Worker

The **worker** is the net8, out-of-process half of the v3 ECMA 2.2 architecture. It is the process the net48 **shim** (`Lithnet.Ecma2Framework.Shim`, loaded inside `miiserver.exe`) spawns and talks to over a local named pipe. Unlike the shim — whose closure is deliberately limited to `{ host MMS, BCL, CsWin32 }` — the worker references the **real `Microsoft.MetadirectoryServices` (MMS) types** and hosts the consumer's providers (the actual MA logic) via Microsoft.Extensions dependency injection, free to bring StreamJsonRpc, modern DI, and the consumer's own packages without colliding with anything inside `miiserver`'s shared AppDomain.

This project (`Lithnet.Ecma2Framework.Worker`) is a **library**, not an executable. In the v3 codegen model the *consumer's own project* compiles to the worker exe: a source generator emits a `Main` in the consumer assembly that constructs the consumer's `IEcmaStartup` and the generated `IConfigRegistrationProvider` and calls `WorkerEntryPoint.RunAsync`. There is therefore no `Program.cs` in this project — the entry point is the public `WorkerEntryPoint.RunAsync` method that the generated `Main` invokes.

## Where it sits

```
  Lithnet.Ecma2Framework.Worker.exe  (net8, out of process)
        ^
        |  local named pipe (lithnet-ecma2-<GUID>), Content-Length JSON-RPC, same-identity only
        |
  +-------------------------------------------------------------+
  |  miiserver.exe -> <MAName>.Ecma2.dll (net48 shim, pipe SERVER) |
  +-------------------------------------------------------------+

  Inside the worker process:

  generated Main  ->  WorkerEntryPoint.RunAsync(startup, registrationProvider, args)
        |
        |  WorkerHost.Create(startup, registrationProvider).BuildContainer(...)   (DI container)
        v
  WorkerRunner.RunPipeLoopAsync(pipeName, new SchemaRpcTarget(workerHost))
        |
        |  NamedPipeClientStream.Connect  ->  JsonRpc.Attach(pipe, target)  ->  await rpc.Completion
        v
  SchemaRpcTarget  (one method per RPC: GetSchema, OpenImport, GetImportPage, PutExport, SetPassword, ...)
        |
        |  deserialise real MMS args (MmsPipeSerializer)  ->  drive orchestrator  ->  serialise result
        v
  Ecma2InitOrchestrator / Ecma2ImportOrchestrator / Ecma2ExportOrchestrator / Ecma2PasswordOrchestrator
        |
        v
  consumer providers (ISchemaProvider, IObjectImportProvider, IObjectExportProvider,
                      IObjectPasswordProvider, ICapabilitiesProvider, IConfigParametersProvider)
        resolved from the DI container built by Lithnet.Ecma2Framework
```

## Key types and responsibilities

### Entry point and runtime loop

- **`WorkerEntryPoint`** (public) — the single public entry point. `RunAsync(IEcmaStartup startup, IConfigRegistrationProvider registrationProvider, string[] args)`:
  - If `--emit-manifest <path>` is present, it runs the build-time manifest mode (`EmitManifest`, see below) and never enters the pipe loop.
  - Otherwise it parses the **required** `--pipe <name>` argument (missing → diagnostic to stderr + exit 1, never defaulted), builds the worker host (`WorkerHost.Create` then `BuildContainer` with an empty parameter set), and hands a `SchemaRpcTarget` to `WorkerRunner.RunPipeLoopAsync`. Returns 0 on clean completion, non-zero (with a stderr diagnostic) on any failure to build the host or run the session.
- **`WorkerRunner`** (internal) — the shared worker runtime: `ParseNamedArg` (parses `--name value`) and `RunPipeLoopAsync`. The loop opens a `NamedPipeClientStream(".", pipeName, …, TokenImpersonationLevel.Identification)`, connects (10 s timeout), attaches StreamJsonRpc (`JsonRpc.Attach(pipe, target)`), and `await rpc.Completion` until the shim closes the connection. The worker is the **pipe client**; the shim is the server. Connecting with `Identification` impersonation lets the host read (but not act as) the worker's identity to confirm same-account before sending anything across the pipe.
- **`WorkerHost`** (internal) — owns the DI container. `Create(startup, registrationProvider)` is the compile-time-bound path the generated `Main` uses (no reflection): the generated host `Main` instantiates the consumer's startup and registration provider directly and passes them in. `BuildContainer(configParameters)` runs `Ecma2Initializer` to produce the `IServiceProvider`, caching it (subsequent calls refresh the config on the existing container). It exposes typed provider resolution: `GetSchemaProvider`, `GetImportProviders`, `GetExportProviders`, `GetPasswordProvider(s)`, `GetCapabilitiesProvider`, `GetConfigParametersProvider`.
- **`DefaultConfigRegistrationProvider`** (public) — a no-op `IConfigRegistrationProvider` for consumers with no options/configuration classes needing parameter-name mapping (used by the test consumer); a production consumer with such classes gets the source-generated `Ecma2ConfigRegistrationProvider` instead.
- **`EmptyConfigParameterCollection`** (internal) — an empty `KeyedCollection<string, ConfigParameter>` keyed by parameter name, used when no parameters are available yet (e.g. at startup before the shim has supplied them).

### RPC dispatch

- **`SchemaRpcTarget`** (internal) — the JSON-RPC target object registered with StreamJsonRpc. StreamJsonRpc maps each incoming request method name to the matching public method by convention. One instance lives for the whole session. It holds one each of an `Ecma2ImportOrchestrator`, `Ecma2ExportOrchestrator`, and `Ecma2PasswordOrchestrator`, created on the corresponding `Open*` call and discarded on `Close*` (a single worker process serves one connection, hence at most one import + one export + one password session). Methods:
  - **Config UI:** `GetSchema`, `GetCapabilities`, `GetConfigParameters`, `ValidateConfigParameters` — each (re)builds the container, resolves `IConfigParameters`, and drives `Ecma2InitOrchestrator`. The real result is serialised back with `MmsPipeSerializer`.
  - **Import:** `OpenImport` (deserialises the run-step + schema, restores the inbound JSON watermark from `CustomData` if present, launches the import producer), `GetImportPage` (drains up to the negotiated page size into a serialised `CSEntryChange` list + `MoreToImport`), `CloseImport` (awaits the producer, returns the outbound watermark; idempotent).
  - **Export:** `OpenExport`, `PutExport` (deserialises the `CSEntryChange` batch, returns the per-entry `CSEntryChangeResult` list as XML), `CloseExport`.
  - **Password:** `OpenPassword`, `GetSecurityLevel` (always reports `Secure`), `SetPassword`, `ChangePassword`, `ClosePassword`. The identity arrives as a `CSEntryIdentity` XML string (not a host `CSEntry`, which has no constructible form); secrets arrive as plain string arguments, are wrapped in a `SecureString` immediately, disposed in a `finally`, and never logged or placed in an exception message.

  Every handler is wrapped in `Guard`/`GuardAsync`, which converts any thrown exception into a `LocalRpcException` whose JSON-RPC error `data` carries a faithful `MmsExceptionEnvelope` (built by `MmsExceptionEnvelopeFactory`). The shim reconstructs and re-throws the exact host exception from it, so FIM's type-driven handling fires. The handlers also guard against a null worker host and against out-of-order calls (`GetImportPage` before `OpenImport`, etc.) with clear `InvalidOperationException` messages.

  `InjectExtensionsDirectory` stores the host's `Utils.ExtensionsDirectory` — captured engine-side by the shim and passed on each `Open*` — onto the worker's `EngineServices` singleton, so providers read the *correct* directory (the worker's own `Utils` static would resolve to the wrong one). It is genuinely optional: only some MAs read it, and a null/empty value is left unset.

- **`ImportPageResult`** (internal) — the string-only transport carrier StreamJsonRpc serialises for one `GetImportPage` result: `EntriesXml` (serialised `List<CSEntryChange>`) + `MoreToImport` + `CustomData`. A thin envelope, not a structured DTO.

### Build-time manifest emission (`Manifest\`)

When invoked with `--emit-manifest`, the worker produces the FIM **Packaged-MA** manifest XML at build time — presenting the MA as a first-class packaged connector — without any IPC, MIM, or live connection. It boots the consumer's DI container, reads the real `MACapabilities` from the consumer's `ICapabilitiesProvider` (via `Ecma2InitOrchestrator.GetCapabilitiesAsync`), and writes the manifest.

- **`ManifestEmitOptions`** — the already-validated identity inputs (`OutputPath`, `ManagementAgentId` GUID, `Company`, `ListName`, `HelpText`, `ShimFileName`, `AssemblyVersion`). They originate from the MSBuild targets that own the opt-in.
- **`ManifestEmitter`** — drives the emit. It validates every required option is non-blank, requires a non-null capabilities provider, derives `capability-bits` / `capabilities-mask` / `export-type` via `CapabilityEncoder`, and builds the **complete** XML in memory before writing a single byte, so a failure never leaves a partial manifest on disk. It fails loud.
- **`ManifestIdentity`** — the identity/presentation values stamped into the manifest.
- **`PackagedMaBuilder`** — a pure XML transform that assembles the `format="Packaged"` manifest, pinned byte-for-byte against a real `mapackager` golden reference (in the Worker.Tests project). It omits the DSML `<schema>` (FIM rediscovers it live via `<rediscover-schema mode="dsml">` and the shim's `IMAExtensible2GetParametersEx`), emits empty parameter sections, sets `<supports-parameters-ex>1`, and emits the `<capabilities-mask>` (which differs from `<capability-bits>` by the export-only `ImmediateExportConfirmation` bit). The import/export page sizes (100 / 10000) are hard-coded here to match the shim's connection values — the worker cannot reference the net48 shim.
- **`CapabilityEncoder`** — reproduces FIM's derivation of `<capability-bits>` (a uint bitfield) and `<export-type>` from a `MACapabilities`, using the `[Flags] MACapabilities` enum decompiled from FIM's `mahostm.dll`, validated bit-for-bit against clean engine output and shipped manifests.

## The framework runtime it hosts (`Lithnet.Ecma2Framework`)

The worker is a thin RPC/process shell around the real framework runtime. The orchestrators and provider model live in the referenced `Lithnet.Ecma2Framework` project:

- **`IEcmaStartup`** — the consumer's hook to configure the configuration builder (`Configure`) and register services (`SetupServices`). One non-abstract implementation per consumer.
- **`Ecma2Initializer`** — builds the `IServiceProvider`: adds logging, wires the `EcmaConfigurationSource` (mapping FIM config parameters to typed options via the `IConfigRegistrationProvider`), calls the consumer's `Configure`/`SetupServices`, and registers internal services including the mutable `EngineServices` singleton (registered as both its concrete type and `IEngineServices`).
- **Provider interfaces** — `ISchemaProvider`, `IObjectImportProvider`, `IObjectExportProvider`, `IObjectPasswordProvider`, `ICapabilitiesProvider`, `IConfigParametersProvider`. The consumer implements these; the worker resolves them from the container.
- **Orchestrators** — `Ecma2InitOrchestrator` (schema / capabilities / config-parameter calls), `Ecma2ImportOrchestrator` (a producer/consumer pattern: a background producer calls each import provider per schema type and feeds a blocking collection that `GetNextPageAsync` drains by page; faults surface to the caller rather than emptying silently; watermarks serialise to JSON on close), `Ecma2ExportOrchestrator`, and `Ecma2PasswordOrchestrator`. Each orchestrator is constructed from the DI `IServiceProvider` and resolves the relevant providers.

## End-to-end request/response flow

Import as the worked example:

1. The generated `Main` calls `WorkerEntryPoint.RunAsync(startup, registrationProvider, args)`. It builds the `WorkerHost`/DI container and calls `WorkerRunner.RunPipeLoopAsync(pipeName, new SchemaRpcTarget(workerHost))`.
2. `RunPipeLoopAsync` connects the named-pipe client to the shim's pre-created server (with `Identification` impersonation), attaches StreamJsonRpc with the `SchemaRpcTarget`, and awaits the session.
3. The shim sends `OpenImport(runStepXml, schemaXml, extensionsDirectory)`. `SchemaRpcTarget.OpenImport` deserialises the real `OpenImportConnectionRunStep` and `Schema` with `MmsPipeSerializer`, injects the extensions directory onto `EngineServices`, restores the inbound watermark, constructs an `Ecma2ImportOrchestrator` from the DI container, and starts its producer.
4. The shim sends `GetImportPage` repeatedly; each call drains a page from the orchestrator and returns an `ImportPageResult` whose `EntriesXml` is a serialised `List<CSEntryChange>`.
5. The shim sends `CloseImport`; the orchestrator finishes and returns the outbound watermark JSON. The shim then disposes the session, which (via the job object) terminates this worker process.

Any provider exception along the way is caught by `GuardAsync`, serialised into the JSON-RPC error `data` as an `MmsExceptionEnvelope`, reconstructed by the shim into the exact host exception, and surfaced to FIM.

## Process lifecycle and DI container

- **Spawned per session.** The shim spawns one worker per connection and owns its lifetime via a Windows Job Object (`KILL_ON_JOB_CLOSE`). The worker does not self-terminate on a schedule; it runs the RPC serve loop until the shim closes the pipe (`rpc.Completion` resolves), then returns 0 and exits. If the shim or `miiserver` dies, the job object kills the worker — no orphans.
- **One container per process.** `WorkerHost.BuildContainer` builds the DI container once and caches it; later calls refresh the config parameters on the existing container rather than rebuilding. The container is built from the consumer's `IEcmaStartup` plus the generated `IConfigRegistrationProvider`.
- **Single-threaded dispatch.** StreamJsonRpc dispatches requests sequentially on the JSON-RPC thread, so `SchemaRpcTarget`'s per-session orchestrator state needs no locking.

## Transport (Path C)

Real `Microsoft.MetadirectoryServices` graphs cross the pipe as `MmsPipeSerializer` XML strings carried as JSON-RPC string args/results; scalars (enum names, page sizes, watermark custom-data) cross as plain JSON values. There is **no mirror/DTO object model**. `MmsPipeSerializer` (in the shared `Lithnet.Ecma2Framework.Serialization` project, referenced here **as an assembly**; the shim compiles the same sources) is a `DataContractSerializer` rooted at the real abstract type with the `*Serializable` surrogate DTOs as known types and `MmsSerializationSurrogate` attached (net8 via `SetSerializationSurrogateProvider`). The worker deserialises incoming real graphs, drives the providers with real objects, and serialises real results back — lossless, no per-field translation.

## How it is built and deployed

- **Target.** `net8.0`, `OutputType=Library`, `LangVersion 12`. References the real host MMS assembly (`Microsoft.MetadirectoryServicesEx.dll`, `Private=true`), which on net8 loads via type-forwarding facades (`NU1701`/`MSB3270` are expected and suppressed). References `Lithnet.Ecma2Framework` and `Lithnet.Ecma2Framework.Serialization`, and the `StreamJsonRpc` package.
- **Compiled into the consumer.** The shipped worker exe is the *consumer's* project built with the source-generated `Main` that calls `WorkerEntryPoint.RunAsync`. The worker's dependency DLLs are laid down as plain files alongside it (no single-file bundling).
- **Deployed to Program Files.** The worker exe and its dependencies live in Program Files. The shim finds it via the per-MA registry value `HKLM\Software\Lithnet\Ecma2\<MAName>\WorkerPath` (written by the installer) or the `LITHNET_ECMA2_WORKER_EXE` environment variable (dev/test).
- **Manifest at build.** The same worker exe, invoked with `--emit-manifest`, writes the FIM Packaged-MA manifest XML at build time (opt-in via the scaffolded MSBuild props).

## Key invariants

- **The worker holds the real MMS types and all the heavy dependencies.** Everything that would conflict inside `miiserver`'s shared AppDomain lives here, out of process.
- **Lossless real-type marshalling.** Graphs round-trip as `MmsPipeSerializer` XML; providers see real objects; no mirror types, no field-by-field translation, no substituted defaults.
- **Required inputs fail loudly.** Missing `--pipe`, a missing consumer host, a missing capabilities provider for the manifest, and out-of-order RPC calls all fail with clear diagnostics rather than defaulting.
- **Same-identity pipe.** The worker connects with `Identification`-level impersonation so the shim can confirm the worker runs as the same account before any secret-bearing call (passwords cross the pipe in plaintext at the boundary).
- **Secrets never logged.** Password plaintext is wrapped in a `SecureString` on arrival, disposed in a `finally`, and excluded from logs and exception messages; exception envelopes copy only secret-free fields.
- **Exact host exceptions cross back.** Provider failures are marshalled as `MmsExceptionEnvelope` so the shim re-throws the precise host exception type FIM expects.

## Related projects

- `Lithnet.Ecma2Framework.Shim` — the net48 DLL inside `miiserver` that spawns this worker and marshals each host call to it. See its README.
- `Lithnet.Ecma2Framework.Serialization` — the shared `MmsPipeSerializer` + surrogate + `*Serializable` DTOs (referenced here as an assembly, compiled into the shim by source).
- `Lithnet.Ecma2Framework` — the provider interfaces, DI initializer, and orchestrators this worker hosts.
