using BasarSoft.Repositories.Interfaces;

namespace BasarSoft.UnitOfWork
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        IRepository<T> Repository<T>() where T : class;
        Task<int> CompleteAsync(); // SaveChanges
    }
}
