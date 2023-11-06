using System.Threading.Tasks;

namespace RedisWorker
{
    public interface IJobRunner<T>
        where T : IJob
    {
        Task RunAsync(T job);
    }
}
