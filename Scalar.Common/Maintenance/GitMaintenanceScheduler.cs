using Scalar.Common.Git;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Scalar.Common.Maintenance
{
    public class GitMaintenanceScheduler : IDisposable
    {
        private readonly TimeSpan looseObjectsDueTime = TimeSpan.FromMinutes(5);
        private readonly TimeSpan looseObjectsPeriod = TimeSpan.FromHours(6);

        private readonly TimeSpan packfileDueTime = TimeSpan.FromMinutes(30);
        private readonly TimeSpan packfilePeriod = TimeSpan.FromHours(12);

        private readonly TimeSpan commitGraphDueTime = TimeSpan.FromMinutes(15);
        private readonly TimeSpan commitGraphPeriod = TimeSpan.FromHours(1);

        private readonly TimeSpan defaultFetchCommitsAndTreesPeriod = TimeSpan.FromMinutes(15);

        private List<Timer> stepTimers;
        private ScalarContext context;
        private GitObjects gitObjects;
        private GitMaintenanceQueue queue;

        public GitMaintenanceScheduler(ScalarContext context, GitObjects gitObjects)
        {
            this.context = context;
            this.gitObjects = gitObjects;
            this.stepTimers = new List<Timer>();
            this.queue = new GitMaintenanceQueue(context);

            this.ScheduleRecurringSteps();
        }

        public void EnqueueOneTimeStep(GitMaintenanceStep step)
        {
            this.queue.TryEnqueue(step);
        }

        public void Dispose()
        {
            this.queue.Stop();

            foreach (Timer timer in this.stepTimers)
            {
                timer?.Dispose();
            }

            this.stepTimers = null;
        }

        private void ScheduleRecurringSteps()
        {
            if (this.context.Unattended)
            {
                return;
            }

            TimeSpan actualFetchCommitsAndTreesPeriod = this.defaultFetchCommitsAndTreesPeriod;
            if (!this.gitObjects.IsUsingCacheServer())
            {
                actualFetchCommitsAndTreesPeriod = TimeSpan.FromHours(24);
            }

            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new FetchCommitsAndTreesStep(this.context, this.gitObjects)),
                state: null,
                dueTime: actualFetchCommitsAndTreesPeriod,
                period: actualFetchCommitsAndTreesPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new LooseObjectsStep(this.context)),
                state: null,
                dueTime: this.looseObjectsDueTime,
                period: this.looseObjectsPeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new PackfileMaintenanceStep(this.context)),
                state: null,
                dueTime: this.packfileDueTime,
                period: this.packfilePeriod));

            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new CommitGraphStep(this.context)),
                state: null,
                dueTime: this.commitGraphDueTime,
                period: this.commitGraphPeriod));
        }
    }
}
