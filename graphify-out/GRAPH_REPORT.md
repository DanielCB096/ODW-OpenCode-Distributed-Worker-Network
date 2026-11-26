# Graph Report - ODW — OpenCode Distributed Worker Network  (2026-11-26)

## Corpus Check
- 215 files · ~175,612 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 5 file(s) not represented in the graph (top: (none) 2, .props 2, .runsettings 1)

## Summary
- 2712 nodes · 8020 edges · 130 communities (108 shown, 22 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 1055 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- PeerApiClient / OpenCodeClientTests.Create()
- NodeRuntime / Ulid
- JsonElement / TaskContractReader
- ExecutionScheduler / TaskSnapshot
- Odw.Core.Model / xunit
- Odw.slnx / Odw.Node.csproj
- DoctorOptions / NodeDoctor.RunAsync()
- system_text / Odw.Node.Tests
- IntPtr / WfpNetworkPolicy
- WorkspaceManager.PrepareAsync() / WorkspaceManager
- Fact / TestTasks.Valid()
- TaskHash.BuildCanonicalJson() / CanonicalJson.Serialize()
- ContractRecordTests.Executor_and_approval_records_construct(
- SqliteDatabase / TimeProvider
- OpenCodeTaskRunner.RunAsync() / OpenCodeClient
- ModelProxy / ModelProxyOptions
- NodeHostOptions / PeerRegistration
- CancellationToken / TimeSpan
- DateTimeOffset / SqliteConnection
- JsonObject / OriginTools.ProposeAsync()
- Theory / InlineData
- McpServer / McpServer.HandleAsync()
- JobObject / JobLimits
- TaskStore / TestDatabase.Create()
- TaskState / TaskStateExtensions
- NodeConfig / NodeConfig.cs
- Odw.Node.Persistence / system_globalization
- Func / PolicyDocumentLoader
- CliApplication / CliApplication.DoctorAsync()
- NodeHost.cs / system_text_json_nodes
- Fixture.StartAsync() / NodeHostTests
- Task / PeerStatusSnapshot
- IReadOnlyList / OpenCodeTaskRunner
- TaskRequest / List
- FakeOpenCodeClient / OpenCodeExecutorTests.Create()
- TaskRole / TransitionContext
- Odw.Sandbox.Windows / Odw.Sandbox.Workspaces
- TaskTrigger / TaskTrigger.cs
- ApprovalService / ApprovalService.DecideAsync()
- FakeTasks / FakePeers
- TaskResult / ContractRecordTests.Task_result_records_constru
- NodeBootstrap.StartAsync() / NodeConfigLoader.Load()
- PolicyEngine.FromJson() / PolicyEngine.Evaluate()
- TaskLimits / WorkspaceSpec
- SandboxConfig / SandboxGuard.Validate()
- SqliteWriter.WriteAsync() / OutboxStore
- OriginProposalService / OriginProposalService.ProposeAsync()
- ValueTask / IAsyncDisposable
- NodeHost / NodeHost.BuildPeer()
- OpenCodeTaskRunnerOptions / AgentAccount
- OriginPoller / OriginPoller.RefreshOnceAsync()
- WorkspaceMode / Enums.cs
- AgentRuntime / AgentRuntimeOptions
- WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests
- ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()
- CreateTaskRequest / CounterpartNode
- OpenCodeServer / Action
- IReadOnlyDictionary / ModelProfile
- TaskCoordinator.TransitionAsync() / OpenCodeTaskRunnerIntegr
- WorkspaceChanges.CaptureAsync() / WorkspaceChanges
- ArtifactToolTests.CreateServer() / FakeArtifacts
- IReadOnlyCollection / OpenCodeEvent
- SqliteWriter / WriteCommand
- WhenDto / LimitsDto
- NodeHost.StartAsync() / TelegramApprovalService
- TransitionTaskRequest / CancelRequested
- PolicyWhen / TimeWindowWhen
- OriginEndToEndTests / OriginEndToEndTests.StartAsync()
- TaskRequest contract / Approval binding to task_id, task_has
- Lineage / LineageGuard.Evaluate()
- OpenCodeDtos.cs / OpenCodeSessionRequest
- NodeConfigTests / NodeConfigTests.Folder()
- Threat model T1-T10 / Dedicated identities odw-svc and odw-a
- ServeHost.cs / NodeHostedService
- TaskStore.Map() / PersistenceJson
- TaskAggregate / TaskStateChange
- SandboxAgentTests.OpenCode_starts_as_the_agent() / AgentCred
- ApprovalsConfig / TelegramConfig
- Peer protocol v1 / ODW — OpenCode Distributed Worker Network
- TaskAggregate.Apply() / TaskAggregate.CreateOrigin()
- Origin state machine / SQLite schema (tasks, peers, events, 
- PolicyRequest / PolicyDecision
- McpTestData.Request() / McpServerTests
- Milestone plan D0 through H6 / Lineage and anti-cycle guard
- PolicyEngine / PolicyEngine.Matches()
- NetworkIsolationWatchdog / CancellationTokenSource
- ApprovalPipeServer.cs / Odw.Node.Approvals
- OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod
- PeerRegistry / PeerIdentity
- OpenCodeServerOptions / AttachProcess
- PeerApiConfig / PeerTlsConfig
- SandboxInspector.Inspect() / SandboxReport
- ODW Node service (one per PC) / Task Manager (state machine,
- ArtifactStoreTests / ArtifactStoreTests.Oversized_artifact_i
- h0-probe.ps1 / Assert-True()
- InvalidStateTransitionException / InvalidOperationException
- JsonSerializerOptions / StringOrArrayConverter
- ValidationResult / ValidationResult.cs
- ConfiguredPeer / PeerFile
- ModelsConfig / ModelProfileConfig
- LimitsWhen / RangeWhen
- Secret references and DPAPI storage / Stateless Streamable H
- ExecutionScheduler / H1 — Minimal node and basic delegation
- MigrationTests.ScalarLong() / SqliteWriterTests.Cancelled_wr
- ApprovalDecisionStatus / AlreadyDecided
- RuleDto / WhenDto
- permission.asked event blocks the prompt indefinitely / Fina
- LimitsDto / RangeDto
- DocumentDto / RuleDto
- ExecutorOutcomeKind / Cancelled
- LineageCheck / DepthExceeded
- TelegramApprovalOptions / AllowedChatIds
- TransitionResult / TransitionStatus
- origin-node.ps1 / Get-OriginProcesses()
- policy.example.json / default
- TaskKind / TaskStateExtensions.ToWireValue()
- TimeWindowDto / Days
- OpenCodeExecutor.cs / ToolPermissionDecision
- RangeDto / Gte
- TestModels.cs / TestModels
- WfpNetworkPolicyTests.Agent_is_blocked_from_internet_but_can

## God Nodes (most connected - your core abstractions)
1. `Odw.Core.Model` - 75 edges
2. `TaskState` - 59 edges
3. `NodeHostOptions` - 59 edges
4. `TaskRequest` - 50 edges
5. `TaskStore` - 50 edges
6. `TaskRole` - 44 edges
7. `NodeHost` - 43 edges
8. `FakeOpenCodeClient` - 41 edges
9. `TaskTrigger` - 39 edges
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

## Communities (130 total, 22 thin omitted)

### Community 0 - "PeerApiClient / OpenCodeClientTests.Create()"
Cohesion: 0.05
Nodes (20): TelegramCallback, TelegramClient, OriginException, PeerApiClient, BaseAddress, PeerArtifactView, PeerConnection, PeerPostResult (+12 more)

### Community 1 - "NodeRuntime / Ulid"
Cohesion: 0.06
Nodes (20): Ulid, IsEmpty, MonotonicState, Timestamp, NodeRuntime, Artifacts, Coordinator, Database (+12 more)

### Community 2 - "JsonElement / TaskContractReader"
Cohesion: 0.08
Nodes (14): TaskOutcome, Cancelled, Failed, ReviewRequired, Succeeded, TaskContractException, Field, TaskContractReader (+6 more)

### Community 3 - "ExecutionScheduler / TaskSnapshot"
Cohesion: 0.10
Nodes (25): TaskSnapshot, ExecutionRequest, ExecutionResult, ExecutionSchedulerOptions, MaxConcurrentTasks, PollInterval, ITaskExecutionRunner, ExecutionScheduler (+17 more)

### Community 4 - "Odw.Core.Model / xunit"
Cohesion: 0.09
Nodes (14): Odw.Core.Validation, Odw.Core.Contracts, Odw.PeerApi, Odw.Node.Contracts, Odw.Core.Hashing, Odw.Core.Tests, Odw.Node.Origins, Odw.Policy.Tests (+6 more)

### Community 5 - "Odw.slnx / Odw.Node.csproj"
Cohesion: 0.05
Nodes (39): coverlet.collector, Microsoft.Data.Sqlite, Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.WindowsServices, System.Security.Cryptography.ProtectedData, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+31 more)

### Community 6 - "DoctorOptions / NodeDoctor.RunAsync()"
Cohesion: 0.07
Nodes (25): BindPolicy, DoctorCheck, DoctorOptions, AllowLoopbackPeerBind, DatabasePath, ExpectedSpecSha256, McpBind, McpPort (+17 more)

### Community 7 - "system_text / Odw.Node.Tests"
Cohesion: 0.10
Nodes (11): Odw.Node.Scheduling, Odw.Executors.OpenCode, Odw.Node.Tests, Odw.Node.Hosting, Odw.Node.Diagnostics, Odw.Policy, Odw.Node.Peers, Odw.Node.Configuration (+3 more)

### Community 8 - "IntPtr / WfpNetworkPolicy"
Cohesion: 0.12
Nodes (11): WfpNetworkPolicy, DefaultStatePath, FwpAction0, FwpByteBlob, FwpConditionValue0, FwpmDisplayData0, FwpmFilter0, FwpmFilterCondition0 (+3 more)

### Community 9 - "WorkspaceManager.PrepareAsync() / WorkspaceManager"
Cohesion: 0.13
Nodes (19): WorkspaceManager, WorkspaceOptions, AgentAccount, ApplyAcls, GitExecutable, GitTimeout, KeepWorkspaces, Repos (+11 more)

### Community 10 - "Fact / TestTasks.Valid()"
Cohesion: 0.14
Nodes (4): TaskHashTests, TaskValidatorCoverageTests, TaskValidatorTests, TestTasks

### Community 11 - "TaskHash.BuildCanonicalJson() / CanonicalJson.Serialize()"
Cohesion: 0.11
Nodes (17): CanonicalArray, Count, this[], CanonicalBoolean, Value, CanonicalJson, CanonicalNode, CanonicalNull (+9 more)

### Community 12 - "ContractRecordTests.Executor_and_approval_records_construct("
Cohesion: 0.07
Nodes (31): ApprovalDecisionKind, Approved, Denied, Expired, ApprovalOutcome, ApprovalRequest, ApprovalScope, Task (+23 more)

### Community 13 - "SqliteDatabase / TimeProvider"
Cohesion: 0.08
Nodes (18): DatabaseMigrator, Migration, MigrationOutcome, SqliteDatabase, Path, SchemaVersion, Writer, SqliteDatabaseOptions (+10 more)

### Community 14 - "OpenCodeTaskRunner.RunAsync() / OpenCodeClient"
Cohesion: 0.12
Nodes (11): ExecutorLimits, ExecutorStartRequest, IOpenCodeClient, BaseAddress, OpenCodeClient, BaseAddress, OpenCodeConnection, OpenCodeServerInfo (+3 more)

### Community 15 - "ModelProxy / ModelProxyOptions"
Cohesion: 0.09
Nodes (15): ModelProxy, Port, Token, Url, ModelProxyOptions, AddressFilter, AllowedHosts, AllowedPorts (+7 more)

### Community 16 - "NodeHostOptions / PeerRegistration"
Cohesion: 0.06
Nodes (35): NodeHostOptions, Accepts, AllowLoopbackPeerBind, ApprovalOptions, ApprovalPipeName, ApprovalPipeSecurityDescriptor, ApprovalSigner, ArtifactsRoot (+27 more)

### Community 17 - "CancellationToken / TimeSpan"
Cohesion: 0.10
Nodes (9): ExecutorOutcome, ExecutorSnapshot, IExecutor, OpenCodePermissionReply, Always, Once, Reject, OpenCodeSession (+1 more)

### Community 18 - "DateTimeOffset / SqliteConnection"
Cohesion: 0.14
Nodes (8): AuditLog, ChainHash, ChainVerification, Valid, PendingAction, RecoveryResult, TaskEventLog, TaskEventRecord

### Community 19 - "JsonObject / OriginTools.ProposeAsync()"
Cohesion: 0.15
Nodes (8): IPeerDirectory, ITaskQuery, McpTool, McpToolResult, JsonTools, OriginTools, PeerTools, TaskTools

### Community 20 - "Theory / InlineData"
Cohesion: 0.10
Nodes (3): ContractFormat, OpenCodeVersion, ContractFormatTests

### Community 21 - "McpServer / McpServer.HandleAsync()"
Cohesion: 0.11
Nodes (14): ApprovalPipeClient, JsonRpc, ErrorCodes, McpEndpoint, McpOptions, MaxBodyBytes, Path, Token (+6 more)

### Community 22 - "JobObject / JobLimits"
Cohesion: 0.09
Nodes (10): JobLimits, ActiveProcessLimit, CpuTime, JobMemoryBytes, ProcessMemoryBytes, JobObject, IoCounters, JobObjectBasicLimitInformation (+2 more)

### Community 23 - "TaskStore / TestDatabase.Create()"
Cohesion: 0.22
Nodes (5): StartupRecovery, TaskStore, RecoveryTests, TaskStoreTests, TestRows

### Community 24 - "TaskState / TaskStateExtensions"
Cohesion: 0.09
Nodes (22): TaskStateExtensions, TaskState, Accepted, Approved, AwaitingLocalApproval, Cancelled, Cancelling, Completed (+14 more)

### Community 25 - "NodeConfig / NodeConfig.cs"
Cohesion: 0.06
Nodes (34): McpConfig, Bind, Port, TokenRef, NodeConfig, Accepts, Approvals, Capabilities (+26 more)

### Community 26 - "Odw.Node.Persistence / system_globalization"
Cohesion: 0.11
Nodes (6): Odw.Node.Persistence.Peers, Odw.Node.Persistence.Outbox, Odw.Node.Persistence, ApprovalStatus, OutboxActions, OutboxStatus

### Community 27 - "Func / PolicyDocumentLoader"
Cohesion: 0.17
Nodes (10): PolicyEffect, Ask, Auto, Deny, PolicyDocumentLoader, PolicyDocument, PolicyIssue, PolicyRule (+2 more)

### Community 29 - "NodeHost.cs / system_text_json_nodes"
Cohesion: 0.14
Nodes (6): Odw.Node.PeerApi, Odw.Mcp.Tests, Odw.Mcp.Abstractions, Odw.Mcp, Odw.Mcp.Tools, Odw.Node.Persistence.Queries

### Community 30 - "Fixture.StartAsync() / NodeHostTests"
Cohesion: 0.19
Nodes (4): NodeHostTests, Fixture, Folder, Host

### Community 31 - "Task / PeerStatusSnapshot"
Cohesion: 0.13
Nodes (5): PeerStatusSnapshot, IsOnline, McpHttpFixture, Client, McpHttpTests

### Community 32 - "IReadOnlyList / OpenCodeTaskRunner"
Cohesion: 0.14
Nodes (6): OpenCodeJson, OpenCodeMessage, Text, OpenCodePermissionRequest, OpenCodeProtocolException, OpenCodeTaskRunner

### Community 33 - "TaskRequest / List"
Cohesion: 0.15
Nodes (17): TaskRequest, Approval, CreatedAt, Executor, ExpiresAt, Instructions, Kind, Limits (+9 more)

### Community 34 - "FakeOpenCodeClient / OpenCodeExecutorTests.Create()"
Cohesion: 0.14
Nodes (14): ToolPermissionHandler, OpenCodeExecutorTests, EventFactory, FakeOpenCodeClient, BaseAddress, BlockAfterEvents, Events, Info (+6 more)

### Community 35 - "TaskRole / TransitionContext"
Cohesion: 0.11
Nodes (14): TaskRole, Origin, Target, TaskStateMachine, Rule, TransitionContext, ApprovalExpired, ApprovalMatches (+6 more)

### Community 36 - "Odw.Sandbox.Windows / Odw.Sandbox.Workspaces"
Cohesion: 0.13
Nodes (4): Odw.Sandbox.Workspaces, Odw.Sandbox.Tests, Odw.Sandbox.Windows, Odw.Sandbox.Network

### Community 37 - "TaskTrigger / TaskTrigger.cs"
Cohesion: 0.07
Nodes (27): TaskTrigger, ApprovalDenied, ApprovalExpired, ApprovalGranted, CancelTimeout, LeaseExpired, OperatorDiscard, OperatorInspect (+19 more)

### Community 38 - "ApprovalService / ApprovalService.DecideAsync()"
Cohesion: 0.20
Nodes (5): ApprovalDecisionResult, ApprovalRecord, ApprovalService, ApprovalSigner, ApprovalServiceTests

### Community 39 - "FakeTasks / FakePeers"
Cohesion: 0.14
Nodes (9): TaskEventView, TaskResultView, TaskStatusView, FakePeers, Peers, FakeTasks, Results, Statuses (+1 more)

### Community 40 - "TaskResult / ContractRecordTests.Task_result_records_constru"
Cohesion: 0.11
Nodes (21): ArtifactRef, TaskError, TaskResult, Artifacts, Branch, ChangedPaths, Commit, DiffSha256 (+13 more)

### Community 41 - "NodeBootstrap.StartAsync() / NodeConfigLoader.Load()"
Cohesion: 0.23
Nodes (5): NodeConfigLoader, NodeSettings, TelegramSettings, NodeBootstrap, NodeConfigurationException

### Community 43 - "TaskLimits / WorkspaceSpec"
Cohesion: 0.08
Nodes (24): ApprovalEvidence, ApprovalId, ApprovedAt, ApprovedBy, Nonce, Signature, TaskHash, ExecutorSpec (+16 more)

### Community 44 - "SandboxConfig / SandboxGuard.Validate()"
Cohesion: 0.12
Nodes (21): SandboxConfig, AgentAccount, AgentConfigPath, AgentSecretPath, ApplyAcls, NetworkAllowedHosts, NetworkIsolation, NetworkProxyPort (+13 more)

### Community 45 - "SqliteWriter.WriteAsync() / OutboxStore"
Cohesion: 0.23
Nodes (8): NewPendingAction, OutboxFailOutcome, DeadLettered, NotFound, RetryScheduled, OutboxStore, MaxAttempts, OutboxTests

### Community 46 - "OriginProposalService / OriginProposalService.ProposeAsync()"
Cohesion: 0.16
Nodes (8): ITaskProposalService, ProposalInput, ProposalOutcome, IsError, ApprovalOptions, Ttl, TaskContractWriter, OriginProposalService

### Community 47 - "ValueTask / IAsyncDisposable"
Cohesion: 0.12
Nodes (9): FacadeFixture, Clock, Database, Folder, TestDatabase, Database, Folder, Path (+1 more)

### Community 48 - "NodeHost / NodeHost.BuildPeer()"
Cohesion: 0.11
Nodes (12): NodeHost, ApprovalCoordinator, Approvals, Coordinator, McpBaseAddress, Options, PeerBaseAddress, Peers (+4 more)

### Community 49 - "OpenCodeTaskRunnerOptions / AgentAccount"
Cohesion: 0.09
Nodes (23): OpenCodeTaskRunnerOptions, AgentAccount, AgentConfigTemplate, AgentRuntimeRoot, ArtifactsRoot, DefaultModelProfile, Executable, ExecutionTimeout (+15 more)

### Community 50 - "OriginPoller / OriginPoller.RefreshOnceAsync()"
Cohesion: 0.15
Nodes (11): OriginPoller, Goal, Accepted, Cancelled, Done, Failed, Interrupted, Rejected (+3 more)

### Community 51 - "WorkspaceMode / Enums.cs"
Cohesion: 0.10
Nodes (17): MessageDelivery, Queue, Steer, NetworkAccess, Allow, None, TestStatus, Failed (+9 more)

### Community 52 - "AgentRuntime / AgentRuntimeOptions"
Cohesion: 0.16
Nodes (9): AgentRuntime, Path, AgentRuntimeOptions, AgentAccount, ApplyAcls, ConfigTemplate, Root, ServiceAccount (+1 more)

### Community 53 - "WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests"
Cohesion: 0.18
Nodes (4): WorkspaceGuard, ByHandleFileInformation, WorkspaceGuardException, WorkspaceGuardTests

### Community 54 - "ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()"
Cohesion: 0.17
Nodes (5): ApprovalPipeOptions, Name, SecurityDescriptor, ApprovalPipeServer, PipeName

### Community 55 - "CreateTaskRequest / CounterpartNode"
Cohesion: 0.10
Nodes (20): CreateTaskRequest, CounterpartNode, CreatedAt, ExecutorRef, ExpiresAt, InitialEventPayloadJson, InitialEventType, Kind (+12 more)

### Community 56 - "OpenCodeServer / Action"
Cohesion: 0.16
Nodes (5): OpenCodeServer, BaseUrl, Connection, Info, ProcessId

### Community 57 - "IReadOnlyDictionary / ModelProfile"
Cohesion: 0.18
Nodes (7): ModelCatalog, DefaultProfiles, Free, ModelProfile, QualifiedName, ModelProfileResolver, ModelProfileResolverTests

### Community 58 - "TaskCoordinator.TransitionAsync() / OpenCodeTaskRunnerIntegr"
Cohesion: 0.30
Nodes (4): TaskCoordinator, ContractModels, OpenCodeTaskRunnerIntegrationTests, TaskCoordinatorTests

### Community 59 - "WorkspaceChanges.CaptureAsync() / WorkspaceChanges"
Cohesion: 0.22
Nodes (4): WorkspaceChanges, WorkspaceChangeSet, FullDiff, WorkspaceChangesTests

### Community 60 - "ArtifactToolTests.CreateServer() / FakeArtifacts"
Cohesion: 0.28
Nodes (5): ArtifactContent, IArtifactFetcher, ArtifactToolTests, FakeArtifacts, Content

### Community 61 - "IReadOnlyCollection / OpenCodeEvent"
Cohesion: 0.20
Nodes (6): OpenCodeEvent, OpenCodeApiException, Body, Method, Path, StatusCode

### Community 62 - "SqliteWriter / WriteCommand"
Cohesion: 0.17
Nodes (8): SqliteWriter, CommittedCount, FailedCount, QueueDepth, WriteCommand, CancellationToken, Completion, Work

### Community 63 - "WhenDto / LimitsDto"
Cohesion: 0.13
Nodes (13): WhenDto, AnyAction, CapabilitiesAll, CapabilitiesNone, Direction, Kind, Limits, Mode (+5 more)

### Community 64 - "NodeHost.StartAsync() / TelegramApprovalService"
Cohesion: 0.23
Nodes (4): ApprovalCoordinator, TelegramApprovalService, PeerApiClientFactory, PeerStore

### Community 65 - "TransitionTaskRequest / CancelRequested"
Cohesion: 0.13
Nodes (15): TransitionTaskRequest, CancelRequested, EventPayloadJson, EventType, ExecutorRef, ExpectedSeq, ExpectedState, FinishedAt (+7 more)

### Community 66 - "PolicyWhen / TimeWindowWhen"
Cohesion: 0.13
Nodes (14): PolicyWhen, AnyAction, CapabilitiesAll, CapabilitiesNone, Direction, Kind, Limits, Mode (+6 more)

### Community 68 - "TaskRequest contract / Approval binding to task_id, task_has"
Cohesion: 0.19
Nodes (11): Approval Gateway (out-of-band), Policy Engine (AUTO / ASK / DENY), Identifier formats (node_id, ULID task_id), task_hash canonical projection (RFC 8785 JCS), TaskRequest contract, ApprovalService, pipe server and signer, odw_* MCP tool catalogue, Policy Engine semantics (first match, fail-closed) (+3 more)

### Community 69 - "Lineage / LineageGuard.Evaluate()"
Cohesion: 0.31
Nodes (5): LineageGuard, Lineage, Hops, MaxDepth, LineageGuardTests

### Community 70 - "OpenCodeDtos.cs / OpenCodeSessionRequest"
Cohesion: 0.22
Nodes (7): OpenCodePermissionEffect, Allow, Ask, Deny, OpenCodePermissionRule, EffectValue, OpenCodeSessionRequest

### Community 72 - "Threat model T1-T10 / Dedicated identities odw-svc and odw-a"
Cohesion: 0.27
Nodes (11): Sandbox module, Isolated workspace per task, H3 — Isolation and policies (done), Dedicated identities odw-svc and odw-agent, Job Object resource limits, Network isolation: WFP by SID plus CONNECT proxy, Reparse point and hardlink guard, Threat model T1-T10 (+3 more)

### Community 73 - "ServeHost.cs / NodeHostedService"
Cohesion: 0.21
Nodes (4): Odw.Cli, NodeHostedService, ServeHost, ServeOptions

### Community 75 - "TaskStore.Map() / PersistenceJson"
Cohesion: 0.24
Nodes (3): OutboxSql, PersistenceJson, Options

### Community 76 - "TaskAggregate / TaskStateChange"
Cohesion: 0.17
Nodes (9): TaskAggregate, Changes, CreatedAt, Role, State, StateSeq, TaskHash, TaskId (+1 more)

### Community 77 - "SandboxAgentTests.OpenCode_starts_as_the_agent() / AgentCred"
Cohesion: 0.27
Nodes (4): RunAsCredentials, AgentCredentials, AgentCredentialsStore, SandboxAgentTests

### Community 78 - "ApprovalsConfig / TelegramConfig"
Cohesion: 0.15
Nodes (13): ApprovalsConfig, Enabled, HmacKeyRef, PipeName, SecurityDescriptor, Telegram, TtlMinutes, TelegramConfig (+5 more)

### Community 79 - "Peer protocol v1 / ODW — OpenCode Distributed Worker Network"
Cohesion: 0.17
Nodes (9): Two interfaces: local MCP facade and peer API, Local MCP facade, ODW — OpenCode Distributed Worker Network, Peer API (node to node), OpenCode SSE: data-only lines, no Last-Event-ID, NodeHost with two listeners, RFC 7807 error catalogue, Peer protocol v1 (+1 more)

### Community 81 - "Origin state machine / SQLite schema (tasks, peers, events, "
Cohesion: 0.20
Nodes (10): Origin state machine, Target state machine, TaskState enum (single enum, per-role subsets), OriginProposalService and OriginPoller, Embedded versioned migrations, Outbox (pending_actions) with backoff and dead-letter, SQLite schema (tasks, peers, events, approvals), SqliteWriter single-writer actor (+2 more)

### Community 82 - "PolicyRequest / PolicyDecision"
Cohesion: 0.21
Nodes (7): IPolicyEngine, PolicyDecision, IsAllowed, RequiresHuman, PolicyRequest, Actions, ContractRecordTests

### Community 84 - "Milestone plan D0 through H6 / Lineage and anti-cycle guard"
Cohesion: 0.22
Nodes (11): Planner (delegation scoring), Lineage and anti-cycle guard, Task divisibility via declared split_units, Deterministic planner scoring formula, Leases and heartbeats, D0 — Detailed design (done), H0 — OpenCode verification (partial, H0-6 open), H4 — Full bidirectionality (+3 more)

### Community 86 - "NetworkIsolationWatchdog / CancellationTokenSource"
Cohesion: 0.29
Nodes (3): NetworkIsolationWatchdog, Lost, NetworkIsolationWatchdogTests

### Community 88 - "OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod"
Cohesion: 0.27
Nodes (8): OpenAPI spec served at GET /openapi.json, pinned by SHA-256, Port 0 plus /api/info pid and urls discovery, Prompt endpoint POST /api/session/{id}/prompt, OpenCode v2.3.2 V2 surface under /api (143 routes), odw doctor diagnostics, OpenCodeClient typed V2 HTTP client, OpenCodeServer launcher (loopback, port 0), odw CLI (serve, doctor, approvals, service)

### Community 89 - "PeerRegistry / PeerIdentity"
Cohesion: 0.31
Nodes (4): PeerIdentity, PeerRegistry, Entry, KnownNodes

### Community 90 - "OpenCodeServerOptions / AttachProcess"
Cohesion: 0.20
Nodes (10): OpenCodeServerOptions, AttachProcess, Environment, Executable, Log, PinnedVersion, RunAs, RuntimeRoot (+2 more)

### Community 91 - "PeerApiConfig / PeerTlsConfig"
Cohesion: 0.20
Nodes (10): PeerApiConfig, Address, AllowLoopback, Bind, Port, Tls, PeerTlsConfig, CertificatePath (+2 more)

### Community 92 - "SandboxInspector.Inspect() / SandboxReport"
Cohesion: 0.27
Nodes (5): SandboxInspector, SandboxReport, FullyEnforced, NetworkEnforced, SandboxInspectorTests

### Community 93 - "ODW Node service (one per PC) / Task Manager (state machine,"
Cohesion: 0.22
Nodes (8): Audit Log (append-only), Identity and Trust module, ODW Node service (one per PC), Resource Monitor (CPU, RAM, GPU, tools), Task Manager (state machine, leases, recovery), Odw.slnx solution and project layout, IExecutor executor port, Hash-chained append-only audit log

### Community 96 - "h0-probe.ps1 / Assert-True()"
Cohesion: 0.28
Nodes (3): Assert-True(), Write-Fail(), Write-Pass()

### Community 97 - "InvalidStateTransitionException / InvalidOperationException"
Cohesion: 0.25
Nodes (6): InvalidStateTransitionException, Failure, From, Role, TaskId, Trigger

### Community 99 - "ValidationResult / ValidationResult.cs"
Cohesion: 0.25
Nodes (6): ValidationCodes, ValidationResult, Codes, Issues, IsValid, Valid

### Community 100 - "ConfiguredPeer / PeerFile"
Cohesion: 0.25
Nodes (8): ConfiguredPeer, Address, Capabilities, NodeId, TokenRef, Trust, PeerFile, Peers

### Community 101 - "ModelsConfig / ModelProfileConfig"
Cohesion: 0.25
Nodes (8): ModelProfileConfig, MaxCostUsd, Model, Provider, ModelsConfig, Allowed, Default, Profiles

### Community 102 - "LimitsWhen / RangeWhen"
Cohesion: 0.25
Nodes (6): LimitsWhen, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network, RangeWhen

### Community 103 - "Secret references and DPAPI storage / Stateless Streamable H"
Cohesion: 0.33
Nodes (6): MCP server config under mcp.servers with oauth false, node.json configuration and secret references, Stateless Streamable HTTP MCP transport, policy.json document and condition set, Peer auth: Bearer token plus X-Odw-Node, Secret references and DPAPI storage

### Community 104 - "ExecutionScheduler / H1 — Minimal node and basic delegation"
Cohesion: 0.38
Nodes (6): ExecutionScheduler, H1 — Minimal node and basic delegation, Architecture invariant tests, Testing strategy and golden rules, TwoNodeHarness end-to-end tests, E2E loopback harness (tools/e2e-loopback.ps1)

### Community 106 - "ApprovalDecisionStatus / AlreadyDecided"
Cohesion: 0.29
Nodes (7): ApprovalDecisionStatus, AlreadyDecided, Approved, Denied, Expired, HashMismatch, NotFound

### Community 107 - "RuleDto / WhenDto"
Cohesion: 0.29
Nodes (6): RuleDto, Effect, Id, Priority, Reason, When

### Community 109 - "permission.asked event blocks the prompt indefinitely / Fina"
Cohesion: 0.40
Nodes (4): TaskResult contract, permission.asked event blocks the prompt indefinitely, Final verification and evidence-based outcome, Permission broker (once or reject, never always)

### Community 110 - "LimitsDto / RangeDto"
Cohesion: 0.33
Nodes (5): LimitsDto, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network

### Community 111 - "DocumentDto / RuleDto"
Cohesion: 0.33
Nodes (5): DocumentDto, Default, Rules, Schema, Version

### Community 112 - "ExecutorOutcomeKind / Cancelled"
Cohesion: 0.33
Nodes (6): ExecutorOutcomeKind, Cancelled, Failed, Interrupted, ReviewRequired, Succeeded

### Community 113 - "LineageCheck / DepthExceeded"
Cohesion: 0.33
Nodes (6): LineageCheck, DepthExceeded, Invalid, Loop, Ok, SelfDelegation

### Community 114 - "TelegramApprovalOptions / AllowedChatIds"
Cohesion: 0.33
Nodes (6): TelegramApprovalOptions, AllowedChatIds, AllowedUserIds, BotToken, NotifyInterval, PollTimeoutSeconds

### Community 115 - "TransitionResult / TransitionStatus"
Cohesion: 0.33
Nodes (5): TransitionResult, TransitionStatus, Applied, Conflict, NotFound

### Community 116 - "origin-node.ps1 / Get-OriginProcesses()"
Cohesion: 0.47
Nodes (4): Get-OriginProcesses(), Stop-Origin(), Test-OriginHealth(), Wait-Health()

### Community 117 - "policy.example.json / default"
Cohesion: 0.40
Nodes (4): default, rules, $schema, version

### Community 119 - "TaskKind / TaskStateExtensions.ToWireValue()"
Cohesion: 0.40
Nodes (4): TaskKind, Analysis, CodeChange, TestRun

### Community 120 - "TimeWindowDto / Days"
Cohesion: 0.40
Nodes (5): TimeWindowDto, Days, From, TimeZone, To

### Community 123 - "OpenCodeExecutor.cs / ToolPermissionDecision"
Cohesion: 0.50
Nodes (3): ToolPermissionDecision, AllowOnce, Reject

### Community 125 - "RangeDto / Gte"
Cohesion: 0.67
Nodes (3): RangeDto, Gte, Lte

## Knowledge Gaps
- **654 isolated node(s):** `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.NET.Sdk`, `Task` (+649 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 821 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Odw.Core.Model` connect `Odw.Core.Model / xunit` to `ExecutionScheduler / TaskSnapshot`, `TaskTrigger / TaskTrigger.cs`, `FakeTasks / FakePeers`, `TaskResult / ContractRecordTests.Task_result_records_constru`, `system_text / Odw.Node.Tests`, `TaskLimits / WorkspaceSpec`, `ContractRecordTests.Executor_and_approval_records_construct(`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `PolicyRequest / PolicyDecision`, `WorkspaceMode / Enums.cs`, `ApprovalPipeServer.cs / Odw.Node.Approvals`, `IReadOnlyDictionary / ModelProfile`, `Odw.Node.Persistence / system_globalization`, `OpenCodeExecutor.cs / ToolPermissionDecision`, `NodeHost.cs / system_text_json_nodes`, `NodeConfig / NodeConfig.cs`?**
  _High betweenness centrality (0.061) - this node is a cross-community bridge._
- **What connects `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices` to the rest of the system?**
  _654 weakly-connected nodes found - possible missing edges._
- **Should `PeerApiClient / OpenCodeClientTests.Create()` be split into smaller, more focused modules?**
  _Cohesion score 0.05365296803652968 - nodes in this community are weakly interconnected._
- **Why does `NodeHost` connect `NodeHost / NodeHost.BuildPeer()` to `ExecutionScheduler / TaskSnapshot`, `SqliteDatabase / TimeProvider`, `OpenCodeTaskRunner.RunAsync() / OpenCodeClient`, `ModelProxy / ModelProxyOptions`, `NodeHostOptions / PeerRegistration`, `McpServer / McpServer.HandleAsync()`, `TaskStore / TestDatabase.Create()`, `NodeHost.cs / system_text_json_nodes`, `Fixture.StartAsync() / NodeHostTests`, `ApprovalService / ApprovalService.DecideAsync()`, `NodeBootstrap.StartAsync() / NodeConfigLoader.Load()`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `ValueTask / IAsyncDisposable`, `OriginPoller / OriginPoller.RefreshOnceAsync()`, `ApprovalPipeServer / ApprovalPipeServer.DispatchAsync()`, `TaskCoordinator.TransitionAsync() / OpenCodeTaskRunnerIntegr`, `NodeHost.StartAsync() / TelegramApprovalService`, `OriginEndToEndTests / OriginEndToEndTests.StartAsync()`, `ServeHost.cs / NodeHostedService`, `PeerRegistry / PeerIdentity`?**
  _High betweenness centrality (0.033) - this node is a cross-community bridge._
- **Should `NodeRuntime / Ulid` be split into smaller, more focused modules?**
  _Cohesion score 0.05875251509054326 - nodes in this community are weakly interconnected._
- **Why does `NodeConfig` connect `NodeConfig / NodeConfig.cs` to `TaskRequest / List`, `ConfiguredPeer / PeerFile`, `ModelsConfig / ModelProfileConfig`, `NodeBootstrap.StartAsync() / NodeConfigLoader.Load()`, `SandboxConfig / SandboxGuard.Validate()`, `ApprovalsConfig / TelegramConfig`, `PeerApiConfig / PeerTlsConfig`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **Should `JsonElement / TaskContractReader` be split into smaller, more focused modules?**
  _Cohesion score 0.08382936507936507 - nodes in this community are weakly interconnected._