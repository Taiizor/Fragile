namespace Fragile.Helpers
{
    /// <summary>
    /// Helper class for tracking progress of parallel chunk processing
    /// </summary>
    internal class ParallelProgress(int chunkCount, IProgress<double>? progress)
    {
        private readonly double[] _chunkProgress = new double[chunkCount];
        private readonly object _lock = new();

        public void ReportChunkProgress(int chunkIndex, double chunkProgress)
        {
            if (progress == null)
            {
                return;
            }

            lock (_lock)
            {
                _chunkProgress[chunkIndex] = chunkProgress;
                double overallProgress = _chunkProgress.Sum() / _chunkProgress.Length;
                progress.Report(overallProgress);
            }
        }
    }
}