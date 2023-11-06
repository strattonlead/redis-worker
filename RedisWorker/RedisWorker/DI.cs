using Microsoft.Extensions.DependencyInjection;

namespace RedisWorker
{
    public static class DI
    {
        public static void AddRedisWorker<TJob, TService, TStore, TJobRunner>(this IServiceCollection services)
            where TService : RedisWorker<TJob>
            where TJob : IJob
            where TStore : class, IJobStore<TJob>
            where TJobRunner : class, IJobRunner<TJob>
        {
            services.AddScoped<TStore>();
            services.AddScoped<TJobRunner>();
            services.AddHostedService<TService>();
        }
    }
}
