using UsersService.Domain.Exceptions;
using UsersService.Utils;

namespace UsersService.Tests.Utils;

[TestClass]
public class PasswordPolicyTests
{
    [TestMethod]
    public void Accepts_a_password_that_meets_every_rule()
    {
        Validators.ValidatePassword("Welcome@1", "admin");
        Validators.ValidatePassword("Str0ng-Passw0rd!", null);
    }

    [TestMethod]
    [DataRow("Sh0rt!", "at least 8")]
    [DataRow("alllower1!", "uppercase")]
    [DataRow("ALLUPPER1!", "lowercase")]
    [DataRow("NoDigits!!", "digit")]
    [DataRow("NoSymbol11", "symbol")]
    [DataRow("Has Space1!", "spaces")]
    [DataRow("Teller.one1!", "login id")]
    public void Rejects_a_password_that_breaks_a_rule(string password, string reasonFragment)
    {
        var ex = Assert.ThrowsExactly<InvalidUserInputException>(() => Validators.ValidatePassword(password, "teller.one"));
        StringAssert.Contains(ex.Message, reasonFragment);
    }
}
