# Graph Report - ODW — OpenCode Distributed Worker Network  (2026-11-26)

## Corpus Check
- 206 files · ~170,970 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 4 file(s) not represented in the graph (top: (none) 2, .props 2)

## Summary
- 2609 nodes · 7627 edges · 131 communities (106 shown, 25 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 992 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- CancellationToken / ExecutionScheduler
- ApprovalPipeServer / ValueTask
- DoctorOptions / NodeDoctor.RunAsync()
- Odw.slnx / Odw.Node.csproj
- IntPtr / WfpNetworkPolicy
- Odw.Core.Model / Odw.Node.Persistence.Tasks
- system_text / system_text_json
- ModelProxy / ModelProxyOptions
- JsonObject / OriginTools.ProposeAsync()
- TaskHash.BuildCanonicalJson() / CanonicalJson.cs
- TaskRole / TaskCoordinator.TransitionAsync()
- Odw.Node.Persistence / Odw.Node.Tests
- SqliteWriter.WriteAsync() / SqliteDatabase
- McpServerTests / McpServerTests.CreateServer()
- Ulid / TimeProvider
- Fact / TestTasks.Valid()
- TaskStore / TestDatabase.Create()
- NodeConfig / NodeConfig.cs
- McpServer / McpServer.HandleAsync()
- FakeOpenCodeClient / OpenCodeExecutor.StreamAsync()
- NodeHost.StartAsync() / ApprovalService
- DateTimeOffset / SqliteConnection
- NodeHostOptions / Accepts
- TaskState / TaskStateExtensions
- OpenCodeServer / OpenCodeServer.StartAsync()
- OpenCodeClient / IOpenCodeClient
- NodeHost.cs / system_text_json_nodes
- CreateTaskRequest / TaskKind
- TaskResult / TaskResult.cs
- OutboxStore / NewPendingAction
- CliApplication / CliApplication.DoctorAsync()
- Odw.Core.Contracts / NodeBootstrap.cs
- JobObject / JobLimits
- OpenCodeIntegrationTests.Real_server_completes_a_read_only_p
- DatabaseMigrator.Migrate() / MigrationTests.Failed_migration
- NodeBootstrap.StartAsync() / NodeConfigLoader.Load()
- TaskRequest / TaskValidator
- PolicyWhen / PolicyModels.cs
- TaskTrigger / TaskTrigger.cs
- SandboxConfig / SandboxGuard.Validate()
- NodeHost / NodeHost.BuildPeer()
- SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()
- OpenCodeDtos.cs / OpenCodeSession
- OpenCodeServerOptions / SandboxAgentTests.OpenCode_reaches_t
- List / Func
- JsonElement / TaskContractReader
- OpenCodeTaskRunner.RunAsync() / TaskLimits
- OpenCodeTaskRunnerOptions / ToolPermissionHandler
- PolicyEngine.FromJson() / PolicyEngine.Evaluate()
- AgentRuntime / AgentRuntimeOptions
- Task / Fixture.StartAsync()
- PeerApiClient / PeerApiClient.PostTaskAsync()
- OriginProposalService / OriginProposalService.ProposeAsync()
- WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests
- IReadOnlyList / OpenCodeClient.GetMessagesAsync()
- Lineage / LineageGuard.Evaluate()
- TransitionContext / TaskStateMachine.TryTransition()
- McpHttpTests / McpHttpFixture
- TaskAggregate / TaskStateMachine
- WorkspaceSpec / WorkspaceMode
- ModelProfile / ModelProfileResolver.TryResolve()
- PeerEndpoints.CreateAsync() / PeerEndpoints
- TelegramClient / StubHandler
- Odw.Node.Hosting / CliApplication.cs
- ContractFormat / Regex
- WhenDto / LimitsDto
- OpenCodeExecutor / IOpenCodeClient.ReplyPermissionAsync()
- PolicyEngine / PolicyRequest
- TransitionTaskRequest / CancelRequested
- OriginEndToEndTests / OriginEndToEndTests.StartAsync()
- TaskRequest contract / Approval binding to task_id, task_has
- xunit / Odw.Core.Tests
- OpenCodeClientTests.Create() / OpenCodeClientTests
- NodeConfigTests / NodeConfigTests.Folder()
- Threat model T1-T10 / Dedicated identities odw-svc and odw-a
- SqlitePeerDirectory / SqlitePeerDirectory.ParseDetails()
- OriginPoller / OriginPoller.RefreshOnceAsync()
- TimeSpan / ExecutorOutcomeKind
- ApprovalsConfig / TelegramConfig
- Peer protocol v1 / ODW — OpenCode Distributed Worker Network
- Origin state machine / SQLite schema (tasks, peers, events, 
- IApprovalChannel.cs / ApprovalDecisionKind
- ApprovalEvidence / ExecutorSpec
- Milestone plan D0 through H6 / Lineage and anti-cycle guard
- ExecutorEventKind / Completed
- NodeRuntime / Coordinator
- OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod
- PeerRegistry / PeerIdentity
- InvalidStateTransitionException / TaskAggregate.Apply()
- Enums.cs / OpenCodeExecutor.SendAsync()
- PeerStatusSnapshot / IPeerDirectory
- PeerApiConfig / PeerTlsConfig
- SandboxInspector.Inspect() / SandboxReport
- ODW Node service (one per PC) / Task Manager (state machine,
- ModelsConfig / Dictionary
- Fixture / HttpClient
- TelegramApprovalService / TelegramClient.GetUpdatesAsync()
- h0-probe.ps1 / Assert-True()
- Exception / PeerApiClientFactory.Create()
- JsonSerializerOptions / StringOrArrayConverter
- PolicyEffect / PolicyDecision
- ValidationResult / ValidationResult.cs
- ConfiguredPeer / PeerFile
- PeerRegistration / TlsTests.Peer_api_uses_https_when_a_certi
- Secret references and DPAPI storage / Stateless Streamable H
- ExecutionScheduler / H1 — Minimal node and basic delegation
- StubHandler / StubHandler.SendAsync()
- ApprovalDecisionStatus / AlreadyDecided
- Goal / Accepted
- RuleDto / WhenDto
- WorkspaceChangesTests / WorkspaceChangesTests.Commits_and_re
- permission.asked event blocks the prompt indefinitely / Fina
- LimitsDto / RangeDto
- DocumentDto / RuleDto
- TelegramApprovalOptions / AllowedChatIds
- TaskContractWriter.Write() / TaskContractWriter
- TransitionResult / TransitionStatus
- origin-node.ps1 / Get-OriginProcesses()
- policy.example.json / default
- TimeWindowDto / Days
- ApprovalOptions / OriginProposalService.OriginProposalServic
- RangeDto / Gte
- WfpNetworkPolicyTests.Agent_is_blocked_from_internet_but_can

## God Nodes (most connected - your core abstractions)
1. `Odw.Core.Model` - 71 edges
2. `NodeHostOptions` - 58 edges
3. `TaskState` - 57 edges
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

## Communities (131 total, 25 thin omitted)

### Community 0 - "CancellationToken / ExecutionScheduler"
Cohesion: 0.06
Nodes (42): ExecutionResult, ExecutionSchedulerOptions, MaxConcurrentTasks, PollInterval, ITaskExecutionRunner, ExecutionScheduler, TransitionOptions, CancelRequested (+34 more)

### Community 1 - "ApprovalPipeServer / ValueTask"
Cohesion: 0.05
Nodes (17): NodeHostedService, ServeHost, ServeOptions, ApprovalPipeOptions, Name, SecurityDescriptor, ApprovalPipeServer, PipeName (+9 more)

### Community 2 - "DoctorOptions / NodeDoctor.RunAsync()"
Cohesion: 0.06
Nodes (24): BindPolicy, DoctorCheck, DoctorOptions, DatabasePath, ExpectedSpecSha256, McpBind, McpPort, McpToken (+16 more)

### Community 3 - "Odw.slnx / Odw.Node.csproj"
Cohesion: 0.06
Nodes (38): Microsoft.Data.Sqlite, Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.WindowsServices, System.Security.Cryptography.ProtectedData, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+30 more)

### Community 4 - "IntPtr / WfpNetworkPolicy"
Cohesion: 0.12
Nodes (11): WfpNetworkPolicy, DefaultStatePath, FwpAction0, FwpByteBlob, FwpConditionValue0, FwpmDisplayData0, FwpmFilter0, FwpmFilterCondition0 (+3 more)

### Community 5 - "Odw.Core.Model / Odw.Node.Persistence.Tasks"
Cohesion: 0.12
Nodes (14): Odw.Core.Validation, Odw.Node.Scheduling, Odw.PeerApi, Odw.Node.Contracts, Odw.Core.Hashing, Odw.Node.Origins, Odw.Core.Delegation, Odw.Node.Tasks (+6 more)

### Community 6 - "system_text / system_text_json"
Cohesion: 0.08
Nodes (6): Odw.Approvals, Odw.Node.Approvals, Odw.Node.Diagnostics, Odw.Node.Persistence.Queries, TestModels, FreeOrFallback

### Community 7 - "ModelProxy / ModelProxyOptions"
Cohesion: 0.09
Nodes (15): ModelProxy, Port, Token, Url, ModelProxyOptions, AddressFilter, AllowedHosts, AllowedPorts (+7 more)

### Community 8 - "JsonObject / OriginTools.ProposeAsync()"
Cohesion: 0.13
Nodes (7): ITaskQuery, McpTool, McpToolResult, JsonTools, OriginTools, PeerTools, TaskTools

### Community 9 - "TaskHash.BuildCanonicalJson() / CanonicalJson.cs"
Cohesion: 0.11
Nodes (17): CanonicalArray, Count, this[], CanonicalBoolean, Value, CanonicalJson, CanonicalNode, CanonicalNull (+9 more)

### Community 10 - "TaskRole / TaskCoordinator.TransitionAsync()"
Cohesion: 0.12
Nodes (12): ApprovalRequest, TaskRole, Origin, Target, TaskStateChange, TaskEventLog, TaskEventRecord, TaskSnapshot (+4 more)

### Community 11 - "Odw.Node.Persistence / Odw.Node.Tests"
Cohesion: 0.09
Nodes (10): Odw.Node.Tests, Odw.Node.Persistence.Peers, Odw.Node.Persistence.Outbox, Odw.Node.Persistence, ApprovalStatus, OutboxActions, OutboxStatus, ContractJson (+2 more)

### Community 12 - "SqliteWriter.WriteAsync() / SqliteDatabase"
Cohesion: 0.07
Nodes (22): SqliteDatabase, Path, Writer, SqliteDatabaseOptions, AppVersion, MigrationBackupDirectory, Pooling, TimeProvider (+14 more)

### Community 13 - "McpServerTests / McpServerTests.CreateServer()"
Cohesion: 0.13
Nodes (10): TaskEventView, TaskResultView, TaskStatusView, McpServerTests, FakePeers, Peers, FakeTasks, Results (+2 more)

### Community 14 - "Ulid / TimeProvider"
Cohesion: 0.10
Nodes (6): Ulid, IsEmpty, MonotonicState, Timestamp, UlidTests, FixedTimeProvider

### Community 15 - "Fact / TestTasks.Valid()"
Cohesion: 0.18
Nodes (3): TaskHashTests, TaskValidatorTests, TestTasks

### Community 16 - "TaskStore / TestDatabase.Create()"
Cohesion: 0.21
Nodes (7): ChainVerification, Valid, StartupRecovery, TaskStore, RecoveryTests, TaskStoreTests, TestRows

### Community 17 - "NodeConfig / NodeConfig.cs"
Cohesion: 0.06
Nodes (34): McpConfig, Bind, Port, TokenRef, NodeConfig, Accepts, Approvals, Capabilities (+26 more)

### Community 18 - "McpServer / McpServer.HandleAsync()"
Cohesion: 0.12
Nodes (11): ApprovalPipeClient, JsonRpc, ErrorCodes, McpEndpoint, McpOptions, MaxBodyBytes, Path, Token (+3 more)

### Community 19 - "FakeOpenCodeClient / OpenCodeExecutor.StreamAsync()"
Cohesion: 0.13
Nodes (13): OpenCodeExecutorTests, EventFactory, FakeOpenCodeClient, BaseAddress, BlockAfterEvents, Events, Info, Interrupts (+5 more)

### Community 20 - "NodeHost.StartAsync() / ApprovalService"
Cohesion: 0.16
Nodes (7): ApprovalCoordinator, ApprovalDecisionResult, ApprovalRecord, ApprovalService, ApprovalSigner, PeerStore, ApprovalServiceTests

### Community 21 - "DateTimeOffset / SqliteConnection"
Cohesion: 0.19
Nodes (5): AuditLog, ChainHash, PersistenceJson, Options, RecoveryResult

### Community 22 - "NodeHostOptions / Accepts"
Cohesion: 0.06
Nodes (32): NodeHostOptions, Accepts, AllowLoopbackPeerBind, ApprovalOptions, ApprovalPipeName, ApprovalPipeSecurityDescriptor, ApprovalSigner, Capabilities (+24 more)

### Community 23 - "TaskState / TaskStateExtensions"
Cohesion: 0.09
Nodes (21): TaskStateExtensions, TaskState, Accepted, Approved, AwaitingLocalApproval, Cancelled, Cancelling, Completed (+13 more)

### Community 24 - "OpenCodeServer / OpenCodeServer.StartAsync()"
Cohesion: 0.12
Nodes (7): OpenCodeConnection, OpenCodeServerInfo, OpenCodeServer, BaseUrl, Connection, Info, ProcessId

### Community 25 - "OpenCodeClient / IOpenCodeClient"
Cohesion: 0.11
Nodes (10): IOpenCodeClient, BaseAddress, OpenCodeEvent, OpenCodeClient, BaseAddress, OpenCodeApiException, Body, Method (+2 more)

### Community 26 - "NodeHost.cs / system_text_json_nodes"
Cohesion: 0.13
Nodes (5): Odw.Node.PeerApi, Odw.Mcp.Tests, Odw.Mcp.Abstractions, Odw.Mcp, Odw.Mcp.Tools

### Community 27 - "CreateTaskRequest / TaskKind"
Cohesion: 0.07
Nodes (24): TaskKind, Analysis, CodeChange, TestRun, CreateTaskRequest, CounterpartNode, CreatedAt, ExecutorRef (+16 more)

### Community 28 - "TaskResult / TaskResult.cs"
Cohesion: 0.08
Nodes (25): TestStatus, Failed, Passed, Skipped, ArtifactRef, TaskError, TaskResult, Artifacts (+17 more)

### Community 29 - "OutboxStore / NewPendingAction"
Cohesion: 0.18
Nodes (10): NewPendingAction, PendingAction, OutboxSql, OutboxFailOutcome, DeadLettered, NotFound, RetryScheduled, OutboxStore (+2 more)

### Community 31 - "Odw.Core.Contracts / NodeBootstrap.cs"
Cohesion: 0.13
Nodes (7): Odw.Core.Contracts, Odw.Sandbox.Workspaces, Odw.Sandbox.Tests, Odw.Sandbox.Windows, Odw.Executors.OpenCode, Odw.Policy.Tests, Odw.Sandbox.Network

### Community 32 - "JobObject / JobLimits"
Cohesion: 0.11
Nodes (10): JobLimits, ActiveProcessLimit, CpuTime, JobMemoryBytes, ProcessMemoryBytes, JobObject, IoCounters, JobObjectBasicLimitInformation (+2 more)

### Community 33 - "OpenCodeIntegrationTests.Real_server_completes_a_read_only_p"
Cohesion: 0.11
Nodes (15): ExecutorLimits, ExecutorSession, ExecutorSnapshot, ExecutorStartRequest, IExecutor, ToolEffect, Allow, Ask (+7 more)

### Community 34 - "DatabaseMigrator.Migrate() / MigrationTests.Failed_migration"
Cohesion: 0.13
Nodes (8): DatabaseMigrator, Migration, MigrationOutcome, SchemaVersion, MigrationTests, SqliteWriterTests, TestTime, Now

### Community 35 - "NodeBootstrap.StartAsync() / NodeConfigLoader.Load()"
Cohesion: 0.21
Nodes (5): NodeConfigLoader, NodeSettings, TelegramSettings, NodeBootstrap, NodeConfigurationException

### Community 36 - "TaskRequest / TaskValidator"
Cohesion: 0.13
Nodes (17): TaskRequest, Approval, CreatedAt, Executor, ExpiresAt, Instructions, Kind, Limits (+9 more)

### Community 37 - "PolicyWhen / PolicyModels.cs"
Cohesion: 0.08
Nodes (22): LimitsWhen, MaxCostUsd, MaxMemoryGb, MaxMinutes, Network, PolicyDocument, PolicyRule, PolicyWhen (+14 more)

### Community 38 - "TaskTrigger / TaskTrigger.cs"
Cohesion: 0.07
Nodes (26): TaskTrigger, ApprovalDenied, ApprovalExpired, ApprovalGranted, CancelTimeout, LeaseExpired, OperatorDiscard, OperatorInspect (+18 more)

### Community 39 - "SandboxConfig / SandboxGuard.Validate()"
Cohesion: 0.12
Nodes (21): SandboxConfig, AgentAccount, AgentConfigPath, AgentSecretPath, ApplyAcls, NetworkAllowedHosts, NetworkIsolation, NetworkProxyPort (+13 more)

### Community 40 - "NodeHost / NodeHost.BuildPeer()"
Cohesion: 0.10
Nodes (12): NodeHost, ApprovalCoordinator, Approvals, Coordinator, McpBaseAddress, Options, PeerBaseAddress, Peers (+4 more)

### Community 41 - "SqliteTaskQuery / SqliteTaskQuery.GetResultAsync()"
Cohesion: 0.15
Nodes (8): TaskOutcome, Cancelled, Failed, ReviewRequired, Succeeded, SqliteTaskQuery, ResultDetails, SqliteFacadeTests

### Community 42 - "OpenCodeDtos.cs / OpenCodeSession"
Cohesion: 0.13
Nodes (10): OpenCodePermissionEffect, Allow, Ask, Deny, OpenCodePermissionRule, EffectValue, OpenCodeProtocolException, OpenCodeSession (+2 more)

### Community 43 - "OpenCodeServerOptions / SandboxAgentTests.OpenCode_reaches_t"
Cohesion: 0.14
Nodes (14): OpenCodeServerOptions, AttachProcess, Environment, Executable, Log, PinnedVersion, RunAs, RuntimeRoot (+6 more)

### Community 44 - "List / Func"
Cohesion: 0.27
Nodes (4): PolicyDocumentLoader, PolicyIssue, PolicyValidationException, Issues

### Community 45 - "JsonElement / TaskContractReader"
Cohesion: 0.33
Nodes (3): TaskContractException, Field, TaskContractReader

### Community 46 - "OpenCodeTaskRunner.RunAsync() / TaskLimits"
Cohesion: 0.15
Nodes (10): ExecutorEvent, TaskLimits, MaxCostUsd, MaxMemoryGb, MaxMinutes, MaxProcesses, Network, ExecutionRequest (+2 more)

### Community 47 - "OpenCodeTaskRunnerOptions / ToolPermissionHandler"
Cohesion: 0.08
Nodes (23): ToolPermissionHandler, OpenCodeTaskRunnerOptions, AgentAccount, AgentConfigTemplate, AgentRuntimeRoot, DefaultModelProfile, Executable, ExecutionTimeout (+15 more)

### Community 49 - "AgentRuntime / AgentRuntimeOptions"
Cohesion: 0.13
Nodes (9): AgentRuntime, Path, AgentRuntimeOptions, AgentAccount, ApplyAcls, ConfigTemplate, Root, ServiceAccount (+1 more)

### Community 51 - "PeerApiClient / PeerApiClient.PostTaskAsync()"
Cohesion: 0.14
Nodes (6): PeerApiClient, BaseAddress, PeerPostResult, Accepted, PeerResultView, PeerTaskView

### Community 52 - "OriginProposalService / OriginProposalService.ProposeAsync()"
Cohesion: 0.19
Nodes (8): ITaskProposalService, ProposalInput, ProposalOutcome, IsError, OriginProposalService, FakeProposals, Last, Outcome

### Community 53 - "WorkspaceGuard.EnsureNoReparsePoints() / WorkspaceGuardTests"
Cohesion: 0.18
Nodes (4): WorkspaceGuard, ByHandleFileInformation, WorkspaceGuardException, WorkspaceGuardTests

### Community 54 - "IReadOnlyList / OpenCodeClient.GetMessagesAsync()"
Cohesion: 0.21
Nodes (4): OpenCodeJson, OpenCodeMessage, Text, OpenCodePermissionRequest

### Community 55 - "Lineage / LineageGuard.Evaluate()"
Cohesion: 0.18
Nodes (11): LineageCheck, DepthExceeded, Invalid, Loop, Ok, SelfDelegation, LineageGuard, Lineage (+3 more)

### Community 56 - "TransitionContext / TaskStateMachine.TryTransition()"
Cohesion: 0.15
Nodes (9): TransitionContext, ApprovalExpired, ApprovalMatches, Default, LeaseExpired, ResourcesAvailable, Satisfied, ValidationViolated (+1 more)

### Community 57 - "McpHttpTests / McpHttpFixture"
Cohesion: 0.18
Nodes (3): McpHttpFixture, Client, McpHttpTests

### Community 58 - "TaskAggregate / TaskStateMachine"
Cohesion: 0.13
Nodes (10): TaskAggregate, Changes, CreatedAt, Role, State, StateSeq, TaskHash, TaskId (+2 more)

### Community 59 - "WorkspaceSpec / WorkspaceMode"
Cohesion: 0.12
Nodes (14): WorkspaceMode, ReadOnly, ReadWriteWorktree, WorkspaceStrategy, Clone, Snapshot, Worktree, WorkspaceSpec (+6 more)

### Community 60 - "ModelProfile / ModelProfileResolver.TryResolve()"
Cohesion: 0.18
Nodes (7): ModelCatalog, DefaultProfiles, Free, ModelProfile, QualifiedName, ModelProfileResolver, ModelProfileResolverTests

### Community 62 - "TelegramClient / StubHandler"
Cohesion: 0.29
Nodes (4): TelegramClient, TelegramClientTests, StubHandler, Calls

### Community 63 - "Odw.Node.Hosting / CliApplication.cs"
Cohesion: 0.20
Nodes (3): Odw.Cli, Odw.Node.Hosting, Odw.Node.Configuration

### Community 65 - "WhenDto / LimitsDto"
Cohesion: 0.13
Nodes (13): WhenDto, AnyAction, CapabilitiesAll, CapabilitiesNone, Direction, Kind, Limits, Mode (+5 more)

### Community 66 - "OpenCodeExecutor / IOpenCodeClient.ReplyPermissionAsync()"
Cohesion: 0.17
Nodes (8): OpenCodePermissionReply, Always, Once, Reject, OpenCodeExecutor, ToolPermissionDecision, AllowOnce, Reject

### Community 67 - "PolicyEngine / PolicyRequest"
Cohesion: 0.19
Nodes (5): IPolicyEngine, PolicyRequest, Actions, PolicyEngine, Document

### Community 68 - "TransitionTaskRequest / CancelRequested"
Cohesion: 0.13
Nodes (15): TransitionTaskRequest, CancelRequested, EventPayloadJson, EventType, ExecutorRef, ExpectedSeq, ExpectedState, FinishedAt (+7 more)

### Community 70 - "TaskRequest contract / Approval binding to task_id, task_has"
Cohesion: 0.19
Nodes (11): Approval Gateway (out-of-band), Policy Engine (AUTO / ASK / DENY), Identifier formats (node_id, ULID task_id), task_hash canonical projection (RFC 8785 JCS), TaskRequest contract, ApprovalService, pipe server and signer, odw_* MCP tool catalogue, Policy Engine semantics (first match, fail-closed) (+3 more)

### Community 74 - "Threat model T1-T10 / Dedicated identities odw-svc and odw-a"
Cohesion: 0.27
Nodes (11): Sandbox module, Isolated workspace per task, H3 — Isolation and policies (done), Dedicated identities odw-svc and odw-agent, Job Object resource limits, Network isolation: WFP by SID plus CONNECT proxy, Reparse point and hardlink guard, Threat model T1-T10 (+3 more)

### Community 76 - "OriginPoller / OriginPoller.RefreshOnceAsync()"
Cohesion: 0.29
Nodes (3): OriginPoller, OriginPollerOptions, PollInterval

### Community 78 - "TimeSpan / ExecutorOutcomeKind"
Cohesion: 0.19
Nodes (7): ExecutorOutcome, ExecutorOutcomeKind, Cancelled, Failed, Interrupted, ReviewRequired, Succeeded

### Community 79 - "ApprovalsConfig / TelegramConfig"
Cohesion: 0.15
Nodes (13): ApprovalsConfig, Enabled, HmacKeyRef, PipeName, SecurityDescriptor, Telegram, TtlMinutes, TelegramConfig (+5 more)

### Community 80 - "Peer protocol v1 / ODW — OpenCode Distributed Worker Network"
Cohesion: 0.17
Nodes (9): Two interfaces: local MCP facade and peer API, Local MCP facade, ODW — OpenCode Distributed Worker Network, Peer API (node to node), OpenCode SSE: data-only lines, no Last-Event-ID, NodeHost with two listeners, RFC 7807 error catalogue, Peer protocol v1 (+1 more)

### Community 81 - "Origin state machine / SQLite schema (tasks, peers, events, "
Cohesion: 0.20
Nodes (10): Origin state machine, Target state machine, TaskState enum (single enum, per-role subsets), OriginProposalService and OriginPoller, Embedded versioned migrations, Outbox (pending_actions) with backoff and dead-letter, SQLite schema (tasks, peers, events, approvals), SqliteWriter single-writer actor (+2 more)

### Community 82 - "IApprovalChannel.cs / ApprovalDecisionKind"
Cohesion: 0.20
Nodes (9): ApprovalDecisionKind, Approved, Denied, Expired, ApprovalOutcome, ApprovalScope, Task, ToolUse (+1 more)

### Community 83 - "ApprovalEvidence / ExecutorSpec"
Cohesion: 0.17
Nodes (11): ApprovalEvidence, ApprovalId, ApprovedAt, ApprovedBy, Nonce, Signature, TaskHash, ExecutorSpec (+3 more)

### Community 84 - "Milestone plan D0 through H6 / Lineage and anti-cycle guard"
Cohesion: 0.22
Nodes (11): Planner (delegation scoring), Lineage and anti-cycle guard, Task divisibility via declared split_units, Deterministic planner scoring formula, Leases and heartbeats, D0 — Detailed design (done), H0 — OpenCode verification (partial, H0-6 open), H4 — Full bidirectionality (+3 more)

### Community 85 - "ExecutorEventKind / Completed"
Cohesion: 0.18
Nodes (11): ExecutorEventKind, Completed, Failed, Heartbeat, Interrupted, PermissionAsked, Started, TextDelta (+3 more)

### Community 86 - "NodeRuntime / Coordinator"
Cohesion: 0.18
Nodes (11): NodeRuntime, Coordinator, Database, Directory, Options, Outbox, Peers, Policy (+3 more)

### Community 87 - "OpenCode v2.3.2 V2 surface under /api (143 routes) / OpenCod"
Cohesion: 0.27
Nodes (8): OpenAPI spec served at GET /openapi.json, pinned by SHA-256, Port 0 plus /api/info pid and urls discovery, Prompt endpoint POST /api/session/{id}/prompt, OpenCode v2.3.2 V2 surface under /api (143 routes), odw doctor diagnostics, OpenCodeClient typed V2 HTTP client, OpenCodeServer launcher (loopback, port 0), odw CLI (serve, doctor, approvals, service)

### Community 88 - "PeerRegistry / PeerIdentity"
Cohesion: 0.31
Nodes (4): PeerIdentity, PeerRegistry, Entry, KnownNodes

### Community 89 - "InvalidStateTransitionException / TaskAggregate.Apply()"
Cohesion: 0.24
Nodes (6): InvalidStateTransitionException, Failure, From, Role, TaskId, Trigger

### Community 90 - "Enums.cs / OpenCodeExecutor.SendAsync()"
Cohesion: 0.22
Nodes (6): MessageDelivery, Queue, Steer, NetworkAccess, Allow, None

### Community 91 - "PeerStatusSnapshot / IPeerDirectory"
Cohesion: 0.31
Nodes (3): IPeerDirectory, PeerStatusSnapshot, IsOnline

### Community 92 - "PeerApiConfig / PeerTlsConfig"
Cohesion: 0.20
Nodes (10): PeerApiConfig, Address, AllowLoopback, Bind, Port, Tls, PeerTlsConfig, CertificatePath (+2 more)

### Community 93 - "SandboxInspector.Inspect() / SandboxReport"
Cohesion: 0.27
Nodes (5): SandboxInspector, SandboxReport, FullyEnforced, NetworkEnforced, SandboxInspectorTests

### Community 94 - "ODW Node service (one per PC) / Task Manager (state machine,"
Cohesion: 0.22
Nodes (8): Audit Log (append-only), Identity and Trust module, ODW Node service (one per PC), Resource Monitor (CPU, RAM, GPU, tools), Task Manager (state machine, leases, recovery), Odw.slnx solution and project layout, IExecutor executor port, Hash-chained append-only audit log

### Community 95 - "ModelsConfig / Dictionary"
Cohesion: 0.22
Nodes (8): ModelProfileConfig, MaxCostUsd, Model, Provider, ModelsConfig, Allowed, Default, Profiles

### Community 96 - "Fixture / HttpClient"
Cohesion: 0.25
Nodes (4): PeerApiClientFactory, Fixture, Folder, Host

### Community 99 - "h0-probe.ps1 / Assert-True()"
Cohesion: 0.28
Nodes (3): Assert-True(), Write-Fail(), Write-Pass()

### Community 100 - "Exception / PeerApiClientFactory.Create()"
Cohesion: 0.32
Nodes (3): OriginException, PeerConnection, PeerProtocolException

### Community 102 - "PolicyEffect / PolicyDecision"
Cohesion: 0.25
Nodes (7): PolicyDecision, IsAllowed, RequiresHuman, PolicyEffect, Ask, Auto, Deny

### Community 103 - "ValidationResult / ValidationResult.cs"
Cohesion: 0.25
Nodes (6): ValidationCodes, ValidationResult, Codes, Issues, IsValid, Valid

### Community 104 - "ConfiguredPeer / PeerFile"
Cohesion: 0.25
Nodes (8): ConfiguredPeer, Address, Capabilities, NodeId, TokenRef, Trust, PeerFile, Peers

### Community 107 - "Secret references and DPAPI storage / Stateless Streamable H"
Cohesion: 0.33
Nodes (6): MCP server config under mcp.servers with oauth false, node.json configuration and secret references, Stateless Streamable HTTP MCP transport, policy.json document and condition set, Peer auth: Bearer token plus X-Odw-Node, Secret references and DPAPI storage

### Community 108 - "ExecutionScheduler / H1 — Minimal node and basic delegation"
Cohesion: 0.38
Nodes (6): ExecutionScheduler, H1 — Minimal node and basic delegation, Architecture invariant tests, Testing strategy and golden rules, TwoNodeHarness end-to-end tests, E2E loopback harness (tools/e2e-loopback.ps1)

### Community 109 - "StubHandler / StubHandler.SendAsync()"
Cohesion: 0.38
Nodes (3): RecordedCall, StubHandler, Calls

### Community 110 - "ApprovalDecisionStatus / AlreadyDecided"
Cohesion: 0.29
Nodes (7): ApprovalDecisionStatus, AlreadyDecided, Approved, Denied, Expired, HashMismatch, NotFound

### Community 111 - "Goal / Accepted"
Cohesion: 0.29
Nodes (7): Goal, Accepted, Cancelled, Done, Failed, Rejected, Running

### Community 112 - "RuleDto / WhenDto"
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

### Community 120 - "TransitionResult / TransitionStatus"
Cohesion: 0.33
Nodes (5): TransitionResult, TransitionStatus, Applied, Conflict, NotFound

### Community 121 - "origin-node.ps1 / Get-OriginProcesses()"
Cohesion: 0.47
Nodes (4): Get-OriginProcesses(), Stop-Origin(), Test-OriginHealth(), Wait-Health()

### Community 122 - "policy.example.json / default"
Cohesion: 0.40
Nodes (4): default, rules, $schema, version

### Community 123 - "TimeWindowDto / Days"
Cohesion: 0.40
Nodes (5): TimeWindowDto, Days, From, TimeZone, To

### Community 128 - "RangeDto / Gte"
Cohesion: 0.67
Nodes (3): RangeDto, Gte, Lte

## Knowledge Gaps
- **645 isolated node(s):** `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.NET.Sdk`, `Task` (+640 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 824 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Odw.Core.Model` connect `Odw.Core.Model / Odw.Node.Persistence.Tasks` to `OpenCodeIntegrationTests.Real_server_completes_a_read_only_p`, `NodeHost.cs / system_text_json_nodes`, `system_text / system_text_json`, `TaskTrigger / TaskTrigger.cs`, `xunit / Odw.Core.Tests`, `Odw.Node.Persistence / Odw.Node.Tests`, `TaskResult / TaskResult.cs`, `NodeConfig / NodeConfig.cs`, `IApprovalChannel.cs / ApprovalDecisionKind`, `ApprovalEvidence / ExecutorSpec`, `OriginProposalService / OriginProposalService.ProposeAsync()`, `Enums.cs / OpenCodeExecutor.SendAsync()`, `ModelProfile / ModelProfileResolver.TryResolve()`, `Odw.Core.Contracts / NodeBootstrap.cs`?**
  _High betweenness centrality (0.064) - this node is a cross-community bridge._
- **What connects `Microsoft.NET.Sdk`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices` to the rest of the system?**
  _645 weakly-connected nodes found - possible missing edges._
- **Should `CancellationToken / ExecutionScheduler` be split into smaller, more focused modules?**
  _Cohesion score 0.06098901098901099 - nodes in this community are weakly interconnected._
- **Why does `NodeConfig` connect `NodeConfig / NodeConfig.cs` to `NodeBootstrap.StartAsync() / NodeConfigLoader.Load()`, `SandboxConfig / SandboxGuard.Validate()`, `ConfiguredPeer / PeerFile`, `List / Func`, `ApprovalsConfig / TelegramConfig`, `PeerApiConfig / PeerTlsConfig`, `ModelsConfig / Dictionary`?**
  _High betweenness centrality (0.034) - this node is a cross-community bridge._
- **Should `ApprovalPipeServer / ValueTask` be split into smaller, more focused modules?**
  _Cohesion score 0.05081967213114754 - nodes in this community are weakly interconnected._
- **Why does `PolicyDocumentLoader` connect `List / Func` to `ContractFormat / Regex`, `RangeDto / Gte`, `WhenDto / LimitsDto`, `JsonSerializerOptions / StringOrArrayConverter`, `Odw.Core.Model / Odw.Node.Persistence.Tasks`, `RuleDto / WhenDto`, `LimitsDto / RangeDto`, `DocumentDto / RuleDto`, `TimeWindowDto / Days`?**
  _High betweenness centrality (0.032) - this node is a cross-community bridge._
- **Should `DoctorOptions / NodeDoctor.RunAsync()` be split into smaller, more focused modules?**
  _Cohesion score 0.06271186440677966 - nodes in this community are weakly interconnected._