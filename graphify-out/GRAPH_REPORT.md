# Graph Report - ODW — OpenCode Distributed Worker Network  (2026-11-26)

## Corpus Check
- 204 files · ~168,677 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 4 file(s) not represented in the graph (top: (none) 2, .props 2)

## Summary
- 2569 nodes · 7509 edges · 131 communities (111 shown, 20 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 986 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Task / ExecutionScheduler
- OpenCodeClientTests.Create() / PeerApiClient
- Odw.slnx / Odw.Node.csproj
- IntPtr / WfpNetworkPolicy
- JsonElement / TaskContractReader
- FakeOpenCodeClient / OpenCodeExecutor.StreamAsync()
- Odw.Core.Model / Odw.Node.Persistence.Tasks
- SqliteWriter.WriteAsync() / SqliteDatabase
- TaskHash.BuildCanonicalJson() / CanonicalJson.cs
- CreateTaskRequest / TaskKind
- NodeHostOptions / TelegramApprovalOptions
- OpenCodeTaskRunner.RunAsync() / OpenCodeExecutor
- system_text / Odw.Core.Primitives
- JsonObject / OriginTools.ProposeAsync()
- AgentRuntime / AgentRuntimeOptions
- DateTimeOffset / SqliteConnection
- OpenCodeClient.SendAsync() / OpenCodeDtos.cs
- Ulid / Ulid.New()
- Fact / TaskValidator.Validate()
- TaskRole / TaskCoordinator.TransitionAsync()
- NodeConfig / NodeConfig.cs
- CancellationToken / WorkspaceManager.PrepareAsync()
- TaskRequest / WorkspaceSpec
- Odw.Node.Persistence / Odw.Node.Tests
- SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()
- TaskStore / TestDatabase.Create()
- NodeHost.cs / system_text_json_nodes
- TaskState / TaskStateExtensions
- ApprovalService / ApprovalService.DecideAsync()
- OpenCodeServer / OpenCodeServerOptions
- CliApplication / CliApplication.DoctorAsync()
- NodeRuntime / PeerEndpoints.CreateAsync()
- JobObject / JobLimits
- OutboxStore / NewPendingAction
- DatabaseMigrator.Migrate() / MigrationTests.Failed_migration
- IReadOnlyList / OpenCodeTaskRunner
- PolicyEngine.FromJson() / PolicyEngine.Evaluate()
- TaskTrigger / TaskTrigger.cs
- List / TaskValidator
- McpServer / McpServer.HandleAsync()
- TaskResult / TaskResult.cs
- OriginProposalService / OriginProposalService.ProposeAsync()
- SandboxConfig / SandboxGuard.Validate()
- WorkspaceOptions / WorkspaceRequest
- PolicyWhen / PolicyModels.cs
- xunit / Odw.Sandbox.Windows
- TransitionContext / TaskStateMachine.TryTransition()
- OpenCodeTaskRunnerOptions / ToolPermissionHandler
- ModelProxyOptions / ModelProxyTests
- McpServerTests / McpServerTests.CreateServer()
- WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests
- Lineage / LineageGuard.Evaluate()
- ModelProxy / ModelProxy.HandleAsync()
- NodeConfigLoader.Load() / NodeConfigurationException
- NodeDoctor / DoctorCheck
- NodeHost / NodeHost.BuildMcp()
- OpenCodeClient / IOpenCodeClient
- TaskLimits / OpenCodeTaskRunnerGuardTests.CreateAsync()
- Odw.Core.Contracts / NodeBootstrap.cs
- ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()
- TimeSpan / ExecutorOutcomeKind
- WorkspaceChanges.CaptureAsync() / WorkspaceChanges
- Odw.Node.Hosting / CliApplication.cs
- McpHttpFixture.InitializeAsync() / TaskStatusView
- WhenDto / LimitsDto
- TaskAggregate / TaskAggregate.Create()
- TransitionTaskRequest / CancelRequested
- TaskRequest contract / Approval binding to task_id, task_has
- NodeHost.StartAsync() / TelegramApprovalService
- IAsyncDisposable / TestDatabase
- Theory / InlineData
- Enums.cs / NetworkAccess
- Threat model T1-T10 / Dedicated identities odw-svc and odw-a
- OriginPoller / OriginPoller.RefreshOnceAsync()
- ApprovalsConfig / TelegramConfig
- Peer protocol v1 / ODW — OpenCode Distributed Worker Network
- Origin state machine / SQLite schema (tasks, peers, events, 
- Func / PolicyDocumentLoader.Load()
- PolicyDocumentLoader / PolicyDocumentLoader.MapWhen()
- IEnumerable / JsonTools.ToArray()
- PolicyEngine / PolicyEngine.Matches()
- IOpenCodeClient.ReplyPermissionAsync() / OpenCodePermissionR
- DoctorOptions / DatabasePath
- Milestone plan D0 through H6 / Lineage and anti-cycle guard
- ApprovalPipeServer.cs / Odw.Node.Approvals
- ExecutorEventKind / Completed
- OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod
- PeerRegistry / PeerIdentity
- InvalidStateTransitionException / TaskAggregate.Apply()
- PeerApiConfig / PeerTlsConfig
- NodeDoctor.RunAsync() / DoctorTests
- SandboxInspector.Inspect() / SandboxReport
- NodeConfigTests / NodeConfigTests.Bootstrap_starts_a_host_fr
- ODW Node service (one per PC) / Task Manager (state machine,
- NodeHostedService / ServeHost.RunAsync()
- IApprovalChannel.cs / ApprovalDecisionKind
- NodeBootstrap.StartAsync() / NodeSettings
- NetworkIsolationWatchdog / NetworkIsolationWatchdogTests.Sto
- h0-probe.ps1 / Assert-True()
- JsonSerializerOptions / StringOrArrayConverter
- ValidationResult / ValidationResult.cs
- ConfiguredPeer / PeerFile
- Secret references and DPAPI storage / Stateless Streamable H
- ExecutionScheduler / H1 — Minimal node and basic delegation
- ApprovalDecisionStatus / AlreadyDecided
- Goal / Accepted
- TaskModels.cs / TransitionResult
- RuleDto / WhenDto
- permission.asked event blocks the prompt indefinitely / Fina
- LimitsDto / RangeDto
- DocumentDto / RuleDto
- PolicyEffect / PolicyRule
- origin-node.ps1 / Get-OriginProcesses()
- policy.example.json / default
- ApprovalRequest / ApprovalScope
- DoctorReport / DoctorReport.DoctorReport()
- TimeWindowDto / Days
- PolicyDecision / IsAllowed
- DoctorStatus / Fail
- RangeDto / Gte
- WfpNetworkPolicyTests.Agent_is_blocked_from_internet_but_can

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

## Communities (131 total, 20 thin omitted)

### Community 0 - "Task / ExecutionScheduler"
Cohesion: 0.06
Nodes (27): PeerRegistration, ExecutionResult, ExecutionSchedulerOptions, MaxConcurrentTasks, PollInterval, ITaskExecutionRunner, ExecutionScheduler, TransitionOptions (+19 more)

### Community 1 - "OpenCodeClientTests.Create() / PeerApiClient"
Cohesion: 0.06
Nodes (19): TelegramCallback, TelegramClient, OriginException, PeerApiClient, BaseAddress, PeerConnection, PeerPostResult, Accepted (+11 more)

### Community 2 - "Odw.slnx / Odw.Node.csproj"
Cohesion: 0.06
Nodes (38): Microsoft.Data.Sqlite, Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.WindowsServices, System.Security.Cryptography.ProtectedData, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+30 more)

### Community 3 - "IntPtr / WfpNetworkPolicy"
Cohesion: 0.12
Nodes (11): WfpNetworkPolicy, DefaultStatePath, FwpAction0, FwpByteBlob, FwpConditionValue0, FwpmDisplayData0, FwpmFilter0, FwpmFilterCondition0 (+3 more)

### Community 4 - "JsonElement / TaskContractReader"
Cohesion: 0.13
Nodes (7): PeerStatusSnapshot, IsOnline, TaskContractException, Field, TaskContractReader, SqlitePeerDirectory, Details

### Community 5 - "FakeOpenCodeClient / OpenCodeExecutor.StreamAsync()"
Cohesion: 0.10
Nodes (15): ExecutorEvent, OpenCodeEvent, OpenCodeExecutorTests, EventFactory, FakeOpenCodeClient, BaseAddress, BlockAfterEvents, Events (+7 more)

### Community 6 - "Odw.Core.Model / Odw.Node.Persistence.Tasks"
Cohesion: 0.11
Nodes (12): Odw.Core.Validation, Odw.Node.Scheduling, Odw.PeerApi, Odw.Node.Contracts, Odw.Core.Hashing, Odw.Core.Tests, Odw.Node.Origins, Odw.Core.Delegation (+4 more)

### Community 7 - "SqliteWriter.WriteAsync() / SqliteDatabase"
Cohesion: 0.07
Nodes (23): SqliteDatabase, Path, Writer, SqliteDatabaseOptions, AppVersion, MigrationBackupDirectory, Pooling, TimeProvider (+15 more)

### Community 8 - "TaskHash.BuildCanonicalJson() / CanonicalJson.cs"
Cohesion: 0.11
Nodes (17): CanonicalArray, Count, this[], CanonicalBoolean, Value, CanonicalJson, CanonicalNode, CanonicalNull (+9 more)

### Community 9 - "CreateTaskRequest / TaskKind"
Cohesion: 0.06
Nodes (28): TaskKind, Analysis, CodeChange, TestRun, WorkspaceMode, ReadOnly, ReadWriteWorktree, OutboxSql (+20 more)

### Community 10 - "NodeHostOptions / TelegramApprovalOptions"
Cohesion: 0.05
Nodes (37): TelegramApprovalOptions, AllowedChatIds, AllowedUserIds, BotToken, NotifyInterval, PollTimeoutSeconds, NodeHostOptions, Accepts (+29 more)

### Community 11 - "OpenCodeTaskRunner.RunAsync() / OpenCodeExecutor"
Cohesion: 0.11
Nodes (19): ExecutorLimits, ExecutorSession, ExecutorSnapshot, ExecutorStartRequest, IExecutor, ToolEffect, Allow, Ask (+11 more)

### Community 12 - "system_text / Odw.Core.Primitives"
Cohesion: 0.11
Nodes (5): Odw.Node.Diagnostics, Odw.Node.Peers, Odw.Core.Primitives, TestModels, FreeOrFallback

### Community 13 - "JsonObject / OriginTools.ProposeAsync()"
Cohesion: 0.15
Nodes (8): IPeerDirectory, ITaskQuery, McpTool, McpToolResult, JsonTools, OriginTools, PeerTools, TaskTools

### Community 14 - "AgentRuntime / AgentRuntimeOptions"
Cohesion: 0.09
Nodes (13): RunAsCredentials, AgentCredentials, AgentCredentialsStore, AgentRuntime, Path, AgentRuntimeOptions, AgentAccount, ApplyAcls (+5 more)

### Community 15 - "DateTimeOffset / SqliteConnection"
Cohesion: 0.16
Nodes (8): AuditLog, ChainHash, ChainVerification, Valid, PersistenceJson, Options, RecoveryResult, TaskEventLog

### Community 16 - "OpenCodeClient.SendAsync() / OpenCodeDtos.cs"
Cohesion: 0.10
Nodes (15): OpenCodeApiException, Body, Method, Path, StatusCode, OpenCodePermissionEffect, Allow, Ask (+7 more)

### Community 17 - "Ulid / Ulid.New()"
Cohesion: 0.10
Nodes (6): Ulid, IsEmpty, MonotonicState, Timestamp, UlidTests, FixedTimeProvider

### Community 18 - "Fact / TaskValidator.Validate()"
Cohesion: 0.18
Nodes (4): TaskHashTests, TaskValidatorTests, TestTasks, TaskContractReaderTests

### Community 19 - "TaskRole / TaskCoordinator.TransitionAsync()"
Cohesion: 0.13
Nodes (11): TaskRole, Origin, Target, TaskStateChange, ApprovalCoordinator, TaskEventRecord, TaskSnapshot, TaskCoordinator (+3 more)

### Community 20 - "NodeConfig / NodeConfig.cs"
Cohesion: 0.07
Nodes (33): McpConfig, Bind, Port, TokenRef, NodeConfig, Accepts, Approvals, Capabilities (+25 more)

### Community 21 - "CancellationToken / WorkspaceManager.PrepareAsync()"
Cohesion: 0.15
Nodes (8): WorkspaceManager, IWorkspacePreparer, WorkspaceException, WorkspaceLease, Branch, Keep, Path, ReadOnly

### Community 22 - "TaskRequest / WorkspaceSpec"
Cohesion: 0.07
Nodes (32): ApprovalEvidence, ApprovalId, ApprovedAt, ApprovedBy, Nonce, Signature, TaskHash, ExecutorSpec (+24 more)

### Community 23 - "Odw.Node.Persistence / Odw.Node.Tests"
Cohesion: 0.12
Nodes (7): Odw.Node.Tests, Odw.Node.Persistence.Peers, Odw.Node.Persistence.Outbox, Odw.Node.Persistence, ApprovalStatus, TestModels, FreeOrFallback

### Community 24 - "SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()"
Cohesion: 0.12
Nodes (10): TaskOutcome, Cancelled, Failed, ReviewRequired, Succeeded, TaskEventView, TaskResultView, SqliteTaskQuery (+2 more)

### Community 25 - "TaskStore / TestDatabase.Create()"
Cohesion: 0.24
Nodes (5): StartupRecovery, TaskStore, RecoveryTests, TaskStoreTests, TestRows

### Community 26 - "NodeHost.cs / system_text_json_nodes"
Cohesion: 0.12
Nodes (6): Odw.Node.PeerApi, Odw.Mcp.Tests, Odw.Mcp.Abstractions, Odw.Mcp, Odw.Mcp.Tools, Odw.Node.Persistence.Queries

### Community 27 - "TaskState / TaskStateExtensions"
Cohesion: 0.10
Nodes (21): TaskStateExtensions, TaskState, Accepted, Approved, AwaitingLocalApproval, Cancelled, Cancelling, Completed (+13 more)

### Community 28 - "ApprovalService / ApprovalService.DecideAsync()"
Cohesion: 0.17
Nodes (5): ApprovalDecisionResult, ApprovalRecord, ApprovalService, ApprovalSigner, ApprovalServiceTests

### Community 29 - "OpenCodeServer / OpenCodeServerOptions"
Cohesion: 0.10
Nodes (15): OpenCodeServer, BaseUrl, Connection, Info, ProcessId, OpenCodeServerOptions, AttachProcess, Environment (+7 more)

### Community 31 - "NodeRuntime / PeerEndpoints.CreateAsync()"
Cohesion: 0.15
Nodes (13): NodeRuntime, Coordinator, Database, Directory, Options, Outbox, Peers, Policy (+5 more)

### Community 32 - "JobObject / JobLimits"
Cohesion: 0.11
Nodes (10): JobLimits, ActiveProcessLimit, CpuTime, JobMemoryBytes, ProcessMemoryBytes, JobObject, IoCounters, JobObjectBasicLimitInformation (+2 more)

### Community 33 - "OutboxStore / NewPendingAction"
Cohesion: 0.18
Nodes (11): NewPendingAction, OutboxActions, OutboxStatus, PendingAction, OutboxFailOutcome, DeadLettered, NotFound, RetryScheduled (+3 more)

### Community 34 - "DatabaseMigrator.Migrate() / MigrationTests.Failed_migration"
Cohesion: 0.13
Nodes (8): DatabaseMigrator, Migration, MigrationOutcome, SchemaVersion, MigrationTests, SqliteWriterTests, TestTime, Now

### Community 35 - "IReadOnlyList / OpenCodeTaskRunner"
Cohesion: 0.15
Nodes (5): OpenCodeJson, OpenCodeMessage, Text, OpenCodePermissionRequest, OpenCodeTaskRunner

### Community 36 - "PolicyEngine.FromJson() / PolicyEngine.Evaluate()"
Cohesion: 0.27
Nodes (4): PolicyRequest, Actions, PolicyEngineTests, PolicyFixtures

### Community 37 - "TaskTrigger / TaskTrigger.cs"
Cohesion: 0.07
Nodes (26): TaskTrigger, ApprovalDenied, ApprovalExpired, ApprovalGranted, CancelTimeout, LeaseExpired, OperatorDiscard, OperatorInspect (+18 more)

### Community 38 - "List / TaskValidator"
Cohesion: 0.19
Nodes (3): ContractFormat, TaskValidator, ValidationIssue

### Community 39 - "McpServer / McpServer.HandleAsync()"
Cohesion: 0.16
Nodes (6): JsonRpc, ErrorCodes, McpEndpoint, McpServer, Tools, OriginToolTests

### Community 40 - "TaskResult / TaskResult.cs"
Cohesion: 0.09
Nodes (21): ArtifactRef, TaskError, TaskResult, Artifacts, Branch, ChangedPaths, Commit, DiffSha256 (+13 more)

### Community 41 - "OriginProposalService / OriginProposalService.ProposeAsync()"
Cohesion: 0.16
Nodes (10): ITaskProposalService, ProposalInput, ProposalOutcome, IsError, ApprovalOptions, Ttl, OriginProposalService, FakeProposals (+2 more)

### Community 42 - "SandboxConfig / SandboxGuard.Validate()"
Cohesion: 0.12
Nodes (21): SandboxConfig, AgentAccount, AgentConfigPath, AgentSecretPath, ApplyAcls, NetworkAllowedHosts, NetworkIsolation, NetworkProxyPort (+13 more)

### Community 43 - "WorkspaceOptions / WorkspaceRequest"
Cohesion: 0.21
Nodes (11): WorkspaceOptions, AgentAccount, ApplyAcls, GitExecutable, GitTimeout, KeepWorkspaces, Repos, ServiceAccount (+3 more)

### Community 44 - "PolicyWhen / PolicyModels.cs"
Cohesion: 0.09
Nodes (20): LimitsWhen, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network, PolicyWhen, AnyAction, CapabilitiesAll (+12 more)

### Community 45 - "xunit / Odw.Sandbox.Windows"
Cohesion: 0.19
Nodes (4): Odw.Sandbox.Workspaces, Odw.Sandbox.Tests, Odw.Sandbox.Windows, Odw.Sandbox.Network

### Community 46 - "TransitionContext / TaskStateMachine.TryTransition()"
Cohesion: 0.13
Nodes (11): TaskStateMachine, Rule, TransitionContext, ApprovalExpired, ApprovalMatches, Default, LeaseExpired, ResourcesAvailable (+3 more)

### Community 47 - "OpenCodeTaskRunnerOptions / ToolPermissionHandler"
Cohesion: 0.09
Nodes (21): ToolPermissionHandler, OpenCodeTaskRunnerOptions, AgentAccount, AgentConfigTemplate, AgentRuntimeRoot, Executable, ExecutionTimeout, ModelProxyUrl (+13 more)

### Community 48 - "ModelProxyOptions / ModelProxyTests"
Cohesion: 0.18
Nodes (11): ModelProxyOptions, AddressFilter, AllowedHosts, AllowedPorts, AuthToken, ConnectTimeout, Log, MaxRequestBytes (+3 more)

### Community 49 - "McpServerTests / McpServerTests.CreateServer()"
Cohesion: 0.24
Nodes (6): McpServerTests, FakePeers, Peers, FakeTasks, Results, Statuses

### Community 50 - "WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests"
Cohesion: 0.18
Nodes (4): WorkspaceGuard, ByHandleFileInformation, WorkspaceGuardException, WorkspaceGuardTests

### Community 51 - "Lineage / LineageGuard.Evaluate()"
Cohesion: 0.18
Nodes (11): LineageCheck, DepthExceeded, Invalid, Loop, Ok, SelfDelegation, LineageGuard, Lineage (+3 more)

### Community 52 - "ModelProxy / ModelProxy.HandleAsync()"
Cohesion: 0.19
Nodes (4): ModelProxy, Port, Token, Url

### Community 53 - "NodeConfigLoader.Load() / NodeConfigurationException"
Cohesion: 0.33
Nodes (3): NodeConfigLoader, TelegramSettings, NodeConfigurationException

### Community 54 - "NodeDoctor / DoctorCheck"
Cohesion: 0.20
Nodes (3): BindPolicy, DoctorCheck, NodeDoctor

### Community 55 - "NodeHost / NodeHost.BuildMcp()"
Cohesion: 0.14
Nodes (12): NodeHost, ApprovalCoordinator, Approvals, Coordinator, McpBaseAddress, Options, PeerBaseAddress, Peers (+4 more)

### Community 56 - "OpenCodeClient / IOpenCodeClient"
Cohesion: 0.18
Nodes (6): IOpenCodeClient, BaseAddress, OpenCodeClient, BaseAddress, OpenCodeConnection, OpenCodeServerInfo

### Community 57 - "TaskLimits / OpenCodeTaskRunnerGuardTests.CreateAsync()"
Cohesion: 0.22
Nodes (9): TaskLimits, MaxCostUsd, MaxMemoryGb, MaxMinutes, MaxProcesses, Network, ExecutionRequest, ContractJson (+1 more)

### Community 58 - "Odw.Core.Contracts / NodeBootstrap.cs"
Cohesion: 0.17
Nodes (5): Odw.Core.Contracts, Odw.Executors.OpenCode, Odw.Policy.Tests, Odw.Policy, Odw.Executors.OpenCode.Tests

### Community 59 - "ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()"
Cohesion: 0.22
Nodes (5): ApprovalPipeOptions, Name, SecurityDescriptor, ApprovalPipeServer, PipeName

### Community 60 - "TimeSpan / ExecutorOutcomeKind"
Cohesion: 0.15
Nodes (8): ApprovalPipeClient, ExecutorOutcome, ExecutorOutcomeKind, Cancelled, Failed, Interrupted, ReviewRequired, Succeeded

### Community 61 - "WorkspaceChanges.CaptureAsync() / WorkspaceChanges"
Cohesion: 0.26
Nodes (3): WorkspaceChanges, WorkspaceChangeSet, WorkspaceChangesTests

### Community 62 - "Odw.Node.Hosting / CliApplication.cs"
Cohesion: 0.20
Nodes (3): Odw.Cli, Odw.Node.Hosting, Odw.Node.Configuration

### Community 63 - "McpHttpFixture.InitializeAsync() / TaskStatusView"
Cohesion: 0.15
Nodes (6): TaskStatusView, McpOptions, MaxBodyBytes, Path, Token, McpTestData

### Community 64 - "WhenDto / LimitsDto"
Cohesion: 0.13
Nodes (13): WhenDto, AnyAction, CapabilitiesAll, CapabilitiesNone, Direction, Kind, Limits, Mode (+5 more)

### Community 65 - "TaskAggregate / TaskAggregate.Create()"
Cohesion: 0.15
Nodes (8): TaskAggregate, Changes, CreatedAt, Role, State, StateSeq, TaskHash, TaskId

### Community 66 - "TransitionTaskRequest / CancelRequested"
Cohesion: 0.13
Nodes (15): TransitionTaskRequest, CancelRequested, EventPayloadJson, EventType, ExecutorRef, ExpectedSeq, ExpectedState, FinishedAt (+7 more)

### Community 67 - "TaskRequest contract / Approval binding to task_id, task_has"
Cohesion: 0.19
Nodes (11): Approval Gateway (out-of-band), Policy Engine (AUTO / ASK / DENY), Identifier formats (node_id, ULID task_id), task_hash canonical projection (RFC 8785 JCS), TaskRequest contract, ApprovalService, pipe server and signer, odw_* MCP tool catalogue, Policy Engine semantics (first match, fail-closed) (+3 more)

### Community 68 - "NodeHost.StartAsync() / TelegramApprovalService"
Cohesion: 0.23
Nodes (3): TelegramApprovalService, PeerApiClientFactory, PeerStore

### Community 69 - "IAsyncDisposable / TestDatabase"
Cohesion: 0.15
Nodes (8): Fixture, Folder, Host, TestDatabase, Database, Folder, Path, Time

### Community 71 - "Enums.cs / NetworkAccess"
Cohesion: 0.14
Nodes (11): NetworkAccess, Allow, None, TestStatus, Failed, Passed, Skipped, WorkspaceStrategy (+3 more)

### Community 72 - "Threat model T1-T10 / Dedicated identities odw-svc and odw-a"
Cohesion: 0.27
Nodes (11): Sandbox module, Isolated workspace per task, H3 — Isolation and policies (done), Dedicated identities odw-svc and odw-agent, Job Object resource limits, Network isolation: WFP by SID plus CONNECT proxy, Reparse point and hardlink guard, Threat model T1-T10 (+3 more)

### Community 73 - "OriginPoller / OriginPoller.RefreshOnceAsync()"
Cohesion: 0.29
Nodes (3): OriginPoller, OriginPollerOptions, PollInterval

### Community 75 - "ApprovalsConfig / TelegramConfig"
Cohesion: 0.15
Nodes (13): ApprovalsConfig, Enabled, HmacKeyRef, PipeName, SecurityDescriptor, Telegram, TtlMinutes, TelegramConfig (+5 more)

### Community 76 - "Peer protocol v1 / ODW — OpenCode Distributed Worker Network"
Cohesion: 0.17
Nodes (9): Two interfaces: local MCP facade and peer API, Local MCP facade, ODW — OpenCode Distributed Worker Network, Peer API (node to node), OpenCode SSE: data-only lines, no Last-Event-ID, NodeHost with two listeners, RFC 7807 error catalogue, Peer protocol v1 (+1 more)

### Community 77 - "Origin state machine / SQLite schema (tasks, peers, events, "
Cohesion: 0.20
Nodes (10): Origin state machine, Target state machine, TaskState enum (single enum, per-role subsets), OriginProposalService and OriginPoller, Embedded versioned migrations, Outbox (pending_actions) with backoff and dead-letter, SQLite schema (tasks, peers, events, approvals), SqliteWriter single-writer actor (+2 more)

### Community 78 - "Func / PolicyDocumentLoader.Load()"
Cohesion: 0.26
Nodes (3): PolicyDocument, PolicyValidationException, Issues

### Community 81 - "PolicyEngine / PolicyEngine.Matches()"
Cohesion: 0.24
Nodes (3): IPolicyEngine, PolicyEngine, Document

### Community 82 - "IOpenCodeClient.ReplyPermissionAsync() / OpenCodePermissionR"
Cohesion: 0.21
Nodes (7): OpenCodePermissionReply, Always, Once, Reject, ToolPermissionDecision, AllowOnce, Reject

### Community 84 - "DoctorOptions / DatabasePath"
Cohesion: 0.17
Nodes (12): DoctorOptions, DatabasePath, ExpectedSpecSha256, McpBind, McpPort, McpToken, OpenCodeExecutable, PeerBind (+4 more)

### Community 85 - "Milestone plan D0 through H6 / Lineage and anti-cycle guard"
Cohesion: 0.22
Nodes (11): Planner (delegation scoring), Lineage and anti-cycle guard, Task divisibility via declared split_units, Deterministic planner scoring formula, Leases and heartbeats, D0 — Detailed design (done), H0 — OpenCode verification (partial, H0-6 open), H4 — Full bidirectionality (+3 more)

### Community 87 - "ExecutorEventKind / Completed"
Cohesion: 0.18
Nodes (11): ExecutorEventKind, Completed, Failed, Heartbeat, Interrupted, PermissionAsked, Started, TextDelta (+3 more)

### Community 88 - "OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod"
Cohesion: 0.27
Nodes (8): OpenAPI spec served at GET /openapi.json, pinned by SHA-256, Port 0 plus /api/info pid and urls discovery, Prompt endpoint POST /api/session/{id}/prompt, OpenCode v2.3.2 V2 surface under /api (143 routes), odw doctor diagnostics, OpenCodeClient typed V2 HTTP client, OpenCodeServer launcher (loopback, port 0), odw CLI (serve, doctor, approvals, service)

### Community 89 - "PeerRegistry / PeerIdentity"
Cohesion: 0.31
Nodes (4): PeerIdentity, PeerRegistry, Entry, KnownNodes

### Community 90 - "InvalidStateTransitionException / TaskAggregate.Apply()"
Cohesion: 0.24
Nodes (6): InvalidStateTransitionException, Failure, From, Role, TaskId, Trigger

### Community 91 - "PeerApiConfig / PeerTlsConfig"
Cohesion: 0.20
Nodes (10): PeerApiConfig, Address, AllowLoopback, Bind, Port, Tls, PeerTlsConfig, CertificatePath (+2 more)

### Community 93 - "SandboxInspector.Inspect() / SandboxReport"
Cohesion: 0.27
Nodes (5): SandboxInspector, SandboxReport, FullyEnforced, NetworkEnforced, SandboxInspectorTests

### Community 95 - "ODW Node service (one per PC) / Task Manager (state machine,"
Cohesion: 0.22
Nodes (8): Audit Log (append-only), Identity and Trust module, ODW Node service (one per PC), Resource Monitor (CPU, RAM, GPU, tools), Task Manager (state machine, leases, recovery), Odw.slnx solution and project layout, IExecutor executor port, Hash-chained append-only audit log

### Community 96 - "NodeHostedService / ServeHost.RunAsync()"
Cohesion: 0.28
Nodes (3): NodeHostedService, ServeHost, ServeOptions

### Community 97 - "IApprovalChannel.cs / ApprovalDecisionKind"
Cohesion: 0.28
Nodes (6): ApprovalDecisionKind, Approved, Denied, Expired, ApprovalOutcome, IApprovalChannel

### Community 99 - "NetworkIsolationWatchdog / NetworkIsolationWatchdogTests.Sto"
Cohesion: 0.33
Nodes (3): NetworkIsolationWatchdog, Lost, NetworkIsolationWatchdogTests

### Community 101 - "h0-probe.ps1 / Assert-True()"
Cohesion: 0.28
Nodes (3): Assert-True(), Write-Fail(), Write-Pass()

### Community 103 - "ValidationResult / ValidationResult.cs"
Cohesion: 0.25
Nodes (6): ValidationCodes, ValidationResult, Codes, Issues, IsValid, Valid

### Community 104 - "ConfiguredPeer / PeerFile"
Cohesion: 0.25
Nodes (8): ConfiguredPeer, Address, Capabilities, NodeId, TokenRef, Trust, PeerFile, Peers

### Community 106 - "Secret references and DPAPI storage / Stateless Streamable H"
Cohesion: 0.33
Nodes (6): MCP server config under mcp.servers with oauth false, node.json configuration and secret references, Stateless Streamable HTTP MCP transport, policy.json document and condition set, Peer auth: Bearer token plus X-Odw-Node, Secret references and DPAPI storage

### Community 107 - "ExecutionScheduler / H1 — Minimal node and basic delegation"
Cohesion: 0.38
Nodes (6): ExecutionScheduler, H1 — Minimal node and basic delegation, Architecture invariant tests, Testing strategy and golden rules, TwoNodeHarness end-to-end tests, E2E loopback harness (tools/e2e-loopback.ps1)

### Community 108 - "ApprovalDecisionStatus / AlreadyDecided"
Cohesion: 0.29
Nodes (7): ApprovalDecisionStatus, AlreadyDecided, Approved, Denied, Expired, HashMismatch, NotFound

### Community 109 - "Goal / Accepted"
Cohesion: 0.29
Nodes (7): Goal, Accepted, Cancelled, Done, Failed, Rejected, Running

### Community 110 - "TaskModels.cs / TransitionResult"
Cohesion: 0.33
Nodes (5): TransitionResult, TransitionStatus, Applied, Conflict, NotFound

### Community 111 - "RuleDto / WhenDto"
Cohesion: 0.29
Nodes (6): RuleDto, Effect, Id, Priority, Reason, When

### Community 113 - "permission.asked event blocks the prompt indefinitely / Fina"
Cohesion: 0.40
Nodes (4): TaskResult contract, permission.asked event blocks the prompt indefinitely, Final verification and evidence-based outcome, Permission broker (once or reject, never always)

### Community 114 - "LimitsDto / RangeDto"
Cohesion: 0.33
Nodes (5): LimitsDto, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network

### Community 115 - "DocumentDto / RuleDto"
Cohesion: 0.33
Nodes (5): DocumentDto, Default, Rules, Schema, Version

### Community 116 - "PolicyEffect / PolicyRule"
Cohesion: 0.33
Nodes (5): PolicyEffect, Ask, Auto, Deny, PolicyRule

### Community 117 - "origin-node.ps1 / Get-OriginProcesses()"
Cohesion: 0.47
Nodes (4): Get-OriginProcesses(), Stop-Origin(), Test-OriginHealth(), Wait-Health()

### Community 118 - "policy.example.json / default"
Cohesion: 0.40
Nodes (4): default, rules, $schema, version

### Community 120 - "ApprovalRequest / ApprovalScope"
Cohesion: 0.40
Nodes (4): ApprovalRequest, ApprovalScope, Task, ToolUse

### Community 121 - "DoctorReport / DoctorReport.DoctorReport()"
Cohesion: 0.40
Nodes (4): DoctorReport, Failures, Ok, Warnings

### Community 122 - "TimeWindowDto / Days"
Cohesion: 0.40
Nodes (5): TimeWindowDto, Days, From, TimeZone, To

### Community 125 - "PolicyDecision / IsAllowed"
Cohesion: 0.50
Nodes (3): PolicyDecision, IsAllowed, RequiresHuman

### Community 126 - "DoctorStatus / Fail"
Cohesion: 0.50
Nodes (4): DoctorStatus, Fail, Pass, Warn

### Community 128 - "RangeDto / Gte"
Cohesion: 0.67
Nodes (3): RangeDto, Gte, Lte

## Knowledge Gaps
- **630 isolated node(s):** `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.NET.Sdk`, `Task` (+625 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 808 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **20 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Odw.Core.Model` connect `Odw.Core.Model / Odw.Node.Persistence.Tasks` to `IApprovalChannel.cs / ApprovalDecisionKind`, `OutboxStore / NewPendingAction`, `NodeHost.cs / system_text_json_nodes`, `TaskTrigger / TaskTrigger.cs`, `Enums.cs / NetworkAccess`, `TaskResult / TaskResult.cs`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `OpenCodeTaskRunner.RunAsync() / OpenCodeExecutor`, `system_text / Odw.Core.Primitives`, `TaskModels.cs / TransitionResult`, `Odw.Node.Persistence / Odw.Node.Tests`, `TaskRequest / WorkspaceSpec`, `ApprovalPipeServer.cs / Odw.Node.Approvals`, `Odw.Core.Contracts / NodeBootstrap.cs`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **What connects `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices` to the rest of the system?**
  _630 weakly-connected nodes found - possible missing edges._
- **Should `Task / ExecutionScheduler` be split into smaller, more focused modules?**
  _Cohesion score 0.06428571428571428 - nodes in this community are weakly interconnected._
- **Why does `PolicyDocumentLoader` connect `PolicyDocumentLoader / PolicyDocumentLoader.MapWhen()` to `RangeDto / Gte`, `WhenDto / LimitsDto`, `List / TaskValidator`, `JsonSerializerOptions / StringOrArrayConverter`, `system_text / Odw.Core.Primitives`, `Func / PolicyDocumentLoader.Load()`, `RuleDto / WhenDto`, `LimitsDto / RangeDto`, `DocumentDto / RuleDto`, `TimeWindowDto / Days`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Should `OpenCodeClientTests.Create() / PeerApiClient` be split into smaller, more focused modules?**
  _Cohesion score 0.05711849957374254 - nodes in this community are weakly interconnected._
- **Why does `NodeHostOptions` connect `NodeHostOptions / TelegramApprovalOptions` to `Task / ExecutionScheduler`, `NodeBootstrap.StartAsync() / NodeSettings`, `IReadOnlyList / OpenCodeTaskRunner`, `NodeHost.StartAsync() / TelegramApprovalService`, `Odw.Core.Model / Odw.Node.Persistence.Tasks`, `Theory / InlineData`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `OriginPoller / OriginPoller.RefreshOnceAsync()`, `PolicyEngine / PolicyEngine.Matches()`, `TaskRole / TaskCoordinator.TransitionAsync()`, `ModelProxy / ModelProxy.HandleAsync()`, `NodeConfigLoader.Load() / NodeConfigurationException`, `CancellationToken / WorkspaceManager.PrepareAsync()`, `NodeHost / NodeHost.BuildMcp()`, `ApprovalService / ApprovalService.DecideAsync()`, `NodeRuntime / PeerEndpoints.CreateAsync()`?**
  _High betweenness centrality (0.033) - this node is a cross-community bridge._
- **Should `Odw.slnx / Odw.Node.csproj` be split into smaller, more focused modules?**
  _Cohesion score 0.055218855218855216 - nodes in this community are weakly interconnected._