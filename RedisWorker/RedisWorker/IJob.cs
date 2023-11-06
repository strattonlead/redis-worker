using System;

namespace RedisWorker
{
    public interface IJob
    {
        public Guid? WorkerId { get; set; }
        public bool Running { get; set; }
        public bool Finished { get; set; }
        public bool? Failed { get; set; }
        TimeSpan? LockExpiration { get; set; }
        DateTime? LockExpirationDate { get; set; }
        DateTime? StartedDate { get; set; }
        DateTime? FinishedDate { get; set; }
        DateTime? ErrorDate { get; set; }
        TimeSpan? Duration { get; set; }
    }
}
