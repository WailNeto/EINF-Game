using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DinoGenesis.Persistence
{
    public sealed class SqliteGameSaveRepository : IGameSaveRepository
    {
        private const int SqliteOk = 0;
        private const int SqliteRow = 100;
        private const int SqliteDone = 101;
        private const int OpenFlags = 0x00000002 | 0x00000004 | 0x00010000;
        private const string LibraryName = "e_sqlite3";
        private readonly string databasePath;

        public SqliteGameSaveRepository(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A SQLite database path is required.", nameof(path));
            }

            databasePath = path;
        }

        public bool HasSave
        {
            get
            {
                using (var database = OpenDatabase())
                using (var statement = Prepare(database.Handle,
                           "SELECT 1 FROM game_save WHERE slot = 1 LIMIT 1;"))
                {
                    var result = Step(statement.Handle);
                    if (result != SqliteRow && result != SqliteDone)
                    {
                        throw CreateException(database.Handle, result, "Checking for a saved game");
                    }

                    return result == SqliteRow;
                }
            }
        }

        public GameSaveData Load()
        {
            using (var database = OpenDatabase())
            using (var statement = Prepare(database.Handle,
                       "SELECT schema_version, payload FROM game_save WHERE slot = 1 LIMIT 1;"))
            {
                var result = Step(statement.Handle);
                if (result == SqliteDone)
                {
                    return null;
                }

                if (result != SqliteRow)
                {
                    throw CreateException(database.Handle, result, "Reading the saved game");
                }

                var schemaVersion = Sqlite.sqlite3_column_int(statement.Handle, 0);
                if (schemaVersion != GameSaveData.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        "Save schema version " + schemaVersion + " is not supported. Expected " +
                        GameSaveData.CurrentSchemaVersion + ".");
                }

                var payload = ReadColumnText(statement.Handle, 1);
                var saveData = JsonUtility.FromJson<GameSaveData>(payload);
                if (saveData == null || saveData.schemaVersion != schemaVersion)
                {
                    throw new InvalidDataException("The saved game payload is empty or has an invalid schema version.");
                }

                return saveData;
            }
        }

        public void Save(GameSaveData saveData)
        {
            if (saveData == null || saveData.schemaVersion != GameSaveData.CurrentSchemaVersion)
            {
                throw new ArgumentException("The saved game has an unsupported schema version.", nameof(saveData));
            }

            var payload = JsonUtility.ToJson(saveData);
            using (var database = OpenDatabase())
            {
                Execute(database.Handle, "BEGIN IMMEDIATE;");
                try
                {
                    using (var statement = Prepare(database.Handle,
                               "INSERT INTO game_save (slot, schema_version, payload, saved_at_utc) " +
                               "VALUES (1, ?1, ?2, ?3) " +
                               "ON CONFLICT(slot) DO UPDATE SET " +
                               "schema_version = excluded.schema_version, " +
                               "payload = excluded.payload, saved_at_utc = excluded.saved_at_utc;"))
                    {
                        Check(database.Handle,
                            Sqlite.sqlite3_bind_int(statement.Handle, 1, saveData.schemaVersion),
                            "Binding the save schema version");
                        BindText(database.Handle, statement.Handle, 2, payload);
                        BindText(database.Handle, statement.Handle, 3, DateTime.UtcNow.ToString("O"));
                        var result = Step(statement.Handle);
                        if (result != SqliteDone)
                        {
                            throw CreateException(database.Handle, result, "Writing the saved game");
                        }
                    }

                    Execute(database.Handle, "COMMIT;");
                }
                catch
                {
                    Execute(database.Handle, "ROLLBACK;");
                    throw;
                }
            }
        }

        private DatabaseHandle OpenDatabase()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (Application.platform != RuntimePlatform.OSXEditor &&
                Application.platform != RuntimePlatform.OSXPlayer)
            {
                throw new PlatformNotSupportedException("The bundled SQLite native library supports macOS only.");
            }

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var path = AllocateUtf8(databasePath);
            try
            {
                var result = Sqlite.sqlite3_open_v2(path, out var database, OpenFlags, IntPtr.Zero);
                if (result != SqliteOk)
                {
                    var exception = CreateException(database, result, "Opening the SQLite database");
                    if (database != IntPtr.Zero)
                    {
                        Sqlite.sqlite3_close_v2(database);
                    }

                    throw exception;
                }

                var handle = new DatabaseHandle(database);
                try
                {
                    Execute(database, "PRAGMA foreign_keys = ON;");
                    Execute(database,
                        "CREATE TABLE IF NOT EXISTS game_save (" +
                        "slot INTEGER PRIMARY KEY CHECK (slot = 1), " +
                        "schema_version INTEGER NOT NULL, " +
                        "payload TEXT NOT NULL, " +
                        "saved_at_utc TEXT NOT NULL);");
                    return handle;
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(path);
            }
#else
            throw new PlatformNotSupportedException(
                "SQLite game saves are configured for the macOS ARM64 player and macOS Unity Editor.");
#endif
        }

        private static StatementHandle Prepare(IntPtr database, string sql)
        {
            var sqlPointer = AllocateUtf8(sql);
            try
            {
                Check(database,
                    Sqlite.sqlite3_prepare_v2(database, sqlPointer, -1, out var statement, IntPtr.Zero),
                    "Preparing a SQLite statement");
                return new StatementHandle(statement);
            }
            finally
            {
                Marshal.FreeHGlobal(sqlPointer);
            }
        }

        private static void Execute(IntPtr database, string sql)
        {
            var sqlPointer = AllocateUtf8(sql);
            try
            {
                var result = Sqlite.sqlite3_exec(database, sqlPointer, IntPtr.Zero, IntPtr.Zero, out var error);
                if (result != SqliteOk)
                {
                    var message = error == IntPtr.Zero ? GetErrorMessage(database) : ReadNullTerminatedUtf8(error);
                    if (error != IntPtr.Zero)
                    {
                        Sqlite.sqlite3_free(error);
                    }

                    throw new InvalidOperationException("SQLite error while executing SQL: " + message);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(sqlPointer);
            }
        }

        private static void BindText(IntPtr database, IntPtr statement, int index, string value)
        {
            var text = AllocateUtf8(value);
            try
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                Check(database,
                    Sqlite.sqlite3_bind_text(statement, index, text, byteCount, new IntPtr(-1)),
                    "Binding SQLite text");
            }
            finally
            {
                Marshal.FreeHGlobal(text);
            }
        }

        private static int Step(IntPtr statement)
        {
            return Sqlite.sqlite3_step(statement);
        }

        private static string ReadColumnText(IntPtr statement, int column)
        {
            var text = Sqlite.sqlite3_column_text(statement, column);
            var byteCount = Sqlite.sqlite3_column_bytes(statement, column);
            if (text == IntPtr.Zero || byteCount <= 0)
            {
                return string.Empty;
            }

            var bytes = new byte[byteCount];
            Marshal.Copy(text, bytes, 0, byteCount);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string ReadNullTerminatedUtf8(IntPtr text)
        {
            var length = 0;
            while (Marshal.ReadByte(text, length) != 0)
            {
                length++;
            }

            var bytes = new byte[length];
            Marshal.Copy(text, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static IntPtr AllocateUtf8(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            var pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        private static void Check(IntPtr database, int result, string operation)
        {
            if (result != SqliteOk)
            {
                throw CreateException(database, result, operation);
            }
        }

        private static Exception CreateException(IntPtr database, int result, string operation)
        {
            return new InvalidOperationException(
                operation + " failed with SQLite result " + result + ": " + GetErrorMessage(database));
        }

        private static string GetErrorMessage(IntPtr database)
        {
            if (database == IntPtr.Zero)
            {
                return "No SQLite database handle is available.";
            }

            var message = Sqlite.sqlite3_errmsg(database);
            return message == IntPtr.Zero ? "Unknown SQLite error." : ReadNullTerminatedUtf8(message);
        }

        private sealed class DatabaseHandle : IDisposable
        {
            public IntPtr Handle { get; private set; }

            public DatabaseHandle(IntPtr handle)
            {
                Handle = handle;
            }

            public void Dispose()
            {
                if (Handle == IntPtr.Zero)
                {
                    return;
                }

                var handle = Handle;
                Handle = IntPtr.Zero;
                var result = Sqlite.sqlite3_close_v2(handle);
                if (result != SqliteOk)
                {
                    throw CreateException(handle, result, "Closing the SQLite database");
                }
            }
        }

        private sealed class StatementHandle : IDisposable
        {
            public IntPtr Handle { get; private set; }

            public StatementHandle(IntPtr handle)
            {
                Handle = handle;
            }

            public void Dispose()
            {
                if (Handle == IntPtr.Zero)
                {
                    return;
                }

                var handle = Handle;
                Handle = IntPtr.Zero;
                Check(IntPtr.Zero, Sqlite.sqlite3_finalize(handle), "Finalizing a SQLite statement");
            }
        }

        private static class Sqlite
        {
            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_open_v2(
                IntPtr filename,
                out IntPtr database,
                int flags,
                IntPtr vfs);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_close_v2(IntPtr database);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_exec(
                IntPtr database,
                IntPtr sql,
                IntPtr callback,
                IntPtr callbackArgument,
                out IntPtr errorMessage);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_prepare_v2(
                IntPtr database,
                IntPtr sql,
                int byteCount,
                out IntPtr statement,
                IntPtr tail);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_step(IntPtr statement);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_finalize(IntPtr statement);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_bind_int(IntPtr statement, int index, int value);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_bind_text(
                IntPtr statement,
                int index,
                IntPtr value,
                int byteCount,
                IntPtr destructor);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_column_int(IntPtr statement, int column);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_column_bytes(IntPtr statement, int column);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr sqlite3_errmsg(IntPtr database);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void sqlite3_free(IntPtr memory);
        }
    }
}
