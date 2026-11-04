using Scion.Domain;

namespace Scion.Application.Skills.Copying;

/// <summary>One side of a copy: the skills of an agent in a location.</summary>
public sealed record AgentLocation(Location Location, Agent Agent);
