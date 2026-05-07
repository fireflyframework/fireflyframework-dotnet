// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace FireflyFramework.Orchestration.Core;

public enum ExecutionStatus
{
    Pending, Running, Waiting, Suspended, Completed, Failed, Cancelled, TimedOut,
    Trying, Confirming, Confirmed, Cancelling, Canceled, Compensating,
}

public enum StepStatus { NotStarted, Running, Completed, Failed, Skipped, Retrying, Compensated, CompensationFailed }
public enum ExecutionPattern { Workflow, Saga, Tcc }
public enum TriggerMode { Sync, Async }
public enum TccPhase { Try, Confirm, Cancel }
