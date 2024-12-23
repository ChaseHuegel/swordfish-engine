using System.Collections.Generic;
using System.Linq;

using Swordfish.Library.Extensions;
using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Swordfish.Integrations.SQL
{
    public class Query
    {
        private readonly List<string> _entries = [];

        public string Name { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public int Timeout { get; set; }

        private Query AddSimpleParameter(string value)
        {
            _entries.Add(value);
            return this;
        }

        private Query AppendParameter(string value)
        {
            _entries[^1] += value;
            return this;
        }


        public bool Execute() => Database.Put(this);

        public QueryResult GetResult() => Database.Get(this);

        public bool HasResult()
        {
            Result<QueryResult> queryResult = Database.Get(this);
            return queryResult && queryResult.Value.Exists();
        }

        public QueryResult GetRecord(string table, string column, string value) => Select(column).From(table).Where(column).Equals(value).GetResult();

        public QueryResult GetRecord(string table, string selectColumn, string whereColumn, string value) => Select(selectColumn).From(table).Where(whereColumn).Equals(value).GetResult();

        public bool RecordExists(string table, string column, string value) => Select(column).From(table).Where(column).Equals(value).HasResult();

        public Query Select(params string[] values) => AddSimpleParameter($"SELECT {string.Join(", ", values)}");

        public Query From(string value) => AddSimpleParameter($"FROM {value}");

        public Query Where(string value) => AddSimpleParameter($"WHERE {value}");

        public Query In(params string[] values) => AddSimpleParameter($"IN ({string.Join(", ", values)})");

        public Query Equals(string value) => AppendParameter(value == null ? "=NULL" : $"=\'{value}\'");

        public Query EqualTo(string value) => Equals(value);

        public Query And(string value) => AddSimpleParameter($"AND {value}");

        public Query InsertInto(string value) => AddSimpleParameter($"INSERT INTO {value}");

        public Query Update(string value) => AddSimpleParameter($"UPDATE {value}");

        public Query Set(string value) => AddSimpleParameter($"SET {value}");

        public Query Columns(params string[] values) => AddSimpleParameter($"({string.Join(",", values)})");

        public Query Values(params string[] values) => AddSimpleParameter($"VALUES ({string.Join(",", values.Select(x => x.Envelope("\'")))})");

        public Query End() => AppendParameter(";");

        public override string ToString()
        {
            return string.Join(" ", _entries);
        }
    }
}
