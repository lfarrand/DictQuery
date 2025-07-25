﻿using Antlr4.Runtime;

namespace AntlrParser8;

public class CustomErrorListener : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line,
        int charPositionInLine, string msg, RecognitionException e)
    {
        throw new ArgumentException($"Syntax error at line {line}:{charPositionInLine}: {msg}");
    }
}