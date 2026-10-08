using Gdb.Common.Domain;

namespace Gdb.Common.Tests;

[TestClass]
public class FoundationTests
{
    private class Account : Entity<int>
    {
        public int Balance { get; set; }
        public Account(int id, int balance) : base(id) => Balance = balance;
    }

    [TestMethod]
    public void Entity_EqualityAndHashByIdentity()
    {
        var a1 = new Account(1, 100);
        var a2 = new Account(1, 999);
        var a3 = new Account(2, 100);

        Assert.AreEqual(a1, a2);
        Assert.AreEqual(a1.GetHashCode(), a2.GetHashCode());
        Assert.AreNotEqual(a1, a3);
        Assert.IsFalse(a1.Equals("not-an-entity"));
    }

    [TestMethod]
    public void Result_OkAndFail()
    {
        var ok = Result<int>.Success(42);
        Assert.IsTrue(ok.IsSuccess);
        Assert.AreEqual(42, ok.Value);
        Assert.IsNull(ok.Error);

        var bad = Result<int>.Failure("nope");
        Assert.IsFalse(bad.IsSuccess);
        Assert.AreEqual("nope", bad.Error);
    }
}
