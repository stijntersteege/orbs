using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Orbs.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Orbs 0.1 and 0.2 created SQLite databases straight from the model, without a migration history.
/// Before migrating such a database, record the first migration as applied so EF Core does not try to
/// create tables that already exist.
/// </summary>
internal static class SqliteBaseline
{
    public static async Task EnsureAsync(SqliteOrbsDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            if (!await TableExistsAsync(connection, "orbs", cancellationToken)
                || await TableExistsAsync(connection, HistoryRepository.DefaultTableName, cancellationToken))
            {
                return;
            }

            var history = db.GetService<IHistoryRepository>();
            await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
            var row = new HistoryRow(db.Database.GetMigrations().First(), ProductInfo.GetVersion());
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(row), cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }
}
