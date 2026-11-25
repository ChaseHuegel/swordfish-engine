using Tomlet;
using GameToml = Swordfish.Library.Serialization.Toml.Toml;

namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Parses content TOML using both shipped authoritative Tomlet options and strict enum-checked options.
/// </summary>
public static class DualTomlParser
{
    private static readonly Lock _tomletLock = new();

    private static readonly TomlSerializerOptions STRICT_OPTIONS = new()
    {
        OverrideConstructorValues = true,
        IgnoreNonPublicMembers = false,
        IgnoreInvalidEnumValues = false
    };

    /// <summary>
    ///     Deserializes <paramref name="tomlText"/> through both authoritative and strict Tomlet paths.
    /// </summary>
    public static DualTomlParseResult<T> Parse<T>(string tomlText)
    {
        lock (_tomletLock)
        {
            bool authSuccess;
            T? authValue = default;
            string? authError = null;
            try
            {
                authValue = GameToml.To<T>(tomlText);
                authSuccess = true;
            }
            catch (Exception ex)
            {
                authSuccess = false;
                authError = ex.Message;
            }

            bool strictSuccess;
            T? strictValue = default;
            string? strictError = null;
            try
            {
                strictValue = TomletMain.To<T>(tomlText, STRICT_OPTIONS);
                strictSuccess = true;
            }
            catch (Exception ex)
            {
                strictSuccess = false;
                strictError = ex.Message;
            }

            return new DualTomlParseResult<T>(
                authSuccess,
                authValue,
                authError,
                strictSuccess,
                strictValue,
                strictError);
        }
    }
}
