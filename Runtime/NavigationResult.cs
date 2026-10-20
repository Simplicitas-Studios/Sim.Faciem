using System;

namespace Sim.Faciem
{
    public enum NavigationStatus
    {
        Succeeded,
        NoOp,
        MissingRegion,
        MissingViewId,
        Superseded,
        UnsupportedOperation,
        InvalidConfiguration,
        Cancelled,
        Failed
    }

    public enum NavigationStage
    {
        None,
        Validation,
        Queue,
        Construction,
        ViewPreparation,
        ViewCommit,
        Cleanup,
        Exit,
        Entry,
        Verification
    }

    /// <summary>Framework UI recovery, not rollback of arbitrary application side effects.</summary>
    public enum NavigationRecovery
    {
        /// <summary>No recovery was needed; inspect Status to distinguish success, no-op and missing lookup.</summary>
        NotRequired,
        /// <summary>This request failed before its owned UI/lifetime mutation. Admission-side supersession is not undone.</summary>
        Preserved,
        /// <summary>Known owned Views are hidden and active IDs empty; activation cleanup was attempted. User effects are not rolled back.</summary>
        Contained,
        /// <summary>Containment encountered further errors or could not establish its postconditions. Inspect Error.</summary>
        Incomplete
    }

    /// <summary>
    /// Opt-in navigation observation. Failures/cancellations carry the exception observed at the framework boundary;
    /// legacy navigation methods retain their throwing/cancellation behavior.
    /// </summary>
    public sealed class NavigationResult
    {
        public NavigationStatus Status { get; }
        /// <summary>Primary failure/lookup stage, or None after success/no-op. Recovery errors do not replace this stage.</summary>
        public NavigationStage Stage { get; }
        public NavigationRecovery Recovery { get; }
        /// <summary>Observed failure, including aggregate recovery errors. UniTask may normalize cancellation exceptions.</summary>
        public Exception Error { get; }
        public bool IsSuccess => Status == NavigationStatus.Succeeded;

        internal NavigationResult(NavigationStatus status, NavigationStage stage,
            NavigationRecovery recovery, Exception error = null)
        {
            Status = status;
            Stage = stage;
            Recovery = recovery;
            Error = error;
        }
    }
}
