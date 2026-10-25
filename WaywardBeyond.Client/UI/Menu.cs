using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.UI;

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
        EnterPage(_pages[_currentPage]);
    }

    public abstract bool IsVisible();
    
    public TIdentifier GetCurrentPage()
    {
        lock (_pageLock)
        {
            return _currentPage;
        }
    }

    public Result<TIdentifier> GetPreviousPage()
    {
        lock (_pageLock)
        {
            if (_pageHistory.TryPeek(out TIdentifier? page))
            {
                return Result<TIdentifier>.FromSuccess(page);
            }
            
            return Result<TIdentifier>.FromFailure("There is no page in the history.");
        }
    }

    public Result GoToPage(TIdentifier page)
    {
        IMenuPage<TIdentifier>? previousPage;
        IMenuPage<TIdentifier>? nextPage;

        lock (_pageLock)
        {
            if (!_pages.TryGetValue(page, out nextPage))
            {
                return Result.FromFailure($"Page \"{page.ToString()}\" not found.");
            }

            if (_currentPage.Equals(page))
            {
                return Result.FromSuccess();
            }

            previousPage = _pages[_currentPage];
            _pageHistory.Push(_currentPage);
            _currentPage = page;
        }

        ExitPage(previousPage);
        EnterPage(nextPage);
        return Result.FromSuccess();
    }
    
    public Result GoBack()
    {
        IMenuPage<TIdentifier>? previousPage;
        IMenuPage<TIdentifier>? nextPage;

        lock (_pageLock)
        {
            if (!_pageHistory.TryPop(out TIdentifier? page))
            {
                return Result.FromFailure("There is no page in the history to go back to.");
            }

            previousPage = _pages[_currentPage];
            nextPage = _pages[page];
            _currentPage = page;
        }

        ExitPage(previousPage);
        EnterPage(nextPage);
        return Result.FromSuccess();
    }

    private void EnterPage(IMenuPage<TIdentifier> page)
    {
        Result<Exception> result = Safe.Invoke(page.OnEnter);
        if (!result)
        {
            _logger.LogError(result.Value, "Failed to enter menu page \"{page}\".", page.GetType());
        }
    }

    private void ExitPage(IMenuPage<TIdentifier> page)
    {
        Result<Exception> result = Safe.Invoke(page.OnExit);
        if (!result)
        {
            _logger.LogError(result.Value, "Failed to exit menu page \"{page}\".", page.GetType());
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