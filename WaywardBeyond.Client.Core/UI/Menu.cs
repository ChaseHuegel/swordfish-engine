using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core.UI;

internal abstract class Menu<TIdentifier> : IUILayer
    where TIdentifier : notnull
{
    private readonly ILogger _logger;
    private readonly ReefContext _reefContext;
    private readonly Dictionary<TIdentifier, IMenuPage<TIdentifier>> _pages;

    private readonly object _pageLock = new();
    private TIdentifier _currentPage;
    private readonly Stack<TIdentifier> _pageHistory =[];
    
    public Menu(ILogger<Menu<TIdentifier>> logger, ReefContext reefContext, IMenuPage<TIdentifier>[] pages)
    {
        _logger = logger;
        _reefContext = reefContext;

        _pages = new Dictionary<TIdentifier, IMenuPage<TIdentifier>>();
        for (var i = 0; i < pages.Length; i++)
        {
            IMenuPage<TIdentifier> page = pages[i];
            _pages[page.ID] = page;
        }
        
        _currentPage = _pages.Keys.First();
    }

    public abstract bool IsVisible();

    public TIdentifier GetCurrentPage()
    {
        lock (_pageLock)
        {
            return _currentPage;
        }
    }

    public Result GoToPage(TIdentifier page)
    {
        lock (_pageLock)
        {
            if (!_pages.ContainsKey(page))
            {
                return Result.FromFailure($"Page \"{page.ToString()}\" not found.");
            }

            _pageHistory.Push(_currentPage);
            _currentPage = page;
            return Result.FromSuccess();
        }
    }
    
    public Result GoBack()
    {
        lock (_pageLock)
        {
            if (!_pageHistory.TryPop(out TIdentifier? page))
            {
                return Result.FromFailure("There is no page in the history to go back to.");
            }
            
            _currentPage = page;
            return Result.FromSuccess();
        }
    }

    public virtual Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        IMenuPage<TIdentifier>? page;
        lock (_pageLock)
        {
            if (!_pages.TryGetValue(_currentPage, out page))
            {
                return Result.FromSuccess();
            }
        }

        try
        {
            Result result = page.RenderPage(delta, _reefContext.Builder, menu: this);
            if (!result)
            {
                _logger.LogError(result, "Failed to render menu page \"{page}\".", page.GetType());
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Caught an exception when rendering menu page \"{page}\".", page.GetType());
        }
        
        return Result.FromSuccess();
    }
}