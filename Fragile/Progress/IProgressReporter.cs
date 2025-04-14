using System;

namespace Fragile.Progress
{
    /// <summary>
    /// Interface for reporting progress of long-running archive operations.
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// Reports the progress of an operation.
        /// </summary>
        /// <param name="current">The current progress value.</param>
        /// <param name="total">The total value representing completion.</param>
        /// <param name="message">An optional message describing the current operation.</param>
        void ReportProgress(long current, long total, string? message = null);

        /// <summary>
        /// Reports the start of a new operation phase.
        /// </summary>
        /// <param name="phaseName">The name or description of the phase.</param>
        void ReportPhaseStart(string phaseName);

        /// <summary>
        /// Reports the completion of the current operation phase.
        /// </summary>
        void ReportPhaseComplete();

        /// <summary>
        /// Reports a warning or non-critical issue during the operation.
        /// </summary>
        /// <param name="message">The warning message.</param>
        void ReportWarning(string message);
    }

    /// <summary>
    /// Default implementation of <see cref="IProgressReporter"/> that does nothing.
    /// </summary>
    public class NullProgressReporter : IProgressReporter
    {
        /// <inheritdoc/>
        public void ReportProgress(long current, long total, string? message = null)
        {
            // No-op
        }

        /// <inheritdoc/>
        public void ReportPhaseStart(string phaseName)
        {
            // No-op
        }

        /// <inheritdoc/>
        public void ReportPhaseComplete()
        {
            // No-op
        }

        /// <inheritdoc/>
        public void ReportWarning(string message)
        {
            // No-op
        }
    }
} 