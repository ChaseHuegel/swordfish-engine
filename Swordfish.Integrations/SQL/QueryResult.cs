using System.Data;

namespace Swordfish.Integrations.SQL
{
    public class QueryResult
    {
        public DataTable Table { get; private set; }

        public QueryResult(DataTable table)
        {
            Table = table;
        }

        public bool Exists()
        {
            return Table.Rows.Count > 0;
        }
    }
}
