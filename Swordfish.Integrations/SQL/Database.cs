using System;
using System.Data;
using System.Data.SqlClient;

namespace Swordfish.Integrations.SQL
{
    public static class Database
    {
        private const string ConnectionString = "Data Source={0},{1};Initial Catalog={2};Trusted_Connection=True;Connection Timeout={3}";

        public static Query Query(string name, string address, int port, int timeout)
        {
            return new Query
            {
                Name = name,
                Address = address,
                Port = port,
                Timeout = timeout
            };
        }

        public static bool Put(Query query)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(string.Format(ConnectionString, query.Address, query.Port, query.Name, query.Timeout)))
                {
                    connection.Open();
                    SqlCommand cmd = new SqlCommand(query.ToString(), connection);
                    cmd.CommandTimeout = query.Timeout;
                    cmd.ExecuteNonQuery();

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception executing an SQL query! {ex}");
                return false;
            }
        }

        public static QueryResult Get(Query query)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(string.Format(ConnectionString, query.Address, query.Port, query.Name, query.Timeout)))
                {
                    connection.Open();
                    SqlCommand cmd = new SqlCommand(query.ToString(), connection);
                    cmd.CommandTimeout = query.Timeout;
                    SqlDataAdapter data = new SqlDataAdapter(cmd);
                    DataTable table = new DataTable();
                    data.Fill(table);
                    return new QueryResult(table);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception executing an SQL query! {ex}");
                return new QueryResult(null);
            }
        }
    }
}
