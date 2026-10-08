using Microsoft.VisualStudio.TestTools.UnitTesting;
using TransactionsService.Domain.Models;

namespace TransactionsService.Tests.Domain;

/// <summary>
/// Locks in the FundTransfer aggregate's state transitions after the P2 refactor: the
/// transfer-specific TransferState and the coarse base Transaction.Status stay in sync, and
/// MarkFailed preserves the REAL reason (no more hard-coded "System failure").
/// </summary>
[TestClass]
public class FundTransferTests
{
    private static FundTransfer NewPending() =>
        FundTransfer.Create(new AccountId(1000), new AccountId(2000), new Money(500m), TransferMode.NEFT);

    [TestMethod]
    public void Create_StartsPending()
        => Assert.AreEqual(TransferStatus.PENDING, NewPending().TransferState);

    [TestMethod]
    public void MarkCompleted_SetsCompletedAndBaseSuccess()
    {
        var t = NewPending();
        t.MarkCompleted();
        Assert.AreEqual(TransferStatus.COMPLETED, t.TransferState);
        Assert.AreEqual(TransactionStatus.SUCCESS, ((Transaction)t).Status);
    }

    [TestMethod]
    public void MarkFailed_PreservesRealReasonAndBaseFailed()
    {
        var t = NewPending();
        t.MarkFailed("payment gateway declined");
        Assert.AreEqual(TransferStatus.FAILED, t.TransferState);
        Assert.AreEqual("payment gateway declined", t.FailureReason);
        Assert.AreEqual(TransactionStatus.FAILED, ((Transaction)t).Status);
    }

    [TestMethod]
    public void Execute_FromPending_MovesToProcessing()
    {
        var t = NewPending();
        t.Execute();
        Assert.AreEqual(TransferStatus.PROCESSING, t.TransferState);
    }

    [TestMethod]
    public void Execute_WhenNotPending_Throws()
    {
        var t = NewPending();
        t.MarkCompleted();
        Assert.ThrowsExactly<System.InvalidOperationException>(() => t.Execute());
    }
}
