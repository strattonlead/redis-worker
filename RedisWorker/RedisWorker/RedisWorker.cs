using DistributedLock.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PubSubServer.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedisWorker
{
    public abstract class RedisWorker<T> : BackgroundService
        where T : IJob
    {
        #region Properties

        public virtual Guid WorkerId { get; protected set; } = Guid.NewGuid();
        protected readonly IPubSubService pubSubService;
        protected abstract string EventName { get; }
        protected readonly IServiceProvider serviceProvider;
        protected readonly ILogger logger;
        protected readonly IDistributedLockService distributedLockService;
        protected string LockName => typeof(RedisWorker<T>).FullName;
        private readonly ManualResetEvent _resetEvent;

        #endregion

        #region Constructor

        public RedisWorker(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            pubSubService = serviceProvider.GetRequiredService<IPubSubService>();
            distributedLockService = serviceProvider.GetRequiredService<IDistributedLockService>();
            logger = serviceProvider.GetRequiredService<ILogger<RedisWorker<T>>>();
            _resetEvent = new ManualResetEvent(false);
        }

        #endregion

        #region Events

        #endregion

        #region Background Service

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () =>
            {
                await pubSubService.SubscribeAsync(EventName, () =>
                {
                    _resetEvent.Set();
                }, stoppingToken);

                stoppingToken.Register(() => _resetEvent.Set());

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        _resetEvent.WaitOne();
                        _resetEvent.Reset();

                        using (var scope = serviceProvider.CreateScope())
                        {
                            var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore<T>>();
                            var jobRunner = scope.ServiceProvider.GetRequiredService<IJobRunner<T>>();

                            T job;
                            do
                            {
                                job = await _getJob(jobStore);
                                if (job != null)
                                {
                                    try
                                    {
                                        await jobRunner.RunAsync(job);
                                        job.Finished = true;
                                        job.Running = false;
                                        job.FinishedDate = DateTime.UtcNow;
                                        job.Duration = job.FinishedDate.Value - job.StartedDate.Value;
                                    }
                                    catch (Exception)
                                    {
                                        job.Running = false;
                                        job.Failed = true;
                                        job.ErrorDate = DateTime.UtcNow;
                                        job.Duration = job.ErrorDate.Value - job.StartedDate.Value;
                                    }

                                    await jobStore.UpdateJobAsync(job);
                                }
                            } while (job != null);
                        }
                    }
                    catch (Exception e)
                    {

                    }
                }
            }, stoppingToken);
        }

        private async Task<T> _getJob(IJobStore<T> jobStore)
        {
            var expiration = TimeSpan.FromSeconds(10);
            using (var @lock = distributedLockService.CreateLock(LockName, expiration))
            {
                if (@lock.IsAcquired)
                {
                    var job = jobStore.GetJobs().FirstOrDefault(x => !x.Finished && !x.Running);
                    job.Running = true;
                    job.WorkerId = WorkerId;
                    job.LockExpiration = expiration;
                    job.LockExpirationDate = DateTime.UtcNow.Add(expiration);
                    job.StartedDate = DateTime.UtcNow;

                    await jobStore.UpdateJobAsync(job);
                    return job;
                }
            }

            return default;
        }

        #endregion
    }
}