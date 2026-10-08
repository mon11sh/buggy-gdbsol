using System;
using AccountsService.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountsService.Tests.Domain;

/// <summary>Invariants for the Money value object (non-negative, currency-carrying).</summary>
[TestClass]
public class MoneyTests
{
    [TestMethod]
    public void Constructor_DefaultsToInr() => Assert.AreEqual("INR", new Money(100m).Currency);

    [TestMethod]
    public void Constructor_NegativeAmount_Throws()
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Money(-1m));

    [TestMethod]
    public void Add_SumsAmounts() => Assert.AreEqual(150m, new Money(100m).Add(new Money(50m)).Amount);

    [TestMethod]
    public void Subtract_WithinBalance_Reduces() => Assert.AreEqual(60m, new Money(100m).Subtract(new Money(40m)).Amount);

    [TestMethod]
    public void Subtract_ThatWouldGoNegative_Throws()
        => Assert.ThrowsExactly<InvalidOperationException>(() => new Money(100m).Subtract(new Money(140m)));
}
