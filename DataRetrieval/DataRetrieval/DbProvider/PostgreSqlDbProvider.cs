﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace DataRetrieval.DbProvider
{
    public class PostgreSqlDbProvider
    {
        private readonly string connectionString;

        public PostgreSqlDbProvider(
            string connectionString = "Host=db.mirvoda.com;Port=5454;Database=CoderLiQ;Username=developer;Password=rtfP@ssw0rd")
        {
            this.connectionString = connectionString;
        }

        public async Task<IEnumerable<Dictionary<string, object>>> ExecuteReadSqlCommandAsync(string command)
        {
            var result = new List<Dictionary<string, object>>();
//            var result2 = new List<object[]>();

            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                // Retrieve all rows
                using (var cmd = new NpgsqlCommand(command, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
//                            var objs = new object[reader.FieldCount];
//                            reader.GetValues(objs);
//                            result2.Add(objs);

                            var obj = new Dictionary<string, object>();

                            for (var i = 0; i < reader.FieldCount; i++) obj[reader.GetName(i)] = reader.GetValue(i);

                            result.Add(obj);
                        }
                    }
                }
            }

//            return result2;
            return result;
        }

        public async Task<IEnumerable<Dictionary<string, object>>> GetRowsAsync(string tableName = "movies", string fields = "*", string condition = "true", int count = int.MaxValue)
        {
            var command = $"SELECT {fields} FROM {tableName} WHERE {condition} LIMIT {count}";
            return await ExecuteReadSqlCommandAsync(command).ConfigureAwait(false);
        }
    }

}