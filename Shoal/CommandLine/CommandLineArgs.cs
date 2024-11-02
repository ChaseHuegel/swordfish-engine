using System.Text;

namespace Shoal.CommandLine;

public class CommandLineArgs
{
    public static readonly CommandLineArgs Empty = new([], []);

    private readonly HashSet<string> _flags = [];
    private readonly Dictionary<string, string[]> _options = [];
    
    public CommandLineArgs(in string[] flags, in KeyValuePair<string,string[]>[] options)
    {
        for (int i = 0; i < flags.Length; i++)
        {
            _flags.Add(flags[i]);
        }
        
        for (int i = 0; i < options.Length; i++)
        {
            KeyValuePair<string, string[]> collection = options[i];
            if (_options.ContainsKey(collection.Key))
            {
                continue;
            }
            
            _options.Add(collection.Key, collection.Value);
        }
    }
    
    public bool GetFlag(string flag)
    {
        return _flags.Contains(flag);
    }

    public string GetValue(string key)
    {
        return _options[key][0];
    }

    public string[] GetValues(string key)
    {
        return _options[key];
    }
    
    public bool TryGetValue(string key, out string? value)
    {
        if (_options.TryGetValue(key, out string[]? values))
        {
            value = values[0];
            return true;
        }

        value = null;
        return false;
    }

    public bool TryGetValues(string key, out string[]? values)
    {
        return _options.TryGetValue(key, out values);
    }

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();

        builder.Append("flags = [");
        builder.AppendJoin(", ", _flags.Select(flag => $"'{flag}'"));
        builder.Append(']');
        
        builder.Append(", ");
        
        builder.Append("options = [");
        int counter = 1;
        foreach (KeyValuePair<string, string[]> option in _options)
        {
            bool isCollection = option.Value.Length > 1;
            
            builder.Append('\'');
            builder.Append(option.Key);
            builder.Append('\'');
            builder.Append('=');
            if (isCollection)
            {
                builder.Append('[');
                builder.AppendJoin(", ", option.Value.Select(value => $"'{value}'"));
                builder.Append(']');
            }
            else
            {
                builder.AppendJoin(", ", option.Value.Select(value => $"'{value}'"));
            }

            if (counter != _options.Count)
            {
                builder.Append(", ");
            }
            counter++;
        }
        builder.Append(']');
        
        return builder.ToString();
    }
}