namespace WaywardBeyond.Client.Core.Services;

/// <summary>
///     The pose a character asset renders in. Standing is the default UI pose; floating is used for remote player billboards.
/// </summary>
internal enum CharacterAssetVariant
{
    Standing,
    Floating,
}