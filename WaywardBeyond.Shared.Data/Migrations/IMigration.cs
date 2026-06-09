using LiteDB;

namespace WaywardBeyond.Shared.Data.Migrations;

public interface IMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    void Apply(LiteDatabase db);
}
