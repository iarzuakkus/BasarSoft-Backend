using BasarSoft.Data;
using BasarSoft.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BasarSoft.Repositories
{
    public sealed class EfRepository<T> : IRepository<T> where T : class
    {
        private readonly BasarSoftDbContext ctx;
        private readonly DbSet<T> set;

        public EfRepository(BasarSoftDbContext context)
        {
            ctx = context;
            set = ctx.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            // FindAsync bazı sürümlerde ValueTask, bazılarında Task dönebiliyor; en güvenlisi await.
            return await set.FindAsync(id);
        }

        public Task<List<T>> GetAllAsync() => set.ToListAsync();

        public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
            => set.Where(predicate).ToListAsync();

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
            => set.AnyAsync(predicate);

        public async Task AddAsync(T entity)
        {
            await set.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await set.AddRangeAsync(entities);
        }

        public void Update(T entity) => set.Update(entity);

        public void Remove(T entity) => set.Remove(entity);
    }
}
