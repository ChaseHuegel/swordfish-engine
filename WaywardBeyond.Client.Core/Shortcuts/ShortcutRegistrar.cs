using System;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Shortcuts;

internal class ShortcutRegistrar : IAutoActivate
{
    public ShortcutRegistrar(in ILogger<ShortcutRegistrar> logger, in IShortcutService shortcutService, in ShortcutRegistration[] registrations)
    {
        foreach (ShortcutRegistration registration in registrations)
        {
            try
            {
                shortcutService.RegisterShortcut(registration.Create());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Caught an exception registering shortcut \"{type}\".", registrations.GetType());
            }
        }
    }
}