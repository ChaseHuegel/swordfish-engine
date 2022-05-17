namespace Swordfish.Library.Types
{
    public class DynamicEnumValue
    {
        public int? ID { get; private set; }

        public string Name { get; private set; }
        
        public DynamicEnumValue(int? id, string name)
        {
            ID = id;
            Name = name;
        }

        public static implicit operator int(DynamicEnumValue c) => c.ID.Value;
        public static implicit operator DynamicEnumValue(int id) => new DynamicEnumValue(id, null);
        public static implicit operator DynamicEnumValue(string name) => new DynamicEnumValue(null, name);

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            DynamicEnumValue other = obj as DynamicEnumValue;
            return other != null && this.ID == other.ID || this.Name == other.Name;
        }

        public override int GetHashCode()
        {
            return ID?.GetHashCode() ?? Name.GetHashCode();
        }
    }
}
