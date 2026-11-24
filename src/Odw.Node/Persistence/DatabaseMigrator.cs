using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Odw.Node.Persistence;

public sealed record Migration(int Version, string Name, string Sql);

public sealed record MigrationOutcome(int FromVersion, int ToVersion, int Applied);

/// <summary>
/// Migraciones embebidas y versionadas por <c>PRAGMA user_version</c> (docs/PERSISTENCE.md §4).
/// Una migración fallida revierte por completo y no avanza la versión.
/// </summary>
public static class DatabaseMigrator
{
    public const int CurrentVersion = 2;

    public static IReadOnlyList<Migration> LoadEmbedded(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var migrations = new List<Migration>();
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)))
        {
            // Recurso: Odw.Node.Persistence.Migrations.0001_init.sql
            var parts = resource.Split('.');
            if (parts.Length < 2)
            {
                continue;
            }

            var fileName = parts[^2];
            var segments = fileName.Split('_', 2);
            if (segments.Length != 2)
            {
                continue;
            }

            if (!int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"No se pudo leer el recurso {resource}.");
            using var reader = new StreamReader(stream);
            migrations.Add(new Migration(version, segments[1], reader.ReadToEnd()));
        }

        var ordered = migrations.OrderBy(migration => migration.Version).ToArray();

        for (var i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].Version == ordered[i - 1].Version)
            {
                throw new InvalidOperationException($"Migración duplicada: {ordered[i].Version}.");
            }
        }

        return ordered;
    }

    public static int GetUserVersion(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static MigrationOutcome Migrate(
        SqliteConnection connection,
        IReadOnlyList<Migration> migrations,
        TimeProvider timeProvider,
        string? backupDirectory = null,
        string appVersion = "0.1.0")
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var from = GetUserVersion(connection);
        var applied = 0;

        foreach (var migration in migrations.OrderBy(item => item.Version))
        {
            if (migration.Version <= from)
            {
                continue;
            }

            if (migration.Version != from + applied + 1)
            {
                throw new InvalidOperationException(
                    $"Migraciones no contiguas: se esperaba {from + applied + 1}, llegó {migration.Version}.");
            }

            if (backupDirectory is not null && applied == 0 && from > 0)
            {
                Backup(connection, backupDirectory, from, timeProvider);
            }

            using var transaction = connection.BeginTransaction();

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO schema_version (version, applied_at, app_version) VALUES ($version, $applied, $app);";
                command.Parameters.AddWithValue("$version", migration.Version);
                command.Parameters.AddWithValue("$applied", PersistenceJson.Timestamp(timeProvider.GetUtcNow()));
                command.Parameters.AddWithValue("$app", appVersion);
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = $"PRAGMA user_version = {migration.Version.ToString(CultureInfo.InvariantCulture)};";
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            applied++;
        }

        return new MigrationOutcome(from, GetUserVersion(connection), applied);
    }

    private static void Backup(SqliteConnection connection, string directory, int version, TimeProvider timeProvider)
    {
        Directory.CreateDirectory(directory);
        var stamp = timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"odw-pre-v{version}-{stamp}.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };

        using var destination = new SqliteConnection(builder.ToString());
        destination.Open();
        connection.BackupDatabase(destination);
    }
}
