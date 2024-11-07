using System.Data;

namespace Swordfish.Integrations.SQL
{
    public class QueryResult(in DataTable table)
    {
        private DataTable Table { get; } = table;

        public bool Exists()
        {
            return Table.Rows.Count > 0;
        }
    }
}
