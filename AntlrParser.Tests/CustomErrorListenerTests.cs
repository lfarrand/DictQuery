using System;
using System.IO;
using Antlr4.Runtime;
using Xunit;

namespace AntlrParser.Tests
{
    public class CustomErrorListenerTests
    {
        private readonly CustomErrorListener _errorListener;
        private readonly StringWriter _output;
        private readonly IRecognizer _recognizer;
        private readonly IToken _offendingSymbol;

        public CustomErrorListenerTests()
        {
            _errorListener = new CustomErrorListener();
            _output = new StringWriter();
            _recognizer = null; // We don't need an actual recognizer for these tests
            _offendingSymbol = null; // We don't need an actual token for these tests
        }

        [Fact]
        public void SyntaxError_ThrowsArgumentException()
        {
            // Arrange
            const int line = 5;
            const int charPosition = 10;
            const string errorMessage = "Unexpected token";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _errorListener.SyntaxError(_output, _recognizer, _offendingSymbol,
                    line, charPosition, errorMessage, null));

            Assert.Equal($"Syntax error at line {line}:{charPosition}: {errorMessage}",
                exception.Message);
        }

        [Theory]
        [InlineData(1, 0, "Missing semicolon")]
        [InlineData(10, 20, "Invalid character")]
        [InlineData(100, 50, "Unexpected end of file")]
        public void SyntaxError_ThrowsArgumentExceptionWithCorrectMessage(
            int line, int charPosition, string errorMessage)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _errorListener.SyntaxError(_output, _recognizer, _offendingSymbol,
                    line, charPosition, errorMessage, null));

            Assert.Equal($"Syntax error at line {line}:{charPosition}: {errorMessage}",
                exception.Message);
        }

        [Fact]
        public void SyntaxError_WithNullMessage_ThrowsArgumentException()
        {
            // Arrange
            const int line = 1;
            const int charPosition = 1;
            string errorMessage = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _errorListener.SyntaxError(_output, _recognizer, _offendingSymbol,
                    line, charPosition, errorMessage, null));

            Assert.Equal($"Syntax error at line {line}:{charPosition}: ",
                exception.Message);
        }
    }
}