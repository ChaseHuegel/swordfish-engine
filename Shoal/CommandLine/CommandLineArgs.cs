using System.Text;
// ReSharper disable UnusedMember.Global

namespace Shoal.CommandLine;

public class CommandLineArgs
{
    public static readonly CommandLineArgs Empty = new([], []);

    private readonly HashSet<string> _flags = [];
    private readonly Dictionary<string, string[]> _options = [];
    
    public CommandLineArgs(in string[] flags, in KeyValuePair<string,string[]>[] options)
    {
        for (var i = 0; i < flags.Length; i++)
        {
            _flags.Add(flags[i]);
        }
        
        for (var i = 0; i < options.Length; i++)
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

    public bool TryGetValue(string key, out int value)
    {
        if (TryGetValue(key, out string? valueStr) && int.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out float value)
    {
        if (TryGetValue(key, out string? valueStr) && float.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out double value)
    {
        if (TryGetValue(key, out string? valueStr) && double.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out long value)
    {
        if (TryGetValue(key, out string? valueStr) && long.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out short value)
    {
        if (TryGetValue(key, out string? valueStr) && short.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out byte value)
    {
        if (TryGetValue(key, out string? valueStr) && byte.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out uint value)
    {
        if (TryGetValue(key, out string? valueStr) && uint.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out ulong value)
    {
        if (TryGetValue(key, out string? valueStr) && ulong.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }
    
    public bool TryGetValue(string key, out ushort value)
    {
        if (TryGetValue(key, out string? valueStr) && ushort.TryParse(valueStr, out value))
        {
            return true;
        }
        
        value = 0;
        return false;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("flags = [");
        builder.AppendJoin(", ", _flags.Select(flag => $"'{flag}'"));
        builder.Append(']');
        
        builder.Append(", ");
        
        builder.Append("options = [");
        var counter = 1;
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