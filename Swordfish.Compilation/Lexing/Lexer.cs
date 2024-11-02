﻿using System;
using System.Collections.Generic;

namespace Swordfish.Compilation.Lexing;

public class Lexer<T>(in List<TokenMatcher<T>> tokenMatchers)
    where T : struct
{
    private readonly List<TokenMatcher<T>> _tokenMatchers = tokenMatchers;
    
    public List<Token<T>> Lex(string input)
    {
        var tokens = new List<Token<T>>();

        int currentIndex = 0;
        while (currentIndex < input.Length)
        {
            if (TryMatch(input, currentIndex, out Match<T> match))
            {
                tokens.Add(match.Token);
                currentIndex += match.Length;
            }
            else
            {
                currentIndex++;
            }
        }

        return tokens;
    }

    private bool TryMatch(string input, int currentIndex, out Match<T> match)
    {
        for (int i = 0; i < _tokenMatchers.Count; i++)
        {
            TokenMatcher<T> tokenMatcher = _tokenMatchers[i];
            match = tokenMatcher.Match(input, currentIndex);
            if (match.Success)
            {
                return true;
            }
        }

        //  Collect the failing line and column
        int lineCounter = 1;
        int currentLineIndex = 0;
        for (int i = 0; i < currentIndex; i++)
        {
            if (input[i] == '\n')
            {
                lineCounter++;
                currentLineIndex = i;
            }
        }

        throw new FormatException($"Encountered unknown Token at ln ({lineCounter}), col ({currentIndex - currentLineIndex + 1}).");
    }
}