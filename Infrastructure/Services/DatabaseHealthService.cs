using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;


public sealed class DatabaseHealthService : IDatabaseHealthService
{
    private readonly AppDbContext _db;

    public DatabaseHealthService(AppDbContext db) => _db = db;

    public async Task<bool> CanConnectAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(ct);
        }
        catch
        {
            return false;
        }
    }
}
