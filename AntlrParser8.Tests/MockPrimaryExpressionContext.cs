﻿namespace AntlrParser8.Tests;

public class MockPrimaryExpressionContext : ModelExpressionParser.PrimaryExpressionContext
{
    public MockPrimaryExpressionContext() : base(null, 0)
    {
    }

    public override bool Equals(object obj)
    {
        return false;
    }
}