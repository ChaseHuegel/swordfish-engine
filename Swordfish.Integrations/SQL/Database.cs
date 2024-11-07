using System;
using System.Data;
using System.Data.SqlClient;
using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Integrations.SQL
{
    public static class Database
    {
        private const string CONNECTION_STRING = "Data Source={0},{1};Initial Catalog={2};Trusted_Connection=True;Connection Timeout={3}";

        public static Query Query(string name, string address, int port, int timeout)
        {
            return new Query
            {
                Name = name,
                Address = address,
                Port = port,
                Timeout = timeout,
            };
        }

        public static Result Put(Query query)
        {
            try
            {
                using var connection = new SqlConnection(string.Format(CONNECTION_STRING, query.Address, query.Port, query.Name, query.Timeout));
                connection.Open();
                var cmd = new SqlCommand(query.ToString(), connection);
                cmd.CommandTimeout = query.Timeout;
                cmd.ExecuteNonQuery();

                return new Result(success: true);
            }
            catch (Exception ex)
            {
                return new Result(success: false, ex.ToString());
            }
        }

        public static Result<QueryResult> Get(Query query)
        {
            try
            {
                using var connection = new SqlConnection(string.Format(CONNECTION_STRING, query.Address, query.Port, query.Name, query.Timeout));
                connection.Open();
                var cmd = new SqlCommand(query.ToString(), connection);
                cmd.CommandTimeout = query.Timeout;
                var data = new SqlDataAdapter(cmd);
                var table = new DataTable();
                data.Fill(table);
                return new Result<QueryResult>(success: true, new QueryResult(table));
            }
            catch (Exception ex)
            {
                return new Result<QueryResult>(success: false, null, ex.ToString());
            }
        }
    }
}
