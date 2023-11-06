using System.Linq;
using System.Threading.Tasks;

namespace RedisWorker
{
    public interface IJobStore<T>
         where T : IJob
    {
        IQueryable<T> GetJobs();
        Task UpdateJobAsync(T job);
    }
}
