using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Data;

public interface ISaveMetaStorage
{
    Result<SaveMeta> Get(string levelGuid);

    IEnumerable<KeyValuePair<string, SaveMeta>> GetAll();

    Result Save(string levelGuid, SaveMeta meta);

    Result Delete(string levelGuid);
}