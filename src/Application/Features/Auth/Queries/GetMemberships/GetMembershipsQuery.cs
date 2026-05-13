using Application.Models.Auth;
using MediatR;

namespace Application.Features.Auth.Queries.GetMemberships;

public record GetMembershipsQuery : IRequest<IReadOnlyList<MembershipInfo>>;
