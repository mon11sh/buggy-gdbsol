using AutoMapper;
using UsersService.Domain.Models;
using UsersService.DTOs;
using UsersService.Mapping;

namespace UsersService.Tests.Utils;

[TestClass]
public class UserMappingProfileTests
{
    public static IMapper NewMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<UserMappingProfile>()).CreateMapper();

    [TestMethod]
    public void Profile_configuration_is_valid()
    {
        // Every destination member is either auto-mapped, explicitly mapped, or the build fails here.
        new MapperConfiguration(cfg => cfg.AddProfile<UserMappingProfile>()).AssertConfigurationIsValid();
    }

    [TestMethod]
    public void Maps_value_objects_enum_and_status()
    {
        var user = User.Load(new UserId(7), new LoginId("sarah.admin"), "Sarah Johnson", PasswordHash.FromExistingHash("h"), UserRole.ADMIN, UserStatus.INACTIVE, new DateTime(2026, 1, 1));

        var dto = NewMapper().Map<ViewUserResponse>(user);

        Assert.AreEqual(7, dto.UserId);
        Assert.AreEqual("sarah.admin", dto.LoginId);
        Assert.AreEqual("Sarah Johnson", dto.Username);
        Assert.AreEqual("ADMIN", dto.Role);
        Assert.IsFalse(dto.IsActive);
        Assert.AreEqual(new DateTime(2026, 1, 1), dto.CreatedAt);
    }
}
