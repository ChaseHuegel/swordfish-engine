using System;
using System.Collections.Generic;
using System.Linq;
using Swordfish.Compilation.Lexing;
using Swordfish.Compilation.Linting;

namespace Swordfish.Compilation.Parsing;

public abstract class TokenParser<TToken, TType> where TToken : struct
{
    private readonly object _parseLock = new();
    private readonly Stack<Token<TToken>> _tokenStack = new();
    private Token<TToken> _lookaheadFirst;
    private Token<TToken> _lookaheadSecond;
    
    protected abstract bool ReadNext();
    protected abstract Issue[] GetIssues();
    protected abstract TType CreateResult();

    // ReSharper disable once VirtualMemberNeverOverridden.Global
    protected virtual void OnPostProcess()
    {
    }

    public TType Parse(List<Token<TToken>> tokens)
    {
        if (tokens == null || tokens.Count == 0)
        {
            throw new ArgumentException("No tokens to parse.", nameof(tokens));
        }

        lock (_parseLock)
        {
            LoadStack(tokens);
            CreateLookaheads();
            ReadTokens();
            OnPostProcess();
            Validate();
            return CreateResult();
        }
    }

    protected bool TryReadToken(TToken t, out Token<TToken> token)
    {
        if (!_lookaheadFirst.Type.Equals(t))
        {
            token = default;
            return false;
        }

        token = _lookaheadFirst;
        DiscardToken();
        return true;
    }

    protected Token<TToken> ReadToken(TToken t)
    {
        if (!TryReadToken(t, out Token<TToken> token))
        {
            throw new ParserException($"Expected ({t.ToString()}) but found ({_lookaheadFirst.Value}).");
        }

        return token;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    protected void DiscardToken()
    {
        _lookaheadFirst = _lookaheadSecond;

        if (_tokenStack.Any())
        {
            _lookaheadSecond = _tokenStack.Pop();
        }
        else
        {
            _lookaheadSecond = new Token<TToken>(default, string.Empty);
        }
    }

    protected bool TryDiscardToken(TToken token)
    {
        if (!_lookaheadFirst.Type.Equals(token))
        {
            return false;
        }

        DiscardToken();
        return true;
    }

    // ReSharper disable once UnusedMember.Global
    protected void DiscardToken(TToken t)
    {
        if (!TryDiscardToken(t))
        {
            throw new ParserException($"Expected ({t.ToString()}) but found ({_lookaheadFirst.Value}).");
        }
    }
    
    private void LoadStack(List<Token<TToken>> tokens)
    {
        _tokenStack.Clear();

        int count = tokens.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            _tokenStack.Push(tokens[i]);
        }
    }

    private void CreateLookaheads()
    {
        _lookaheadFirst = _tokenStack.Pop();
        _lookaheadSecond = _tokenStack.Pop();
    }

    private void ReadTokens()
    {
        while (_tokenStack.Count > 0 || !TryDiscardToken(default))
        {
            if (ReadNext())
            {
                continue;
            }

            throw new ParserException($"Encountered unexpected token: ({_lookaheadFirst.Value}).");
        }
    }
    
    private void Validate()
    {
        Issue[] issues = GetIssues();
        if (issues.Length == 0)
        {
            return;
        }

        var exceptions = new List<Exception>();
        foreach (Issue issue in issues.Where(issue => issue.Level == IssueLevel.Error))
        {
            exceptions.Add(new ParserException(issue.Message));
        }

        throw new ParserException("There were one or more syntax errors.", exceptions);
    }
}