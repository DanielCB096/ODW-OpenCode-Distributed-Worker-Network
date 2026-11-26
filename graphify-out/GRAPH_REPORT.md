# Graph Report - ODW — OpenCode Distributed Worker Network  (2026-11-26)

## Corpus Check
- 203 files · ~167,556 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 4 file(s) not represented in the graph (top: (none) 2, .props 2)

## Summary
- 2563 nodes · 7502 edges · 128 communities (104 shown, 24 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 986 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- IReadOnlyList / JsonElement
- ExecutionScheduler / TestRows.NewId()
- DoctorOptions / NodeDoctor.RunAsync()
- CancellationToken / OpenCodeClient
- Odw.slnx / Odw.Node.csproj
- OpenCodeTaskRunnerIntegrationTests.Runs_a_real_task_in_an_is
- system_text / Odw.Core.Primitives
- IntPtr / WfpNetworkPolicy
- TaskStore / TestDatabase.Create()
- JsonObject / OriginTools.ProposeAsync()
- TaskHash.BuildCanonicalJson() / CanonicalJson.cs
- DateTimeOffset / SqliteConnection
- McpServer / McpServer.HandleAsync()
- JobObject / JobLimits
- Odw.Node.Tests / Odw.Node.Persistence.Tasks
- Ulid / Ulid.New()
- Fact / TaskValidator.Validate()
- OpenCodeTaskRunner.RunAsync() / OpenCodeServerOptions
- Odw.Core.Model / Odw.Core.Contracts
- NodeConfig / NodeConfig.cs
- AgentRuntime / AgentRuntimeOptions
- TaskRequest / WorkspaceSpec
- TaskTrigger / TaskStateMachine
- NodeHost.StartAsync() / SqliteDatabase
- IExecutor.cs / ExecutorEventKind
- TaskResult / TaskResult.cs
- NodeHostOptions / Accepts
- CliApplication / CliApplication.DoctorAsync()
- NodeHost.cs / system_text_json_nodes
- NodeRuntime / PeerEndpoints.CreateAsync()
- ApprovalService / ApprovalService.DecideAsync()
- Odw.Node.Persistence / system_globalization
- TaskState / TaskStateExtensions
- TimeProvider / SqliteDatabaseOptions
- xunit / Odw.Sandbox.Windows
- PolicyEngine.FromJson() / PolicyEngine.Evaluate()
- FakeOpenCodeClient / OpenCodeExecutorTests.Create()
- OpenCodeExecutor / OpenCodeExecutor.StreamAsync()
- TaskAggregate / InvalidStateTransitionException
- SandboxConfig / SandboxGuard.Validate()
- OpenCodeServer / OpenCodeServer.StartAsync()
- SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()
- CreateTaskRequest / WorkspaceMode
- SqliteWriter.WriteAsync() / OutboxStore
- PolicyWhen / PolicyModels.cs
- PeerApiClient / PeerApiClient.PostTaskAsync()
- Task / Fixture.StartAsync()
- OpenCodeTaskRunnerOptions / ToolPermissionHandler
- ModelProxyOptions / ModelProxyTests
- McpServerTests / McpServerTests.CreateServer()
- TransitionContext / TaskStateMachine.TryTransition()
- TaskRole / TaskSnapshot
- WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests
- ModelProxy / ModelProxy.HandleAsync()
- NodeHost / NodeHost.BuildPeer()
- McpHttpFixture.InitializeAsync() / TaskResultView
- Lineage / LineageGuard.Evaluate()
- TaskKind / Enums.cs
- McpHttpTests / McpHttpFixture
- ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()
- NodeConfigLoader.Load() / NodeConfigurationException
- OriginProposalService / OriginProposalService.ProposeAsync()
- WorkspaceChanges.CaptureAsync() / WorkspaceChanges
- ContractFormat / Regex
- WhenDto / LimitsDto
- TransitionTaskRequest / CancelRequested
- WfpNetworkPolicy.IsInstalled() / WfpNetworkPolicyTests.Agent
- OriginEndToEndTests / OriginEndToEndTests.StartAsync()
- TaskRequest contract / Approval binding to task_id, task_has
- IAsyncDisposable / TestDatabase
- OpenCodeClientTests.Create() / OpenCodeClientTests
- List / TaskValidator
- TelegramClient / StubHandler
- IApprovalChannel.cs / ApprovalRequest
- OpenCodeDtos.cs / OpenCodeSessionRequest
- Threat model T1-T10 / Dedicated identities odw-svc and odw-a
- OriginPoller / OriginPoller.RefreshOnceAsync()
- ApprovalsConfig / TelegramConfig
- Peer protocol v1 / ODW — OpenCode Distributed Worker Network
- Origin state machine / SQLite schema (tasks, peers, events, 
- Func / PolicyDocumentLoader.Load()
- PolicyDocumentLoader / PolicyDocumentLoader.MapWhen()
- PolicyEngine / PolicyEngine.Matches()
- Milestone plan D0 through H6 / Lineage and anti-cycle guard
- FacadeFixture / Clock
- NetworkIsolationWatchdog / CancellationTokenSource
- OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod
- PeerRegistry / PeerIdentity
- PeerApiConfig / PeerTlsConfig
- NodeConfigTests / NodeConfigTests.Bootstrap_starts_a_host_fr
- ODW Node service (one per PC) / Task Manager (state machine,
- StubHandler / HttpRequestMessage
- NodeHostedService / ServeHost.RunAsync()
- TelegramApprovalService / TelegramClient.GetUpdatesAsync()
- NodeBootstrap.StartAsync() / NodeSettings
- h0-probe.ps1 / Assert-True()
- JsonSerializerOptions / StringOrArrayConverter
- ValidationResult / ValidationResult.cs
- ConfiguredPeer / PeerFile
- Secret references and DPAPI storage / Stateless Streamable H
- ExecutionScheduler / H1 — Minimal node and basic delegation
- MigrationTests.ScalarLong() / SqliteWriterTests.Cancelled_wr
- ApprovalDecisionStatus / AlreadyDecided
- Goal / Accepted
- PeerApiClientFactory.Create() / PeerApiClientFactory
- PeerRegistration / TlsTests.Peer_api_uses_https_when_a_certi
- TaskModels.cs / TransitionResult
- RuleDto / WhenDto
- permission.asked event blocks the prompt indefinitely / Fina
- LimitsDto / RangeDto
- DocumentDto / RuleDto
- PolicyEffect / PolicyRule
- TelegramApprovalOptions / AllowedChatIds
- policy.example.json / default
- TimeWindowDto / Days
- ServeHost.cs / Odw.Cli
- PolicyDecision / IsAllowed
- ApprovalOptions / OriginProposalService.OriginProposalServic
- TaskContractWriter / TaskContractWriter.Write()
- RangeDto / Gte

## God Nodes (most connected - your core abstractions)
1. `Odw.Core.Model` - 66 edges
2. `TaskState` - 57 edges
3. `NodeHostOptions` - 54 edges
4. `TaskStore` - 50 edges
5. `TaskRequest` - 49 edges
6. `TaskRole` - 44 edges
7. `NodeHost` - 43 edges
8. `FakeOpenCodeClient` - 41 edges
9. `TaskTrigger` - 38 edges
10. `WfpNetworkPolicy` - 37 edges

## Surprising Connections (you probably didn't know these)
- `ODW Node service (one per PC)` --references--> `NodeHost with two listeners`  [INFERRED]
  ARCHITECTURE.md → docs/IMPLEMENTATION.md
- `TwoNodeHarness end-to-end tests` --semantically_similar_to--> `E2E loopback harness (tools/e2e-loopback.ps1)`  [INFERRED] [semantically similar]
  docs/TESTING.md → README.md
- `ODW — OpenCode Distributed Worker Network` --references--> `TaskRequest contract`  [INFERRED]
  ARCHITECTURE.md → docs/DOMAIN-MODEL.md
- `Odw.slnx solution and project layout` --references--> `ODW Node service (one per PC)`  [INFERRED]
  docs/IMPLEMENTATION.md → ARCHITECTURE.md
- `Local MCP facade` --references--> `odw_* MCP tool catalogue`  [INFERRED]
  ARCHITECTURE.md → docs/MCP-FACADE.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Layered execution isolation controls** — docs_security_identities, docs_security_job_object, docs_security_workspace_acl, docs_security_reparse_guard, docs_security_network_isolation, architecture_workspace_isolation [EXTRACTED 1.00]
- **ODW node internal modules** — architecture_odw_node, architecture_mcp_facade, architecture_peer_api, architecture_policy_engine, architecture_task_manager, architecture_planner, architecture_resource_monitor, architecture_sandbox, architecture_approval_gateway, architecture_audit_log, architecture_identity_trust [EXTRACTED 1.00]
- **Out-of-band human approval flow** — architecture_approval_gateway, docs_security_approval_binding, docs_security_approval_channels, docs_decisions_adr_009, docs_implementation_approval_service, docs_domain_model_task_hash [EXTRACTED 1.00]

## Communities (128 total, 24 thin omitted)

### Community 0 - "IReadOnlyList / JsonElement"
Cohesion: 0.08
Nodes (12): OpenCodeJson, OpenCodeMessage, Text, OpenCodePermissionRequest, OpenCodeProtocolException, PeerStatusSnapshot, IsOnline, TaskContractException (+4 more)

### Community 1 - "ExecutionScheduler / TestRows.NewId()"
Cohesion: 0.09
Nodes (30): TaskLimits, MaxCostUsd, MaxMemoryGb, MaxMinutes, MaxProcesses, Network, ExecutionRequest, ExecutionResult (+22 more)

### Community 2 - "DoctorOptions / NodeDoctor.RunAsync()"
Cohesion: 0.06
Nodes (24): BindPolicy, DoctorCheck, DoctorOptions, DatabasePath, ExpectedSpecSha256, McpBind, McpPort, McpToken (+16 more)

### Community 3 - "CancellationToken / OpenCodeClient"
Cohesion: 0.08
Nodes (15): ApprovalPipeClient, ExecutorOutcome, ExecutorSnapshot, IExecutor, IOpenCodeClient, BaseAddress, OpenCodeClient, BaseAddress (+7 more)

### Community 4 - "Odw.slnx / Odw.Node.csproj"
Cohesion: 0.06
Nodes (38): Microsoft.Data.Sqlite, Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.WindowsServices, System.Security.Cryptography.ProtectedData, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+30 more)

### Community 5 - "OpenCodeTaskRunnerIntegrationTests.Runs_a_real_task_in_an_is"
Cohesion: 0.11
Nodes (20): WorkspaceManager, WorkspaceOptions, AgentAccount, ApplyAcls, GitExecutable, GitTimeout, KeepWorkspaces, Repos (+12 more)

### Community 6 - "system_text / Odw.Core.Primitives"
Cohesion: 0.07
Nodes (6): Odw.Executors.OpenCode, Odw.Approvals, Odw.Node.Diagnostics, Odw.Core.Primitives, TestModels, FreeOrFallback

### Community 7 - "IntPtr / WfpNetworkPolicy"
Cohesion: 0.13
Nodes (11): WfpNetworkPolicy, DefaultStatePath, FwpAction0, FwpByteBlob, FwpConditionValue0, FwpmDisplayData0, FwpmFilter0, FwpmFilterCondition0 (+3 more)

### Community 8 - "TaskStore / TestDatabase.Create()"
Cohesion: 0.18
Nodes (7): StartupRecovery, TaskStore, TaskCoordinator, ContractModels, RecoveryTests, TaskCoordinatorTests, TaskStoreTests

### Community 9 - "JsonObject / OriginTools.ProposeAsync()"
Cohesion: 0.13
Nodes (6): IPeerDirectory, McpToolResult, JsonTools, OriginTools, PeerTools, TaskTools

### Community 10 - "TaskHash.BuildCanonicalJson() / CanonicalJson.cs"
Cohesion: 0.11
Nodes (17): CanonicalArray, Count, this[], CanonicalBoolean, Value, CanonicalJson, CanonicalNode, CanonicalNull (+9 more)

### Community 11 - "DateTimeOffset / SqliteConnection"
Cohesion: 0.15
Nodes (7): AuditLog, ChainHash, ChainVerification, Valid, RecoveryResult, TaskEventLog, TaskEventRecord

### Community 12 - "McpServer / McpServer.HandleAsync()"
Cohesion: 0.11
Nodes (14): JsonRpc, ErrorCodes, McpEndpoint, McpOptions, MaxBodyBytes, Path, Token, McpServer (+6 more)

### Community 13 - "JobObject / JobLimits"
Cohesion: 0.09
Nodes (10): JobLimits, ActiveProcessLimit, CpuTime, JobMemoryBytes, ProcessMemoryBytes, JobObject, IoCounters, JobObjectBasicLimitInformation (+2 more)

### Community 14 - "Odw.Node.Tests / Odw.Node.Persistence.Tasks"
Cohesion: 0.13
Nodes (14): Odw.Node.Scheduling, Odw.PeerApi, Odw.Node.Origins, Odw.Node.Tests, Odw.Node.Approvals, Odw.Node.Hosting, Odw.Node.Tasks, Odw.Node.Persistence.Tasks (+6 more)

### Community 15 - "Ulid / Ulid.New()"
Cohesion: 0.10
Nodes (6): Ulid, IsEmpty, MonotonicState, Timestamp, UlidTests, FixedTimeProvider

### Community 16 - "Fact / TaskValidator.Validate()"
Cohesion: 0.18
Nodes (4): TaskHashTests, TaskValidatorTests, TestTasks, TaskContractReaderTests

### Community 17 - "OpenCodeTaskRunner.RunAsync() / OpenCodeServerOptions"
Cohesion: 0.11
Nodes (17): ExecutorLimits, ExecutorSession, ExecutorStartRequest, OpenCodeServerOptions, AttachProcess, Environment, Executable, Log (+9 more)

### Community 18 - "Odw.Core.Model / Odw.Core.Contracts"
Cohesion: 0.11
Nodes (10): Odw.Core.Validation, Odw.Core.Contracts, Odw.Node.Contracts, Odw.Core.Hashing, Odw.Core.Tests, Odw.Policy.Tests, Odw.Core.Delegation, Odw.Node.PeerApi (+2 more)

### Community 19 - "NodeConfig / NodeConfig.cs"
Cohesion: 0.07
Nodes (33): McpConfig, Bind, Port, TokenRef, NodeConfig, Accepts, Approvals, Capabilities (+25 more)

### Community 20 - "AgentRuntime / AgentRuntimeOptions"
Cohesion: 0.10
Nodes (12): AgentCredentials, AgentCredentialsStore, AgentRuntime, Path, AgentRuntimeOptions, AgentAccount, ApplyAcls, ConfigTemplate (+4 more)

### Community 21 - "TaskRequest / WorkspaceSpec"
Cohesion: 0.07
Nodes (32): ApprovalEvidence, ApprovalId, ApprovedAt, ApprovedBy, Nonce, Signature, TaskHash, ExecutorSpec (+24 more)

### Community 22 - "TaskTrigger / TaskStateMachine"
Cohesion: 0.06
Nodes (28): TaskTrigger, ApprovalDenied, ApprovalExpired, ApprovalGranted, CancelTimeout, LeaseExpired, OperatorDiscard, OperatorInspect (+20 more)

### Community 23 - "NodeHost.StartAsync() / SqliteDatabase"
Cohesion: 0.09
Nodes (14): ApprovalCoordinator, PeerStore, SqliteDatabase, Path, SchemaVersion, Writer, SqliteWriter, CommittedCount (+6 more)

### Community 24 - "IExecutor.cs / ExecutorEventKind"
Cohesion: 0.07
Nodes (26): ExecutorEvent, ExecutorEventKind, Completed, Failed, Heartbeat, Interrupted, PermissionAsked, Started (+18 more)

### Community 25 - "TaskResult / TaskResult.cs"
Cohesion: 0.08
Nodes (25): TestStatus, Failed, Passed, Skipped, ArtifactRef, TaskError, TaskResult, Artifacts (+17 more)

### Community 26 - "NodeHostOptions / Accepts"
Cohesion: 0.07
Nodes (30): NodeHostOptions, Accepts, AllowLoopbackPeerBind, ApprovalOptions, ApprovalPipeName, ApprovalPipeSecurityDescriptor, ApprovalSigner, Capabilities (+22 more)

### Community 28 - "NodeHost.cs / system_text_json_nodes"
Cohesion: 0.13
Nodes (6): Odw.Mcp.Tests, Odw.Mcp.Abstractions, Odw.Node.Persistence.Peers, Odw.Mcp, Odw.Mcp.Tools, Odw.Node.Persistence.Queries

### Community 29 - "NodeRuntime / PeerEndpoints.CreateAsync()"
Cohesion: 0.15
Nodes (13): NodeRuntime, Coordinator, Database, Directory, Options, Outbox, Peers, Policy (+5 more)

### Community 30 - "ApprovalService / ApprovalService.DecideAsync()"
Cohesion: 0.18
Nodes (5): ApprovalDecisionResult, ApprovalRecord, ApprovalService, ApprovalSigner, ApprovalServiceTests

### Community 31 - "Odw.Node.Persistence / system_globalization"
Cohesion: 0.13
Nodes (5): Odw.Node.Persistence.Outbox, Odw.Node.Persistence, ApprovalStatus, OutboxActions, OutboxStatus

### Community 32 - "TaskState / TaskStateExtensions"
Cohesion: 0.10
Nodes (21): TaskStateExtensions, TaskState, Accepted, Approved, AwaitingLocalApproval, Cancelled, Cancelling, Completed (+13 more)

### Community 33 - "TimeProvider / SqliteDatabaseOptions"
Cohesion: 0.13
Nodes (12): DatabaseMigrator, Migration, MigrationOutcome, SqliteDatabaseOptions, AppVersion, MigrationBackupDirectory, Pooling, TimeProvider (+4 more)

### Community 34 - "xunit / Odw.Sandbox.Windows"
Cohesion: 0.16
Nodes (4): Odw.Sandbox.Workspaces, Odw.Sandbox.Tests, Odw.Sandbox.Windows, Odw.Sandbox.Network

### Community 35 - "PolicyEngine.FromJson() / PolicyEngine.Evaluate()"
Cohesion: 0.27
Nodes (4): PolicyRequest, Actions, PolicyEngineTests, PolicyFixtures

### Community 36 - "FakeOpenCodeClient / OpenCodeExecutorTests.Create()"
Cohesion: 0.16
Nodes (13): OpenCodeExecutorTests, EventFactory, FakeOpenCodeClient, BaseAddress, BlockAfterEvents, Events, Info, Interrupts (+5 more)

### Community 37 - "OpenCodeExecutor / OpenCodeExecutor.StreamAsync()"
Cohesion: 0.14
Nodes (10): OpenCodeEvent, OpenCodeApiException, Body, Method, Path, StatusCode, OpenCodeExecutor, ToolPermissionDecision (+2 more)

### Community 38 - "TaskAggregate / InvalidStateTransitionException"
Cohesion: 0.10
Nodes (15): TaskAggregate, Changes, CreatedAt, Role, State, StateSeq, TaskHash, TaskId (+7 more)

### Community 39 - "SandboxConfig / SandboxGuard.Validate()"
Cohesion: 0.12
Nodes (21): SandboxConfig, AgentAccount, AgentConfigPath, AgentSecretPath, ApplyAcls, NetworkAllowedHosts, NetworkIsolation, NetworkProxyPort (+13 more)

### Community 40 - "OpenCodeServer / OpenCodeServer.StartAsync()"
Cohesion: 0.14
Nodes (6): OpenCodeConnection, OpenCodeServer, BaseUrl, Connection, Info, ProcessId

### Community 41 - "SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()"
Cohesion: 0.15
Nodes (8): TaskOutcome, Cancelled, Failed, ReviewRequired, Succeeded, SqliteTaskQuery, ResultDetails, SqliteFacadeTests

### Community 42 - "CreateTaskRequest / WorkspaceMode"
Cohesion: 0.08
Nodes (23): WorkspaceMode, ReadOnly, ReadWriteWorktree, CreateTaskRequest, CounterpartNode, CreatedAt, ExecutorRef, ExpiresAt (+15 more)

### Community 43 - "SqliteWriter.WriteAsync() / OutboxStore"
Cohesion: 0.24
Nodes (8): NewPendingAction, OutboxFailOutcome, DeadLettered, NotFound, RetryScheduled, OutboxStore, MaxAttempts, OutboxTests

### Community 44 - "PolicyWhen / PolicyModels.cs"
Cohesion: 0.09
Nodes (20): LimitsWhen, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network, PolicyWhen, AnyAction, CapabilitiesAll (+12 more)

### Community 45 - "PeerApiClient / PeerApiClient.PostTaskAsync()"
Cohesion: 0.14
Nodes (7): PeerApiClient, BaseAddress, PeerPostResult, Accepted, PeerProtocolException, PeerResultView, PeerTaskView

### Community 47 - "OpenCodeTaskRunnerOptions / ToolPermissionHandler"
Cohesion: 0.09
Nodes (21): ToolPermissionHandler, OpenCodeTaskRunnerOptions, AgentAccount, AgentConfigTemplate, AgentRuntimeRoot, Executable, ExecutionTimeout, ModelProxyUrl (+13 more)

### Community 48 - "ModelProxyOptions / ModelProxyTests"
Cohesion: 0.18
Nodes (11): ModelProxyOptions, AddressFilter, AllowedHosts, AllowedPorts, AuthToken, ConnectTimeout, Log, MaxRequestBytes (+3 more)

### Community 49 - "McpServerTests / McpServerTests.CreateServer()"
Cohesion: 0.24
Nodes (6): McpServerTests, FakePeers, Peers, FakeTasks, Results, Statuses

### Community 50 - "TransitionContext / TaskStateMachine.TryTransition()"
Cohesion: 0.13
Nodes (9): TransitionContext, ApprovalExpired, ApprovalMatches, Default, LeaseExpired, ResourcesAvailable, Satisfied, ValidationViolated (+1 more)

### Community 51 - "TaskRole / TaskSnapshot"
Cohesion: 0.14
Nodes (9): TaskRole, Origin, Target, PendingAction, OutboxSql, PersistenceJson, Options, TaskSnapshot (+1 more)

### Community 52 - "WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests"
Cohesion: 0.18
Nodes (4): WorkspaceGuard, ByHandleFileInformation, WorkspaceGuardException, WorkspaceGuardTests

### Community 53 - "ModelProxy / ModelProxy.HandleAsync()"
Cohesion: 0.17
Nodes (4): ModelProxy, Port, Token, Url

### Community 54 - "NodeHost / NodeHost.BuildPeer()"
Cohesion: 0.13
Nodes (12): NodeHost, ApprovalCoordinator, Approvals, Coordinator, McpBaseAddress, Options, PeerBaseAddress, Peers (+4 more)

### Community 55 - "McpHttpFixture.InitializeAsync() / TaskResultView"
Cohesion: 0.15
Nodes (5): ITaskQuery, TaskEventView, TaskResultView, TaskStatusView, McpTestData

### Community 56 - "Lineage / LineageGuard.Evaluate()"
Cohesion: 0.18
Nodes (11): LineageCheck, DepthExceeded, Invalid, Loop, Ok, SelfDelegation, LineageGuard, Lineage (+3 more)

### Community 57 - "TaskKind / Enums.cs"
Cohesion: 0.11
Nodes (14): MessageDelivery, Queue, Steer, NetworkAccess, Allow, None, TaskKind, Analysis (+6 more)

### Community 58 - "McpHttpTests / McpHttpFixture"
Cohesion: 0.18
Nodes (3): McpHttpFixture, Client, McpHttpTests

### Community 59 - "ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()"
Cohesion: 0.20
Nodes (5): ApprovalPipeOptions, Name, SecurityDescriptor, ApprovalPipeServer, PipeName

### Community 60 - "NodeConfigLoader.Load() / NodeConfigurationException"
Cohesion: 0.33
Nodes (3): NodeConfigLoader, TelegramSettings, NodeConfigurationException

### Community 61 - "OriginProposalService / OriginProposalService.ProposeAsync()"
Cohesion: 0.28
Nodes (5): ITaskProposalService, ProposalInput, ProposalOutcome, IsError, OriginProposalService

### Community 62 - "WorkspaceChanges.CaptureAsync() / WorkspaceChanges"
Cohesion: 0.26
Nodes (3): WorkspaceChanges, WorkspaceChangeSet, WorkspaceChangesTests

### Community 64 - "WhenDto / LimitsDto"
Cohesion: 0.13
Nodes (13): WhenDto, AnyAction, CapabilitiesAll, CapabilitiesNone, Direction, Kind, Limits, Mode (+5 more)

### Community 65 - "TransitionTaskRequest / CancelRequested"
Cohesion: 0.13
Nodes (15): TransitionTaskRequest, CancelRequested, EventPayloadJson, EventType, ExecutorRef, ExpectedSeq, ExpectedState, FinishedAt (+7 more)

### Community 66 - "WfpNetworkPolicy.IsInstalled() / WfpNetworkPolicyTests.Agent"
Cohesion: 0.17
Nodes (6): SandboxInspector, SandboxReport, FullyEnforced, NetworkEnforced, SandboxInspectorTests, WfpNetworkPolicyTests

### Community 68 - "TaskRequest contract / Approval binding to task_id, task_has"
Cohesion: 0.19
Nodes (11): Approval Gateway (out-of-band), Policy Engine (AUTO / ASK / DENY), Identifier formats (node_id, ULID task_id), task_hash canonical projection (RFC 8785 JCS), TaskRequest contract, ApprovalService, pipe server and signer, odw_* MCP tool catalogue, Policy Engine semantics (first match, fail-closed) (+3 more)

### Community 69 - "IAsyncDisposable / TestDatabase"
Cohesion: 0.15
Nodes (8): Fixture, Folder, Host, TestDatabase, Database, Folder, Path, Time

### Community 72 - "TelegramClient / StubHandler"
Cohesion: 0.33
Nodes (4): TelegramClient, TelegramClientTests, StubHandler, Calls

### Community 73 - "IApprovalChannel.cs / ApprovalRequest"
Cohesion: 0.19
Nodes (10): ApprovalDecisionKind, Approved, Denied, Expired, ApprovalOutcome, ApprovalRequest, ApprovalScope, Task (+2 more)

### Community 74 - "OpenCodeDtos.cs / OpenCodeSessionRequest"
Cohesion: 0.22
Nodes (7): OpenCodePermissionEffect, Allow, Ask, Deny, OpenCodePermissionRule, EffectValue, OpenCodeSessionRequest

### Community 75 - "Threat model T1-T10 / Dedicated identities odw-svc and odw-a"
Cohesion: 0.27
Nodes (11): Sandbox module, Isolated workspace per task, H3 — Isolation and policies (done), Dedicated identities odw-svc and odw-agent, Job Object resource limits, Network isolation: WFP by SID plus CONNECT proxy, Reparse point and hardlink guard, Threat model T1-T10 (+3 more)

### Community 76 - "OriginPoller / OriginPoller.RefreshOnceAsync()"
Cohesion: 0.29
Nodes (3): OriginPoller, OriginPollerOptions, PollInterval

### Community 78 - "ApprovalsConfig / TelegramConfig"
Cohesion: 0.15
Nodes (13): ApprovalsConfig, Enabled, HmacKeyRef, PipeName, SecurityDescriptor, Telegram, TtlMinutes, TelegramConfig (+5 more)

### Community 79 - "Peer protocol v1 / ODW — OpenCode Distributed Worker Network"
Cohesion: 0.17
Nodes (9): Two interfaces: local MCP facade and peer API, Local MCP facade, ODW — OpenCode Distributed Worker Network, Peer API (node to node), OpenCode SSE: data-only lines, no Last-Event-ID, NodeHost with two listeners, RFC 7807 error catalogue, Peer protocol v1 (+1 more)

### Community 80 - "Origin state machine / SQLite schema (tasks, peers, events, "
Cohesion: 0.20
Nodes (10): Origin state machine, Target state machine, TaskState enum (single enum, per-role subsets), OriginProposalService and OriginPoller, Embedded versioned migrations, Outbox (pending_actions) with backoff and dead-letter, SQLite schema (tasks, peers, events, approvals), SqliteWriter single-writer actor (+2 more)

### Community 81 - "Func / PolicyDocumentLoader.Load()"
Cohesion: 0.26
Nodes (3): PolicyDocument, PolicyValidationException, Issues

### Community 83 - "PolicyEngine / PolicyEngine.Matches()"
Cohesion: 0.24
Nodes (3): IPolicyEngine, PolicyEngine, Document

### Community 85 - "Milestone plan D0 through H6 / Lineage and anti-cycle guard"
Cohesion: 0.22
Nodes (11): Planner (delegation scoring), Lineage and anti-cycle guard, Task divisibility via declared split_units, Deterministic planner scoring formula, Leases and heartbeats, D0 — Detailed design (done), H0 — OpenCode verification (partial, H0-6 open), H4 — Full bidirectionality (+3 more)

### Community 86 - "FacadeFixture / Clock"
Cohesion: 0.22
Nodes (6): Clock, Now, FacadeFixture, Clock, Database, Folder

### Community 87 - "NetworkIsolationWatchdog / CancellationTokenSource"
Cohesion: 0.29
Nodes (3): NetworkIsolationWatchdog, Lost, NetworkIsolationWatchdogTests

### Community 88 - "OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod"
Cohesion: 0.27
Nodes (8): OpenAPI spec served at GET /openapi.json, pinned by SHA-256, Port 0 plus /api/info pid and urls discovery, Prompt endpoint POST /api/session/{id}/prompt, OpenCode v2.3.2 V2 surface under /api (143 routes), odw doctor diagnostics, OpenCodeClient typed V2 HTTP client, OpenCodeServer launcher (loopback, port 0), odw CLI (serve, doctor, approvals, service)

### Community 89 - "PeerRegistry / PeerIdentity"
Cohesion: 0.31
Nodes (4): PeerIdentity, PeerRegistry, Entry, KnownNodes

### Community 90 - "PeerApiConfig / PeerTlsConfig"
Cohesion: 0.20
Nodes (10): PeerApiConfig, Address, AllowLoopback, Bind, Port, Tls, PeerTlsConfig, CertificatePath (+2 more)

### Community 92 - "ODW Node service (one per PC) / Task Manager (state machine,"
Cohesion: 0.22
Nodes (8): Audit Log (append-only), Identity and Trust module, ODW Node service (one per PC), Resource Monitor (CPU, RAM, GPU, tools), Task Manager (state machine, leases, recovery), Odw.slnx solution and project layout, IExecutor executor port, Hash-chained append-only audit log

### Community 93 - "StubHandler / HttpRequestMessage"
Cohesion: 0.31
Nodes (3): RecordedCall, StubHandler, Calls

### Community 94 - "NodeHostedService / ServeHost.RunAsync()"
Cohesion: 0.28
Nodes (3): NodeHostedService, ServeHost, ServeOptions

### Community 98 - "h0-probe.ps1 / Assert-True()"
Cohesion: 0.28
Nodes (3): Assert-True(), Write-Fail(), Write-Pass()

### Community 100 - "ValidationResult / ValidationResult.cs"
Cohesion: 0.25
Nodes (6): ValidationCodes, ValidationResult, Codes, Issues, IsValid, Valid

### Community 101 - "ConfiguredPeer / PeerFile"
Cohesion: 0.25
Nodes (8): ConfiguredPeer, Address, Capabilities, NodeId, TokenRef, Trust, PeerFile, Peers

### Community 102 - "Secret references and DPAPI storage / Stateless Streamable H"
Cohesion: 0.33
Nodes (6): MCP server config under mcp.servers with oauth false, node.json configuration and secret references, Stateless Streamable HTTP MCP transport, policy.json document and condition set, Peer auth: Bearer token plus X-Odw-Node, Secret references and DPAPI storage

### Community 103 - "ExecutionScheduler / H1 — Minimal node and basic delegation"
Cohesion: 0.38
Nodes (6): ExecutionScheduler, H1 — Minimal node and basic delegation, Architecture invariant tests, Testing strategy and golden rules, TwoNodeHarness end-to-end tests, E2E loopback harness (tools/e2e-loopback.ps1)

### Community 105 - "ApprovalDecisionStatus / AlreadyDecided"
Cohesion: 0.29
Nodes (7): ApprovalDecisionStatus, AlreadyDecided, Approved, Denied, Expired, HashMismatch, NotFound

### Community 106 - "Goal / Accepted"
Cohesion: 0.29
Nodes (7): Goal, Accepted, Cancelled, Done, Failed, Rejected, Running

### Community 107 - "PeerApiClientFactory.Create() / PeerApiClientFactory"
Cohesion: 0.38
Nodes (3): OriginException, PeerApiClientFactory, PeerConnection

### Community 109 - "TaskModels.cs / TransitionResult"
Cohesion: 0.33
Nodes (5): TransitionResult, TransitionStatus, Applied, Conflict, NotFound

### Community 110 - "RuleDto / WhenDto"
Cohesion: 0.29
Nodes (6): RuleDto, Effect, Id, Priority, Reason, When

### Community 112 - "permission.asked event blocks the prompt indefinitely / Fina"
Cohesion: 0.40
Nodes (4): TaskResult contract, permission.asked event blocks the prompt indefinitely, Final verification and evidence-based outcome, Permission broker (once or reject, never always)

### Community 113 - "LimitsDto / RangeDto"
Cohesion: 0.33
Nodes (5): LimitsDto, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network

### Community 114 - "DocumentDto / RuleDto"
Cohesion: 0.33
Nodes (5): DocumentDto, Default, Rules, Schema, Version

### Community 115 - "PolicyEffect / PolicyRule"
Cohesion: 0.33
Nodes (5): PolicyEffect, Ask, Auto, Deny, PolicyRule

### Community 116 - "TelegramApprovalOptions / AllowedChatIds"
Cohesion: 0.33
Nodes (6): TelegramApprovalOptions, AllowedChatIds, AllowedUserIds, BotToken, NotifyInterval, PollTimeoutSeconds

### Community 117 - "policy.example.json / default"
Cohesion: 0.40
Nodes (4): default, rules, $schema, version

### Community 118 - "TimeWindowDto / Days"
Cohesion: 0.40
Nodes (5): TimeWindowDto, Days, From, TimeZone, To

### Community 122 - "PolicyDecision / IsAllowed"
Cohesion: 0.50
Nodes (3): PolicyDecision, IsAllowed, RequiresHuman

### Community 126 - "RangeDto / Gte"
Cohesion: 0.67
Nodes (3): RangeDto, Gte, Lte

## Knowledge Gaps
- **630 isolated node(s):** `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.NET.Sdk`, `Task` (+625 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 807 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **24 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Odw.Core.Model` connect `Odw.Core.Model / Odw.Core.Contracts` to `xunit / Odw.Sandbox.Windows`, `system_text / Odw.Core.Primitives`, `IApprovalChannel.cs / ApprovalRequest`, `TaskModels.cs / TransitionResult`, `Odw.Node.Tests / Odw.Node.Persistence.Tasks`, `TaskRequest / WorkspaceSpec`, `TaskTrigger / TaskStateMachine`, `McpHttpFixture.InitializeAsync() / TaskResultView`, `IExecutor.cs / ExecutorEventKind`, `TaskKind / Enums.cs`, `NodeHost.cs / system_text_json_nodes`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `TaskResult / TaskResult.cs`, `Odw.Node.Persistence / system_globalization`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **What connects `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices` to the rest of the system?**
  _630 weakly-connected nodes found - possible missing edges._
- **Should `IReadOnlyList / JsonElement` be split into smaller, more focused modules?**
  _Cohesion score 0.08074534161490683 - nodes in this community are weakly interconnected._
- **Why does `PolicyDocumentLoader` connect `PolicyDocumentLoader / PolicyDocumentLoader.MapWhen()` to `WhenDto / LimitsDto`, `JsonSerializerOptions / StringOrArrayConverter`, `system_text / Odw.Core.Primitives`, `RuleDto / WhenDto`, `LimitsDto / RangeDto`, `DocumentDto / RuleDto`, `Func / PolicyDocumentLoader.Load()`, `TimeWindowDto / Days`, `RangeDto / Gte`, `ContractFormat / Regex`?**
  _High betweenness centrality (0.040) - this node is a cross-community bridge._
- **Should `ExecutionScheduler / TestRows.NewId()` be split into smaller, more focused modules?**
  _Cohesion score 0.09077380952380952 - nodes in this community are weakly interconnected._
- **Why does `NodeHostOptions` connect `NodeHostOptions / Accepts` to `IReadOnlyList / JsonElement`, `ExecutionScheduler / TestRows.NewId()`, `DoctorOptions / NodeDoctor.RunAsync()`, `OpenCodeTaskRunnerIntegrationTests.Runs_a_real_task_in_an_is`, `Odw.Node.Tests / Odw.Node.Persistence.Tasks`, `NodeHost.StartAsync() / SqliteDatabase`, `NodeRuntime / PeerEndpoints.CreateAsync()`, `ApprovalService / ApprovalService.DecideAsync()`, `TimeProvider / SqliteDatabaseOptions`, `Task / Fixture.StartAsync()`, `ModelProxy / ModelProxy.HandleAsync()`, `NodeHost / NodeHost.BuildPeer()`, `NodeConfigLoader.Load() / NodeConfigurationException`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `OriginPoller / OriginPoller.RefreshOnceAsync()`, `PolicyEngine / PolicyEngine.Matches()`, `NodeBootstrap.StartAsync() / NodeSettings`, `PeerRegistration / TlsTests.Peer_api_uses_https_when_a_certi`, `TelegramApprovalOptions / AllowedChatIds`, `ApprovalOptions / OriginProposalService.OriginProposalServic`?**
  _High betweenness centrality (0.032) - this node is a cross-community bridge._
- **Should `DoctorOptions / NodeDoctor.RunAsync()` be split into smaller, more focused modules?**
  _Cohesion score 0.06174863387978142 - nodes in this community are weakly interconnected._