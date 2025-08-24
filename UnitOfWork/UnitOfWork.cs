using BasarSoft.Data;
using BasarSoft.Repositories;
using BasarSoft.Repositories.Interfaces;

namespace BasarSoft.UnitOfWork
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        readonly BasarSoftDbContext ctx;
        readonly Dictionary<Type, object> repos = new();
        public UnitOfWork(BasarSoftDbContext context) => ctx = context;

        public IRepository<T> Repository<T>() where T : class
        {
            var t = typeof(T);
            if (repos.TryGetValue(t, out var r)) return (IRepository<T>)r;
            var repo = new EfRepository<T>(ctx);
            repos[t] = repo;
            return repo;
        }

        public Task<int> CompleteAsync() => ctx.SaveChangesAsync();
        public ValueTask DisposeAsync() => ctx.DisposeAsync();
    }
}
