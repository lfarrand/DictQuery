using System;
using System.IO;
using Antlr4.Runtime;

namespace AntlrParser
{
    public class ThrowingErrorListener : BaseErrorListener
    {
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, 
            IToken offendingSymbol, int line, int charPositionInLine, string msg, 
            RecognitionException e)
        {
            throw new ArgumentException($"Syntax error at {line}:{charPositionInLine} - {msg}");
        }
    }
}