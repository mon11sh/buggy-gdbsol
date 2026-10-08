using AutoMapper;
using UsersService.Domain.Models;
using UsersService.DTOs;

namespace UsersService.Mapping;

/// <summary>
/// CONCEPT: AutoMapper profile - convention-based domain → DTO mapping. Same-named properties map
/// automatically; the value objects (<c>Id</c>, <c>LoginId</c>), the enum and the status flag are
/// mapped explicitly with ForMember; derived response records reuse the base map (IncludeBase) and
/// ignore their own default-valued <c>Message</c>. Registered once via AddAutoMapper in Program.cs.
/// </summary>
/// <remarks>
/// AutoMapper 14 is the last MIT release and carries advisory GHSA-rvv3-g6hj-g44x (DoS through
/// uncontrolled recursion when mapping attacker-shaped object graphs). This profile only maps the
/// service's own flat domain objects, never request payloads, so the advisory is not reachable; the
/// dependency audit suppression in Directory.Build.props records that decision.
/// </remarks>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserResponse>()
            .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.LoginId, o => o.MapFrom(s => s.LoginId.Value))
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.IsActive, o => o.MapFrom(s => s.Status == UserStatus.ACTIVE));

        CreateMap<User, ViewUserResponse>().IncludeBase<User, UserResponse>();
        CreateMap<User, AddUserResponse>().IncludeBase<User, UserResponse>().ForMember(d => d.Message, o => o.Ignore());
        CreateMap<User, EditUserResponse>().IncludeBase<User, UserResponse>().ForMember(d => d.Message, o => o.Ignore());
        CreateMap<User, InactivateUserResponse>().IncludeBase<User, UserResponse>().ForMember(d => d.Message, o => o.Ignore());
    }
}
