# Graph Report - ODW — OpenCode Distributed Worker Network  (2026-11-25)

## Corpus Check
- 197 files · ~160,287 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 4 file(s) not represented in the graph (top: (none) 2, .props 2)

## Summary
- 2545 nodes · 7488 edges · 127 communities (107 shown, 20 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 986 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- OpenCodeClientTests.Create() / PeerApiClient
- Ulid / NodeRuntime
- DoctorOptions / NodeDoctor.RunAsync()
- WorkspaceManager.PrepareAsync() / WorkspaceManager
- Odw.slnx / Odw.Node.csproj
- IntPtr / WfpNetworkPolicy
- CancellationToken / OpenCodeClient
- NodeHostOptions / PeerRegistration
- ModelProxy / ModelProxyOptions
- TaskHash.BuildCanonicalJson() / CanonicalJson.cs
- DateTimeOffset / SqliteConnection
- Odw.Node.Tests / Odw.Node.Persistence.Tasks
- JsonElement / TaskContractReader
- Fact / TaskValidator.Validate()
- TaskRole / TaskCoordinator.TransitionAsync()
- NodeConfig / NodeConfig.cs
- Odw.Core.Model / Odw.Node.Persistence
- IReadOnlyList / PeerStatusSnapshot
- Task / FakeOpenCodeClient
- JsonObject / TaskTools.ResultAsync()
- TaskRequest / WorkspaceSpec
- OpenCodeTaskRunner.RunAsync() / SandboxAgentTests.OpenCode_r
- NodeHost.cs / system_text_json_nodes
- OpenCodeServer / OpenCodeServerOptions
- CliApplication / CliApplication.DoctorAsync()
- system_text / system_net
- JobObject / JobLimits
- TaskStore / TestDatabase.Create()
- OriginProposalService / OriginProposalService.ProposeAsync()
- xunit / Odw.Sandbox.Windows
- TaskState / TaskStateExtensions
- TransitionContext / TaskStateMachine.TryTransition()
- TaskTrigger / TaskTrigger.cs
- SqliteDatabase / SqliteWriter
- TaskResult / TaskResult.cs
- SandboxConfig / SandboxGuard.Validate()
- SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()
- CreateTaskRequest / WorkspaceMode
- SqliteWriter.WriteAsync() / OutboxStore
- PolicyEngine.FromJson() / PolicyEngine.Evaluate()
- Func / PolicyDocumentLoader
- TestRows.NewId() / FakeWorkspacePreparer
- TimeProvider / DatabaseMigrator.Migrate()
- NodeHost.StartAsync() / ApprovalPipeServer
- OpenCodeTaskRunnerOptions / ToolPermissionHandler
- McpServerTests / McpServerTests.CreateServer()
- Fixture.StartAsync() / NodeHostTests
- McpServer / McpServer.HandleAsync()
- PolicyEngine / PolicyRequest
- PolicyWhen / PolicyEffect
- ApprovalService / ApprovalService.CreateAsync()
- WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests
- OpenCodeExecutor / OpenCodeExecutor.StreamAsync()
- NodeHost / NodeHost.BuildPeer()
- Lineage / LineageGuard.Evaluate()
- system_text_json / ApprovalPipeServer.cs
- OriginPoller / OriginPoller.RefreshOnceAsync()
- TimeSpan / NetworkIsolationWatchdog
- Odw.Core.Primitives / Odw.Core.Hashing
- NodeConfigLoader.Load() / NodeConfigurationException
- OpenCodeDtos.cs / OpenCodeSession
- SqliteDatabaseOptions / FacadeFixture
- TransitionTaskRequest / TestRows.Transition()
- TaskKind / TaskStore.Map()
- Enums.cs / NetworkAccess
- ExecutionScheduler / ExecutionScheduler.ExecuteOneAsync()
- WhenDto / LimitsDto
- OriginEndToEndTests / OriginEndToEndTests.StartAsync()
- TaskRequest contract / Approval binding to task_id, task_has
- McpHttpTests / McpHttpTests.Authorized()
- NodeBootstrap.StartAsync() / OpenCodeTaskRunner
- McpHttpFixture.InitializeAsync() / TaskResultView
- List / TaskValidator
- IApprovalChannel.cs / ApprovalRequest
- Threat model T1-T10 / Dedicated identities odw-svc and odw-a
- Odw.Core.Contracts / PolicyModels.cs
- TelegramApprovalService / TelegramClient
- TaskLimits / ExecutionRequest
- SandboxAgentTests.OpenCode_starts_as_the_agent() / AgentCred
- ApprovalsConfig / TelegramConfig
- Peer protocol v1 / ODW — OpenCode Distributed Worker Network
- Origin state machine / SQLite schema (tasks, peers, events, 
- ContractFormat / Regex
- IAsyncDisposable / Fixture
- NodeHost.BuildMcp() / McpEndpoint.HandleAsync()
- TaskAggregate / TaskAggregate.Create()
- AgentRuntime / AgentRuntime.Create()
- Milestone plan D0 through H6 / Lineage and anti-cycle guard
- AgentRuntimeOptions / AgentRuntimeTests.Creates_and_removes_
- ServeHost.cs / NodeHostedService
- ExecutorEventKind / Completed
- ExecutionResult / ExecutorOutcomeKind
- ApprovalService.DecideAsync() / ApprovalCoordinator.ApproveA
- OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod
- PeerRegistry / PeerIdentity
- InvalidStateTransitionException / TaskAggregate.Apply()
- PeerApiConfig / PeerTlsConfig
- SandboxInspector.Inspect() / SandboxReport
- NodeConfigTests / NodeConfigTests.Bootstrap_starts_a_host_fr
- ODW Node service (one per PC) / Task Manager (state machine,
- IEnumerable / ApprovalPipeServer.ListJson()
- h0-probe.ps1 / Assert-True()
- JsonSerializerOptions / StringOrArrayConverter
- ValidationResult / ValidationResult.cs
- ConfiguredPeer / PeerFile
- LimitsWhen / RangeWhen
- Secret references and DPAPI storage / Stateless Streamable H
- ExecutionScheduler / H1 — Minimal node and basic delegation
- MigrationTests.ScalarLong() / SqliteWriterTests.Cancelled_wr
- ApprovalDecisionStatus / AlreadyDecided
- RuleDto / WhenDto
- permission.asked event blocks the prompt indefinitely / Fina
- LimitsDto / RangeDto
- DocumentDto / RuleDto
- TelegramApprovalOptions / AllowedChatIds
- TransitionResult / TransitionStatus
- policy.example.json / default
- TimeWindowDto / Days
- OutboxFailOutcome / DeadLettered
- RangeDto / Gte
- TestModels.cs / TestModels
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

## Communities (127 total, 20 thin omitted)

### Community 0 - "OpenCodeClientTests.Create() / PeerApiClient"
Cohesion: 0.05
Nodes (23): OpenCodeApiException, Body, Method, Path, StatusCode, OpenCodeProtocolException, OriginException, PeerApiClient (+15 more)

### Community 1 - "Ulid / NodeRuntime"
Cohesion: 0.06
Nodes (19): Ulid, IsEmpty, MonotonicState, Timestamp, NodeRuntime, Coordinator, Database, Directory (+11 more)

### Community 2 - "DoctorOptions / NodeDoctor.RunAsync()"
Cohesion: 0.06
Nodes (25): BindPolicy, DoctorCheck, DoctorOptions, DatabasePath, ExpectedSpecSha256, McpBind, McpPort, McpToken (+17 more)

### Community 3 - "WorkspaceManager.PrepareAsync() / WorkspaceManager"
Cohesion: 0.09
Nodes (21): WorkspaceChanges, WorkspaceChangeSet, WorkspaceManager, WorkspaceOptions, AgentAccount, ApplyAcls, GitExecutable, GitTimeout (+13 more)

### Community 4 - "Odw.slnx / Odw.Node.csproj"
Cohesion: 0.06
Nodes (38): Microsoft.Data.Sqlite, Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.WindowsServices, System.Security.Cryptography.ProtectedData, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+30 more)

### Community 5 - "IntPtr / WfpNetworkPolicy"
Cohesion: 0.12
Nodes (11): WfpNetworkPolicy, DefaultStatePath, FwpAction0, FwpByteBlob, FwpConditionValue0, FwpmDisplayData0, FwpmFilter0, FwpmFilterCondition0 (+3 more)

### Community 6 - "CancellationToken / OpenCodeClient"
Cohesion: 0.09
Nodes (10): ExecutorSnapshot, IOpenCodeClient, BaseAddress, OpenCodeClient, BaseAddress, OpenCodePermissionReply, Always, Once (+2 more)

### Community 7 - "NodeHostOptions / PeerRegistration"
Cohesion: 0.06
Nodes (35): NodeHostOptions, Accepts, AllowLoopbackPeerBind, ApprovalOptions, ApprovalPipeName, ApprovalPipeSecurityDescriptor, ApprovalSigner, Capabilities (+27 more)

### Community 8 - "ModelProxy / ModelProxyOptions"
Cohesion: 0.09
Nodes (15): ModelProxy, Port, Token, Url, ModelProxyOptions, AddressFilter, AllowedHosts, AllowedPorts (+7 more)

### Community 9 - "TaskHash.BuildCanonicalJson() / CanonicalJson.cs"
Cohesion: 0.11
Nodes (17): CanonicalArray, Count, this[], CanonicalBoolean, Value, CanonicalJson, CanonicalNode, CanonicalNull (+9 more)

### Community 10 - "DateTimeOffset / SqliteConnection"
Cohesion: 0.15
Nodes (7): AuditLog, ChainHash, ChainVerification, Valid, PendingAction, RecoveryResult, TaskEventLog

### Community 11 - "Odw.Node.Tests / Odw.Node.Persistence.Tasks"
Cohesion: 0.13
Nodes (11): Odw.Node.Scheduling, Odw.PeerApi, Odw.Node.Origins, Odw.Node.Tests, Odw.Node.Hosting, Odw.Node.Tasks, Odw.Node.Persistence.Tasks, Odw.Policy (+3 more)

### Community 12 - "JsonElement / TaskContractReader"
Cohesion: 0.18
Nodes (5): TaskContractException, Field, TaskContractReader, SqlitePeerDirectory, Details

### Community 13 - "Fact / TaskValidator.Validate()"
Cohesion: 0.17
Nodes (4): TaskHashTests, TaskValidatorTests, TestTasks, TaskContractReaderTests

### Community 14 - "TaskRole / TaskCoordinator.TransitionAsync()"
Cohesion: 0.13
Nodes (12): TaskRole, Origin, Target, TaskStateChange, TaskStatusView, TaskEventRecord, TaskSnapshot, TaskCoordinator (+4 more)

### Community 15 - "NodeConfig / NodeConfig.cs"
Cohesion: 0.06
Nodes (33): McpConfig, Bind, Port, TokenRef, NodeConfig, Accepts, Approvals, Capabilities (+25 more)

### Community 16 - "Odw.Core.Model / Odw.Node.Persistence"
Cohesion: 0.12
Nodes (7): Odw.Node.Persistence.Peers, Odw.Node.Persistence.Outbox, Odw.Node.Persistence, Odw.Core.Model, ApprovalStatus, OutboxActions, OutboxStatus

### Community 17 - "IReadOnlyList / PeerStatusSnapshot"
Cohesion: 0.11
Nodes (6): OpenCodeJson, OpenCodeMessage, Text, OpenCodePermissionRequest, PeerStatusSnapshot, IsOnline

### Community 18 - "Task / FakeOpenCodeClient"
Cohesion: 0.14
Nodes (13): OpenCodeExecutorTests, EventFactory, FakeOpenCodeClient, BaseAddress, BlockAfterEvents, Events, Info, Interrupts (+5 more)

### Community 19 - "JsonObject / TaskTools.ResultAsync()"
Cohesion: 0.17
Nodes (7): IPeerDirectory, ITaskQuery, McpTool, McpToolResult, JsonTools, PeerTools, TaskTools

### Community 20 - "TaskRequest / WorkspaceSpec"
Cohesion: 0.07
Nodes (32): ApprovalEvidence, ApprovalId, ApprovedAt, ApprovedBy, Nonce, Signature, TaskHash, ExecutorSpec (+24 more)

### Community 21 - "OpenCodeTaskRunner.RunAsync() / SandboxAgentTests.OpenCode_r"
Cohesion: 0.14
Nodes (13): ExecutorLimits, ExecutorSession, ExecutorStartRequest, IExecutor, ToolEffect, Allow, Ask, Deny (+5 more)

### Community 22 - "NodeHost.cs / system_text_json_nodes"
Cohesion: 0.12
Nodes (6): Odw.Node.PeerApi, Odw.Mcp.Tests, Odw.Mcp.Abstractions, Odw.Mcp, Odw.Mcp.Tools, Odw.Node.Persistence.Queries

### Community 23 - "OpenCodeServer / OpenCodeServerOptions"
Cohesion: 0.10
Nodes (15): OpenCodeServer, BaseUrl, Connection, Info, ProcessId, OpenCodeServerOptions, AttachProcess, Environment (+7 more)

### Community 25 - "system_text / system_net"
Cohesion: 0.11
Nodes (6): Odw.Sandbox.Network, Odw.Node.Diagnostics, Odw.Executors.OpenCode.Tests, TestModels, FreeOrFallback, TestRequests

### Community 26 - "JobObject / JobLimits"
Cohesion: 0.11
Nodes (10): JobLimits, ActiveProcessLimit, CpuTime, JobMemoryBytes, ProcessMemoryBytes, JobObject, IoCounters, JobObjectBasicLimitInformation (+2 more)

### Community 27 - "TaskStore / TestDatabase.Create()"
Cohesion: 0.28
Nodes (4): StartupRecovery, TaskStore, RecoveryTests, TaskStoreTests

### Community 28 - "OriginProposalService / OriginProposalService.ProposeAsync()"
Cohesion: 0.15
Nodes (8): ITaskProposalService, ProposalInput, ProposalOutcome, IsError, OriginTools, ApprovalOptions, Ttl, OriginProposalService

### Community 29 - "xunit / Odw.Sandbox.Windows"
Cohesion: 0.15
Nodes (4): Odw.Sandbox.Workspaces, Odw.Sandbox.Tests, Odw.Sandbox.Windows, Odw.Executors.OpenCode

### Community 30 - "TaskState / TaskStateExtensions"
Cohesion: 0.10
Nodes (21): TaskStateExtensions, TaskState, Accepted, Approved, AwaitingLocalApproval, Cancelled, Cancelling, Completed (+13 more)

### Community 31 - "TransitionContext / TaskStateMachine.TryTransition()"
Cohesion: 0.10
Nodes (11): TaskStateMachine, Rule, TransitionContext, ApprovalExpired, ApprovalMatches, Default, LeaseExpired, ResourcesAvailable (+3 more)

### Community 32 - "TaskTrigger / TaskTrigger.cs"
Cohesion: 0.07
Nodes (26): TaskTrigger, ApprovalDenied, ApprovalExpired, ApprovalGranted, CancelTimeout, LeaseExpired, OperatorDiscard, OperatorInspect (+18 more)

### Community 33 - "SqliteDatabase / SqliteWriter"
Cohesion: 0.09
Nodes (16): SqliteDatabase, Path, Writer, SqliteWriter, CommittedCount, FailedCount, QueueDepth, WriteCommand (+8 more)

### Community 34 - "TaskResult / TaskResult.cs"
Cohesion: 0.09
Nodes (21): ArtifactRef, TaskError, TaskResult, Artifacts, Branch, ChangedPaths, Commit, DiffSha256 (+13 more)

### Community 35 - "SandboxConfig / SandboxGuard.Validate()"
Cohesion: 0.12
Nodes (21): SandboxConfig, AgentAccount, AgentConfigPath, AgentSecretPath, ApplyAcls, NetworkAllowedHosts, NetworkIsolation, NetworkProxyPort (+13 more)

### Community 36 - "SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()"
Cohesion: 0.15
Nodes (8): TaskOutcome, Cancelled, Failed, ReviewRequired, Succeeded, SqliteTaskQuery, ResultDetails, SqliteFacadeTests

### Community 37 - "CreateTaskRequest / WorkspaceMode"
Cohesion: 0.08
Nodes (23): WorkspaceMode, ReadOnly, ReadWriteWorktree, CreateTaskRequest, CounterpartNode, CreatedAt, ExecutorRef, ExpiresAt (+15 more)

### Community 38 - "SqliteWriter.WriteAsync() / OutboxStore"
Cohesion: 0.24
Nodes (4): NewPendingAction, OutboxStore, MaxAttempts, OutboxTests

### Community 40 - "Func / PolicyDocumentLoader"
Cohesion: 0.26
Nodes (4): PolicyDocumentLoader, PolicyIssue, PolicyValidationException, Issues

### Community 41 - "TestRows.NewId() / FakeWorkspacePreparer"
Cohesion: 0.33
Nodes (8): IWorkspacePreparer, ExecutionSchedulerTests, FakeExecutionRunner, Requests, FakeWorkspacePreparer, FailWith, Requests, Root

### Community 42 - "TimeProvider / DatabaseMigrator.Migrate()"
Cohesion: 0.16
Nodes (7): DatabaseMigrator, Migration, MigrationOutcome, SchemaVersion, MigrationTests, TestTime, Now

### Community 43 - "NodeHost.StartAsync() / ApprovalPipeServer"
Cohesion: 0.14
Nodes (8): ApprovalCoordinator, ApprovalPipeOptions, Name, SecurityDescriptor, ApprovalPipeServer, PipeName, PeerApiClientFactory, PeerStore

### Community 44 - "OpenCodeTaskRunnerOptions / ToolPermissionHandler"
Cohesion: 0.09
Nodes (21): ToolPermissionHandler, OpenCodeTaskRunnerOptions, AgentAccount, AgentConfigTemplate, AgentRuntimeRoot, Executable, ExecutionTimeout, ModelProxyUrl (+13 more)

### Community 45 - "McpServerTests / McpServerTests.CreateServer()"
Cohesion: 0.24
Nodes (6): McpServerTests, FakePeers, Peers, FakeTasks, Results, Statuses

### Community 47 - "McpServer / McpServer.HandleAsync()"
Cohesion: 0.20
Nodes (8): JsonRpc, ErrorCodes, McpServer, Tools, OriginToolTests, FakeProposals, Last, Outcome

### Community 48 - "PolicyEngine / PolicyRequest"
Cohesion: 0.13
Nodes (8): IPolicyEngine, PolicyDecision, IsAllowed, RequiresHuman, PolicyRequest, Actions, PolicyEngine, Document

### Community 49 - "PolicyWhen / PolicyEffect"
Cohesion: 0.10
Nodes (19): PolicyEffect, Ask, Auto, Deny, PolicyRule, PolicyWhen, AnyAction, CapabilitiesAll (+11 more)

### Community 50 - "ApprovalService / ApprovalService.CreateAsync()"
Cohesion: 0.24
Nodes (4): ApprovalRecord, ApprovalService, ApprovalSigner, ApprovalServiceTests

### Community 51 - "WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests"
Cohesion: 0.18
Nodes (4): WorkspaceGuard, ByHandleFileInformation, WorkspaceGuardException, WorkspaceGuardTests

### Community 52 - "OpenCodeExecutor / OpenCodeExecutor.StreamAsync()"
Cohesion: 0.21
Nodes (3): ExecutorEvent, OpenCodeEvent, OpenCodeExecutor

### Community 53 - "NodeHost / NodeHost.BuildPeer()"
Cohesion: 0.13
Nodes (12): NodeHost, ApprovalCoordinator, Approvals, Coordinator, McpBaseAddress, Options, PeerBaseAddress, Peers (+4 more)

### Community 54 - "Lineage / LineageGuard.Evaluate()"
Cohesion: 0.18
Nodes (11): LineageCheck, DepthExceeded, Invalid, Loop, Ok, SelfDelegation, LineageGuard, Lineage (+3 more)

### Community 55 - "system_text_json / ApprovalPipeServer.cs"
Cohesion: 0.14
Nodes (4): Odw.Node.Contracts, Odw.Approvals, Odw.Node.Approvals, ApprovalPipeClient

### Community 56 - "OriginPoller / OriginPoller.RefreshOnceAsync()"
Cohesion: 0.16
Nodes (10): OriginPoller, Goal, Accepted, Cancelled, Done, Failed, Rejected, Running (+2 more)

### Community 57 - "TimeSpan / NetworkIsolationWatchdog"
Cohesion: 0.16
Nodes (4): ExecutorOutcome, NetworkIsolationWatchdog, Lost, NetworkIsolationWatchdogTests

### Community 58 - "Odw.Core.Primitives / Odw.Core.Hashing"
Cohesion: 0.16
Nodes (5): Odw.Core.Validation, Odw.Core.Hashing, Odw.Core.Tests, Odw.Core.Delegation, Odw.Core.Primitives

### Community 59 - "NodeConfigLoader.Load() / NodeConfigurationException"
Cohesion: 0.33
Nodes (3): NodeConfigLoader, TelegramSettings, NodeConfigurationException

### Community 60 - "OpenCodeDtos.cs / OpenCodeSession"
Cohesion: 0.19
Nodes (9): OpenCodePermissionEffect, Allow, Ask, Deny, OpenCodePermissionRule, EffectValue, OpenCodeSession, IsBusy (+1 more)

### Community 61 - "SqliteDatabaseOptions / FacadeFixture"
Cohesion: 0.12
Nodes (12): SqliteDatabaseOptions, AppVersion, MigrationBackupDirectory, Pooling, TimeProvider, WriteQueueCapacity, Clock, Now (+4 more)

### Community 62 - "TransitionTaskRequest / TestRows.Transition()"
Cohesion: 0.11
Nodes (16): TransitionTaskRequest, CancelRequested, EventPayloadJson, EventType, ExecutorRef, ExpectedSeq, ExpectedState, FinishedAt (+8 more)

### Community 63 - "TaskKind / TaskStore.Map()"
Cohesion: 0.17
Nodes (7): TaskKind, Analysis, CodeChange, TestRun, OutboxSql, PersistenceJson, Options

### Community 64 - "Enums.cs / NetworkAccess"
Cohesion: 0.12
Nodes (14): MessageDelivery, Queue, Steer, NetworkAccess, Allow, None, TestStatus, Failed (+6 more)

### Community 65 - "ExecutionScheduler / ExecutionScheduler.ExecuteOneAsync()"
Cohesion: 0.21
Nodes (8): ExecutionScheduler, TransitionOptions, CancelRequested, ExecutorRef, FinishedAt, LeaseExpiresAt, NextAction, ResultJson

### Community 66 - "WhenDto / LimitsDto"
Cohesion: 0.13
Nodes (13): WhenDto, AnyAction, CapabilitiesAll, CapabilitiesNone, Direction, Kind, Limits, Mode (+5 more)

### Community 68 - "TaskRequest contract / Approval binding to task_id, task_has"
Cohesion: 0.19
Nodes (11): Approval Gateway (out-of-band), Policy Engine (AUTO / ASK / DENY), Identifier formats (node_id, ULID task_id), task_hash canonical projection (RFC 8785 JCS), TaskRequest contract, ApprovalService, pipe server and signer, odw_* MCP tool catalogue, Policy Engine semantics (first match, fail-closed) (+3 more)

### Community 70 - "NodeBootstrap.StartAsync() / OpenCodeTaskRunner"
Cohesion: 0.23
Nodes (4): NodeSettings, NodeBootstrap, OpenCodeTaskRunner, PolicyDocument

### Community 71 - "McpHttpFixture.InitializeAsync() / TaskResultView"
Cohesion: 0.20
Nodes (3): TaskEventView, TaskResultView, McpTestData

### Community 73 - "IApprovalChannel.cs / ApprovalRequest"
Cohesion: 0.19
Nodes (10): ApprovalDecisionKind, Approved, Denied, Expired, ApprovalOutcome, ApprovalRequest, ApprovalScope, Task (+2 more)

### Community 74 - "Threat model T1-T10 / Dedicated identities odw-svc and odw-a"
Cohesion: 0.27
Nodes (11): Sandbox module, Isolated workspace per task, H3 — Isolation and policies (done), Dedicated identities odw-svc and odw-agent, Job Object resource limits, Network isolation: WFP by SID plus CONNECT proxy, Reparse point and hardlink guard, Threat model T1-T10 (+3 more)

### Community 75 - "Odw.Core.Contracts / PolicyModels.cs"
Cohesion: 0.17
Nodes (6): Odw.Core.Contracts, Odw.Policy.Tests, ToolPermissionDecision, AllowOnce, Reject, OpenCodeIntegrationTests

### Community 77 - "TelegramApprovalService / TelegramClient"
Cohesion: 0.27
Nodes (3): TelegramCallback, TelegramClient, TelegramApprovalService

### Community 78 - "TaskLimits / ExecutionRequest"
Cohesion: 0.28
Nodes (8): TaskLimits, MaxCostUsd, MaxMemoryGb, MaxMinutes, MaxProcesses, Network, ExecutionRequest, OpenCodeTaskRunnerGuardTests

### Community 79 - "SandboxAgentTests.OpenCode_starts_as_the_agent() / AgentCred"
Cohesion: 0.27
Nodes (4): RunAsCredentials, AgentCredentials, AgentCredentialsStore, SandboxAgentTests

### Community 80 - "ApprovalsConfig / TelegramConfig"
Cohesion: 0.15
Nodes (13): ApprovalsConfig, Enabled, HmacKeyRef, PipeName, SecurityDescriptor, Telegram, TtlMinutes, TelegramConfig (+5 more)

### Community 81 - "Peer protocol v1 / ODW — OpenCode Distributed Worker Network"
Cohesion: 0.17
Nodes (9): Two interfaces: local MCP facade and peer API, Local MCP facade, ODW — OpenCode Distributed Worker Network, Peer API (node to node), OpenCode SSE: data-only lines, no Last-Event-ID, NodeHost with two listeners, RFC 7807 error catalogue, Peer protocol v1 (+1 more)

### Community 82 - "Origin state machine / SQLite schema (tasks, peers, events, "
Cohesion: 0.20
Nodes (10): Origin state machine, Target state machine, TaskState enum (single enum, per-role subsets), OriginProposalService and OriginPoller, Embedded versioned migrations, Outbox (pending_actions) with backoff and dead-letter, SQLite schema (tasks, peers, events, approvals), SqliteWriter single-writer actor (+2 more)

### Community 84 - "IAsyncDisposable / Fixture"
Cohesion: 0.18
Nodes (5): McpHttpFixture, Client, Fixture, Folder, Host

### Community 85 - "NodeHost.BuildMcp() / McpEndpoint.HandleAsync()"
Cohesion: 0.24
Nodes (5): McpEndpoint, McpOptions, MaxBodyBytes, Path, Token

### Community 87 - "TaskAggregate / TaskAggregate.Create()"
Cohesion: 0.20
Nodes (8): TaskAggregate, Changes, CreatedAt, Role, State, StateSeq, TaskHash, TaskId

### Community 89 - "Milestone plan D0 through H6 / Lineage and anti-cycle guard"
Cohesion: 0.22
Nodes (11): Planner (delegation scoring), Lineage and anti-cycle guard, Task divisibility via declared split_units, Deterministic planner scoring formula, Leases and heartbeats, D0 — Detailed design (done), H0 — OpenCode verification (partial, H0-6 open), H4 — Full bidirectionality (+3 more)

### Community 90 - "AgentRuntimeOptions / AgentRuntimeTests.Creates_and_removes_"
Cohesion: 0.20
Nodes (7): AgentRuntimeOptions, AgentAccount, ApplyAcls, ConfigTemplate, Root, ServiceAccount, AgentRuntimeTests

### Community 91 - "ServeHost.cs / NodeHostedService"
Cohesion: 0.25
Nodes (4): Odw.Cli, NodeHostedService, ServeHost, ServeOptions

### Community 92 - "ExecutorEventKind / Completed"
Cohesion: 0.18
Nodes (11): ExecutorEventKind, Completed, Failed, Heartbeat, Interrupted, PermissionAsked, Started, TextDelta (+3 more)

### Community 93 - "ExecutionResult / ExecutorOutcomeKind"
Cohesion: 0.20
Nodes (9): ExecutorOutcomeKind, Cancelled, Failed, Interrupted, ReviewRequired, Succeeded, ExecutionResult, ITaskExecutionRunner (+1 more)

### Community 95 - "OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod"
Cohesion: 0.27
Nodes (8): OpenAPI spec served at GET /openapi.json, pinned by SHA-256, Port 0 plus /api/info pid and urls discovery, Prompt endpoint POST /api/session/{id}/prompt, OpenCode v2.3.2 V2 surface under /api (143 routes), odw doctor diagnostics, OpenCodeClient typed V2 HTTP client, OpenCodeServer launcher (loopback, port 0), odw CLI (serve, doctor, approvals, service)

### Community 96 - "PeerRegistry / PeerIdentity"
Cohesion: 0.31
Nodes (4): PeerIdentity, PeerRegistry, Entry, KnownNodes

### Community 97 - "InvalidStateTransitionException / TaskAggregate.Apply()"
Cohesion: 0.24
Nodes (6): InvalidStateTransitionException, Failure, From, Role, TaskId, Trigger

### Community 98 - "PeerApiConfig / PeerTlsConfig"
Cohesion: 0.20
Nodes (10): PeerApiConfig, Address, AllowLoopback, Bind, Port, Tls, PeerTlsConfig, CertificatePath (+2 more)

### Community 99 - "SandboxInspector.Inspect() / SandboxReport"
Cohesion: 0.27
Nodes (5): SandboxInspector, SandboxReport, FullyEnforced, NetworkEnforced, SandboxInspectorTests

### Community 101 - "ODW Node service (one per PC) / Task Manager (state machine,"
Cohesion: 0.22
Nodes (8): Audit Log (append-only), Identity and Trust module, ODW Node service (one per PC), Resource Monitor (CPU, RAM, GPU, tools), Task Manager (state machine, leases, recovery), Odw.slnx solution and project layout, IExecutor executor port, Hash-chained append-only audit log

### Community 104 - "h0-probe.ps1 / Assert-True()"
Cohesion: 0.28
Nodes (3): Assert-True(), Write-Fail(), Write-Pass()

### Community 106 - "ValidationResult / ValidationResult.cs"
Cohesion: 0.25
Nodes (6): ValidationCodes, ValidationResult, Codes, Issues, IsValid, Valid

### Community 107 - "ConfiguredPeer / PeerFile"
Cohesion: 0.25
Nodes (8): ConfiguredPeer, Address, Capabilities, NodeId, TokenRef, Trust, PeerFile, Peers

### Community 108 - "LimitsWhen / RangeWhen"
Cohesion: 0.25
Nodes (6): LimitsWhen, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network, RangeWhen

### Community 110 - "Secret references and DPAPI storage / Stateless Streamable H"
Cohesion: 0.33
Nodes (6): MCP server config under mcp.servers with oauth false, node.json configuration and secret references, Stateless Streamable HTTP MCP transport, policy.json document and condition set, Peer auth: Bearer token plus X-Odw-Node, Secret references and DPAPI storage

### Community 111 - "ExecutionScheduler / H1 — Minimal node and basic delegation"
Cohesion: 0.38
Nodes (6): ExecutionScheduler, H1 — Minimal node and basic delegation, Architecture invariant tests, Testing strategy and golden rules, TwoNodeHarness end-to-end tests, E2E loopback harness (tools/e2e-loopback.ps1)

### Community 113 - "ApprovalDecisionStatus / AlreadyDecided"
Cohesion: 0.29
Nodes (7): ApprovalDecisionStatus, AlreadyDecided, Approved, Denied, Expired, HashMismatch, NotFound

### Community 114 - "RuleDto / WhenDto"
Cohesion: 0.29
Nodes (6): RuleDto, Effect, Id, Priority, Reason, When

### Community 115 - "permission.asked event blocks the prompt indefinitely / Fina"
Cohesion: 0.40
Nodes (4): TaskResult contract, permission.asked event blocks the prompt indefinitely, Final verification and evidence-based outcome, Permission broker (once or reject, never always)

### Community 116 - "LimitsDto / RangeDto"
Cohesion: 0.33
Nodes (5): LimitsDto, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network

### Community 117 - "DocumentDto / RuleDto"
Cohesion: 0.33
Nodes (5): DocumentDto, Default, Rules, Schema, Version

### Community 118 - "TelegramApprovalOptions / AllowedChatIds"
Cohesion: 0.33
Nodes (6): TelegramApprovalOptions, AllowedChatIds, AllowedUserIds, BotToken, NotifyInterval, PollTimeoutSeconds

### Community 119 - "TransitionResult / TransitionStatus"
Cohesion: 0.33
Nodes (5): TransitionResult, TransitionStatus, Applied, Conflict, NotFound

### Community 120 - "policy.example.json / default"
Cohesion: 0.40
Nodes (4): default, rules, $schema, version

### Community 121 - "TimeWindowDto / Days"
Cohesion: 0.40
Nodes (5): TimeWindowDto, Days, From, TimeZone, To

### Community 123 - "OutboxFailOutcome / DeadLettered"
Cohesion: 0.50
Nodes (4): OutboxFailOutcome, DeadLettered, NotFound, RetryScheduled

### Community 124 - "RangeDto / Gte"
Cohesion: 0.67
Nodes (3): RangeDto, Gte, Lte

## Knowledge Gaps
- **630 isolated node(s):** `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.NET.Sdk`, `Task` (+625 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 792 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **20 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Odw.Core.Model` connect `Odw.Core.Model / Odw.Node.Persistence` to `Enums.cs / NetworkAccess`, `TaskTrigger / TaskTrigger.cs`, `TaskResult / TaskResult.cs`, `IApprovalChannel.cs / ApprovalRequest`, `Odw.Core.Contracts / PolicyModels.cs`, `Odw.Node.Tests / Odw.Node.Persistence.Tasks`, `TaskRequest / WorkspaceSpec`, `OpenCodeTaskRunner.RunAsync() / SandboxAgentTests.OpenCode_r`, `NodeHost.cs / system_text_json_nodes`, `system_text_json / ApprovalPipeServer.cs`, `system_text / system_net`, `Odw.Core.Primitives / Odw.Core.Hashing`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `xunit / Odw.Sandbox.Windows`?**
  _High betweenness centrality (0.043) - this node is a cross-community bridge._
- **What connects `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices` to the rest of the system?**
  _630 weakly-connected nodes found - possible missing edges._
- **Should `OpenCodeClientTests.Create() / PeerApiClient` be split into smaller, more focused modules?**
  _Cohesion score 0.05370843989769821 - nodes in this community are weakly interconnected._
- **Why does `PolicyDocumentLoader` connect `Func / PolicyDocumentLoader` to `WhenDto / LimitsDto`, `JsonSerializerOptions / StringOrArrayConverter`, `RuleDto / WhenDto`, `ContractFormat / Regex`, `LimitsDto / RangeDto`, `DocumentDto / RuleDto`, `TimeWindowDto / Days`, `Odw.Core.Primitives / Odw.Core.Hashing`, `RangeDto / Gte`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Should `Ulid / NodeRuntime` be split into smaller, more focused modules?**
  _Cohesion score 0.059027777777777776 - nodes in this community are weakly interconnected._
- **Why does `NodeHostOptions` connect `NodeHostOptions / PeerRegistration` to `Ulid / NodeRuntime`, `DoctorOptions / NodeDoctor.RunAsync()`, `NodeBootstrap.StartAsync() / OpenCodeTaskRunner`, `ModelProxy / ModelProxyOptions`, `TestRows.NewId() / FakeWorkspacePreparer`, `TimeProvider / DatabaseMigrator.Migrate()`, `Odw.Node.Tests / Odw.Node.Persistence.Tasks`, `NodeHost.StartAsync() / ApprovalPipeServer`, `Fixture.StartAsync() / NodeHostTests`, `PolicyEngine / PolicyRequest`, `IReadOnlyList / PeerStatusSnapshot`, `ApprovalService / ApprovalService.CreateAsync()`, `NodeHost / NodeHost.BuildPeer()`, `TelegramApprovalOptions / AllowedChatIds`, `OriginPoller / OriginPoller.RefreshOnceAsync()`, `NodeConfigLoader.Load() / NodeConfigurationException`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `ExecutionResult / ExecutorOutcomeKind`?**
  _High betweenness centrality (0.032) - this node is a cross-community bridge._
- **Should `DoctorOptions / NodeDoctor.RunAsync()` be split into smaller, more focused modules?**
  _Cohesion score 0.05853174603174603 - nodes in this community are weakly interconnected._